using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    IOptions<SecurityOptions> securityOptions,
    ILogger<AuthController> logger) : ApiControllerBase
{
    public sealed record LoginRequest([property: JsonPropertyName("username")] string Username, [property: JsonPropertyName("password")] string Password);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await admin.GetUserByNameAsync(request.Username);
        if (user is null || user.Status != UserStatus.Active || !security.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            var trace = $"trc_{Guid.NewGuid():N}"[..24];
            await audit.RecordAsync(AuditCategory.Auth, "LOGIN.FAILED", trace,
                target: request.Username, ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
                detail: new { reason = "invalid credentials" });
            return Unauthorized(Err("AUTH_INVALID_CREDENTIALS"));
        }

        var roles = user.Roles.Select(r => r.Code).ToArray();
        var token = JwtTokenFactory.Issue(securityOptions.Value, user.Id, user.Username, user.DisplayName ?? user.Username, roles);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await admin.UpdateUserAsync(user);

        await audit.RecordAsync(AuditCategory.Auth, "LOGIN.SUCCESS", $"trc_{Guid.NewGuid():N}"[..24], user.Id,
            ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            detail: new { username = user.Username });

        logger.LogInformation("用户登录 {Username} uid={Uid}", user.Username, user.Id);
        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id,
                username = user.Username,
                displayName = user.DisplayName ?? user.Username,
                email = user.Email,
                roles,
                isAdmin = roles.Contains("admin"),
            },
        });
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
            isAdmin = roles.Contains("admin"),
        });
    }
}
