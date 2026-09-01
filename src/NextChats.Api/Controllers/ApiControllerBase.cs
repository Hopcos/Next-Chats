using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NextChats.Core.Abstractions;
using NextChats.Core.Localization;

namespace NextChats.Api.Controllers;

/// <summary>本地化错误响应体（JSON: { code, message }）</summary>
public sealed record ApiErrorBody(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public abstract class ApiControllerBase : ControllerBase
{
    private Guid? _userId;

    /// <summary>当前登录用户 Id（JWT uid 声明）</summary>
    protected Guid UserId
    {
        get
        {
            if (_userId.HasValue) return _userId.Value;
            var raw = User.FindFirst("uid")?.Value ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(raw, out var id))
            {
                throw new UnauthorizedAccessException();
            }
            _userId = id;
            return id;
        }
    }

    protected bool IsAdminRole => User.FindAll("role").Any(c => c.Value == "admin");

    /// <summary>请求语言（X-Lang &gt; Accept-Language 前缀；zh 前缀 = 中文，其余英文）</summary>
    protected string Lang
    {
        get
        {
            var header = HttpContext.Request.Headers["X-Lang"].ToString();
            if (string.IsNullOrWhiteSpace(header))
            {
                header = HttpContext.Request.Headers.AcceptLanguage.ToString();
            }
            return header;
        }
    }

    /// <summary>构造按请求语言本地化的错误响应体（文案一律来自 Texts 字典，代码不硬编码消息）</summary>
    protected ApiErrorBody Err(string code, params object?[] args) => new(code, Texts.Get(code, Lang, args));
}

/// <summary>
/// 只读模式守卫：标记为只读的用户（即使拥有 admin 角色）对管理端写操作一律 403。
/// 逐请求读取数据库中的 IsReadonly，因此置位立即生效（不受 JWT 30 分钟窗口影响）。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AdminReadonlyGuardAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (method is "GET" or "HEAD" or "OPTIONS")
        {
            await next();
            return;
        }

        var uidRaw = context.HttpContext.User.FindFirst("uid")?.Value;
        if (Guid.TryParse(uidRaw, out var uid))
        {
            var store = context.HttpContext.RequestServices.GetRequiredService<IAdminStore>();
            var user = await store.GetUserAsync(uid, ct: context.HttpContext.RequestAborted);
            if (user is { IsReadonly: true })
            {
                var lang = context.HttpContext.Request.Headers["X-Lang"].ToString();
                await context.HttpContext.RequestServices.GetRequiredService<IAuditLogger>().RecordAsync(
                    Core.Domain.AuditCategory.Admin, "ADMIN.WRITE_BLOCKED", $"trc_{Guid.NewGuid():N}"[..24], uid,
                    ip: context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    detail: new { method, path = context.HttpContext.Request.Path.ToString(), reason = "readonly" });
                context.Result = new ObjectResult(new ApiErrorBody("USER_READONLY", Texts.Get("USER_READONLY", lang)))
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                return;
            }
        }

        await next();
    }
}

/// <summary>管理端基类（角色隔离：仅 admin 角色可访问；只读用户禁止写操作）</summary>
[Authorize(Policy = "admin")]
[AdminReadonlyGuard]
[Route("api/admin/[controller]")]
public abstract class AdminControllerBase : ApiControllerBase
{
}
