using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

/// <summary>管理端基类（角色隔离：仅 admin 角色可访问）</summary>
[Authorize(Policy = "admin")]
[Route("api/admin/[controller]")]
public abstract class AdminControllerBase : ApiControllerBase
{
}
