using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextChats.Api.Security;
using NextChats.Core.Abstractions;
using NextChats.Core.Configuration;
using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAdminStore admin,
    ISecurityService security,
    IAuditLogger audit,
    IHttpClientFactory httpClientFactory,
    IOptions<SecurityOptions> securityOptions,
    ILogger<AuthController> logger) : ApiControllerBase
{
    /// <summary>本地系统账号（密码登录）对应的账号类型</summary>
    private const string DefaultAuthType = "default";

    public sealed record LoginRequest(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("authType")] string? AuthType = null);

    public sealed record RefreshRequest(
        [property: JsonPropertyName("refreshToken")] string RefreshToken);

    public sealed record LogoutRequest(
        [property: JsonPropertyName("refreshToken")] string? RefreshToken = null);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var authType = string.IsNullOrWhiteSpace(request.AuthType) ? DefaultAuthType : request.AuthType.Trim();
        AppUser? user = null;
        string? providerError = null;

        if (authType == DefaultAuthType)
        {
            user = await admin.GetUserAsync(DefaultAuthType, request.Username);
            if (user is null || user.Status != UserStatus.Active
                || !security.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                user = null;
            }
        }
        else
        {
            var result = await LoginViaInternalProviderAsync(authType, request.Username, request.Password);
            user = result.User;
            providerError = result.ErrorCode;
        }

        if (user is null)
        {
            var trace = $"trc_{Guid.NewGuid():N}"[..24];
            await audit.RecordAsync(AuditCategory.Auth, "LOGIN.FAILED", trace,
                target: request.Username, ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
                detail: new { reason = providerError ?? "invalid credentials", authType });
            return Unauthorized(Err(providerError ?? "AUTH_INVALID_CREDENTIALS"));
        }

        var roles = user.Roles.Select(r => r.Code).ToArray();
        var token = JwtTokenFactory.Issue(securityOptions.Value, user.Id, user.Username, user.DisplayName ?? user.Username, roles);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await admin.UpdateUserAsync(user);

        // 双 token：登录即撤销该用户旧的刷新令牌（旧会话无法再续期），随后签发新 refresh
        await admin.RevokeRefreshTokensForUserAsync(user.Id);
        var refreshToken = GenerateRefreshToken();
        await admin.CreateRefreshTokenAsync(
            user.Id,
            Sha256Hex(refreshToken),
            DateTimeOffset.UtcNow.AddDays(Math.Clamp(securityOptions.Value.JwtRefreshExpireDays, 1, 90)));

        await audit.RecordAsync(AuditCategory.Auth, "LOGIN.SUCCESS", $"trc_{Guid.NewGuid():N}"[..24],
            user.Id, ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            detail: new { username = user.Username, authType = user.AuthType });

        logger.LogInformation("用户登录 {Username} uid={Uid} authType={AuthType}", user.Username, user.Id, user.AuthType);
        return Ok(new
        {
            token,
            refreshToken,
            expiresIn = securityOptions.Value.JwtExpireMinutes * 60,
            user = new
            {
                id = user.Id,
                username = user.Username,
                displayName = user.DisplayName ?? user.Username,
                email = user.Email,
                roles,
                authType = user.AuthType,
                isAdmin = roles.Contains("admin"),
            },
        });
    }

    /// <summary>
    /// 刷新令牌续期（方案 B）：access 过期后借此静默换新，旧 refresh 轮换撤销、记录替换者。
    /// 校验链：令牌存在 → 未过期 → 未撤销 → 用户仍为 Active（用户被禁用时此处拒绝，令其立即掉线）。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var trace = $"trc_{Guid.NewGuid():N}"[..24];
        string reason;
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            reason = "REFRESH_TOKEN_INVALID";
            await audit.RecordAsync(AuditCategory.Auth, "REFRESH.FAILED", trace,
                ip: HttpContext.Connection.RemoteIpAddress?.ToString(), detail: new { reason });
            return Unauthorized(Err(reason));
        }

        var entity = await admin.GetRefreshTokenAsync(Sha256Hex(request.RefreshToken));
        if (entity is null || entity.User is null)
        {
            reason = "REFRESH_TOKEN_INVALID";
            await audit.RecordAsync(AuditCategory.Auth, "REFRESH.FAILED", trace,
                ip: HttpContext.Connection.RemoteIpAddress?.ToString(), detail: new { reason });
            return Unauthorized(Err(reason));
        }

        if (entity.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            reason = "REFRESH_TOKEN_EXPIRED";
        }
        else if (entity.RevokedAt is not null)
        {
            // 已轮换/已撤销：旧令牌重放 → 登录态全部作废，防令牌泄露持续利用
            reason = "REFRESH_TOKEN_REVOKED";
        }
        else if (entity.User.Status != UserStatus.Active)
        {
            reason = "AUTH_USER_DISABLED";
        }
        else
        {
            reason = string.Empty;
        }

        if (reason.Length > 0)
        {
            await audit.RecordAsync(AuditCategory.Auth, "REFRESH.FAILED", trace,
                entity.UserId, ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
                detail: new { username = entity.User.Username, reason });
            return Unauthorized(Err(reason));
        }

        // 轮换：旧令牌撤销并记录替换者 → 签发新 access + 新 refresh
        var newRefresh = GenerateRefreshToken();
        await admin.RevokeRefreshTokenAsync(Sha256Hex(request.RefreshToken), Sha256Hex(newRefresh));
        await admin.CreateRefreshTokenAsync(
            entity.UserId,
            Sha256Hex(newRefresh),
            DateTimeOffset.UtcNow.AddDays(Math.Clamp(securityOptions.Value.JwtRefreshExpireDays, 1, 90)));

        var roles = entity.User.Roles.Select(r => r.Code).ToArray();
        var token = JwtTokenFactory.Issue(securityOptions.Value, entity.User.Id, entity.User.Username,
            entity.User.DisplayName ?? entity.User.Username, roles);

        await audit.RecordAsync(AuditCategory.Auth, "REFRESH.SUCCESS", trace,
            entity.UserId, ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            detail: new { username = entity.User.Username });

        logger.LogInformation("刷新令牌续期 {Username} uid={Uid}", entity.User.Username, entity.User.Id);
        return Ok(new
        {
            token,
            refreshToken = newRefresh,
            expiresIn = securityOptions.Value.JwtExpireMinutes * 60,
        });
    }

    /// <summary>登出：撤销该用户全部有效刷新令牌（access 到期即彻底失效）</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var revoked = await admin.RevokeRefreshTokensForUserAsync(UserId);
        await audit.RecordAsync(AuditCategory.Auth, "LOGOUT", $"trc_{Guid.NewGuid():N}"[..24],
            UserId, ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            detail: new { revoked });
        return NoContent();
    }

    /// <summary>登录页可选的鉴权方式（default + 已启用的内部鉴权）</summary>
    [AllowAnonymous]
    [HttpGet("providers")]
    public async Task<IActionResult> Providers()
    {
        var providers = await admin.ListInternalAuthProvidersAsync();
        return Ok(providers.Where(p => p.Enabled).Select(p => new { name = p.Name }));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await admin.GetUserAsync(UserId, includeRoles: true);
        if (user is null) return Unauthorized(Err("AUTH_USER_NOT_FOUND"));
        var roles = user.Roles.Select(r => r.Code).ToArray();
        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            displayName = user.DisplayName ?? user.Username,
            email = user.Email,
            roles,
            authType = user.AuthType,
            isAdmin = roles.Contains("admin"),
        });
    }

    // ---------------- 内部鉴权登录 ----------------

    private sealed record InternalAuthResult(AppUser? User, string? ErrorCode);

    /// <summary>
    /// 内部鉴权登录：调用鉴权中心验证账号/密码 → 通过后按 (AuthType, Username) 建号或取号。
    /// ErrorCode 语义：null=成功；AUTH_INVALID_CREDENTIALS=凭据错误；AUTH_PROVIDER_NOT_FOUND=配置不存在/禁用；
    /// AUTH_PROVIDER_ERROR=鉴权中心不可达/响应异常。
    /// </summary>
    private async Task<InternalAuthResult> LoginViaInternalProviderAsync(string authType, string username, string password)
    {
        var provider = await admin.GetInternalAuthProviderByNameAsync(authType);
        if (provider is null || !provider.Enabled || provider.SuccessRules.Count == 0)
        {
            return new InternalAuthResult(null, "AUTH_PROVIDER_NOT_FOUND");
        }

        var (ok, body, callError) = await CallProviderAsync(provider, username, password);
        if (!ok)
        {
            return new InternalAuthResult(null, callError ?? "AUTH_INVALID_CREDENTIALS");
        }

        // 鉴权中心拒绝（非 2xx）或响应不满足成功判定规则 → 凭据错误
        if (body is null || !EvaluateSuccessRules(provider.SuccessRules, body))
        {
            return new InternalAuthResult(null, "AUTH_INVALID_CREDENTIALS");
        }

        // 已存在内部用户：直接读取其权限信息（角色由管理端维护/当前鉴权配置默认角色）
        var existing = await admin.GetUserAsync(provider.Name, username, includeRoles: true);
        if (existing is not null)
        {
            return existing.Status == UserStatus.Active
                ? new InternalAuthResult(existing, null)
                : new InternalAuthResult(null, "AUTH_INVALID_CREDENTIALS");
        }

        // 自动建号：无密码（随机哈希不可登录），用户名=显示名，状态正常，角色=鉴权配置默认角色
        var (hash, salt) = security.HashPassword(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        var created = new AppUser
        {
            AuthType = provider.Name,
            Username = username,
            DisplayName = username,
            Email = null,
            PasswordHash = hash,
            PasswordSalt = salt,
            Status = UserStatus.Active,
        };
        try
        {
            var saved = await admin.CreateUserAsync(created, provider.DefaultRoles.Select(r => r.Id).ToArray());
            logger.LogInformation("内部鉴权自动建号 {Username} authType={AuthType} uid={Uid}", saved.Username, saved.AuthType, saved.Id);
            return new InternalAuthResult(saved, null);
        }
        catch (DbUpdateException)
        {
            // 并发重复登录：唯一索引冲突 → 重读已存在的用户
            var again = await admin.GetUserAsync(provider.Name, username, includeRoles: true);
            return again is { Status: UserStatus.Active }
                ? new InternalAuthResult(again, null)
                : new InternalAuthResult(null, "AUTH_INVALID_CREDENTIALS");
        }
    }

    /// <summary>调用鉴权中心；成功返回 true 且 body 为响应原文（可能非 JSON）</summary>
    private async Task<(bool Ok, string? Body, string? ErrorCode)> CallProviderAsync(InternalAuthProvider provider, string username, string password)
    {
        var methodName = provider.HttpMethod.Trim().ToUpperInvariant();
        // 仅允许安全的 HTTP 方法 + 校验 URL 协议/主机，防止服务端请求伪造
        if (methodName is not ("POST" or "GET" or "PUT" or "PATCH"))
        {
            return (false, null, "AUTH_PROVIDER_ERROR");
        }
        if (!Uri.TryCreate(provider.Api, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, null, "AUTH_PROVIDER_ERROR");
        }

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.TimeoutSeconds, 1, 120));
        using var request = new HttpRequestMessage(new HttpMethod(methodName), uri);
        if (provider.RequestFormat.Equals("BodyJson", StringComparison.OrdinalIgnoreCase)
            && methodName is not ("GET" or "HEAD"))
        {
            var payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [provider.UsernameField] = username,
                [provider.PasswordField] = password,
            });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.SendAsync(request);
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning("内部鉴权中心不可达 authType={AuthType} api={Api} err={Err}",
                provider.Name, Core.Services.LogSanitizer.MaskUri(provider.Api), ex.Message);
            return (false, null, "AUTH_PROVIDER_ERROR");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode) return (false, null, null); // 非 2xx：凭据错误等，按未通过处理
        }
        return (true, body, null);
    }

    /// <summary>逐条判定成功规则（AND：全部满足才算成功）；响应非 JSON 视为不通过</summary>
    private static bool EvaluateSuccessRules(IReadOnlyList<InternalAuthSuccessRule> rules, string json)
    {
        using var doc = ParseJson(json);
        if (doc is null) return false;
        foreach (var rule in rules)
        {
            var value = ResolvePath(doc.RootElement, rule.Field);
            if (value is null || !MatchRule(value.Value, rule)) return false;
        }
        return true;
    }

    private static JsonDocument? ParseJson(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? ResolvePath(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current)) return null;
        }
        return current;
    }

    private static bool MatchRule(JsonElement value, InternalAuthSuccessRule rule)
    {
        if (rule.Operator == SuccessRuleOperator.Equals)
        {
            return value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), rule.ExpectedValue, StringComparison.Ordinal);
        }
        return value.ValueKind switch
        {
            JsonValueKind.Null => false,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => true, // 数字 / 布尔视为非空
        };
    }

    // ---------------- 刷新令牌辅助 ----------------

    /// <summary>生成 48 字节随机刷新令牌（Base64Url，仅哈希落库）</summary>
    private static string GenerateRefreshToken()
    {
        var buffer = new byte[48];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>刷新令牌明文 → SHA-256 十六进制（存储与查询都用哈希）</summary>
    private static string Sha256Hex(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
