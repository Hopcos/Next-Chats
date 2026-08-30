using Microsoft.AspNetCore.Mvc;
using NextChats.Core.Abstractions;
using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Api.Controllers;

/// <summary>管理端：内部鉴权管理（acs / ucs… 鉴权中心配置 + 成功判定规则 + 默认角色映射）</summary>
[Route("api/admin/internal-auth")]
public sealed class AdminInternalAuthController(IAdminStore store, IAuditLogger audit) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var providers = await store.ListInternalAuthProvidersAsync();
        return Ok(providers.Select(p => new
        {
            p.Id, p.Name, p.Api, p.HttpMethod, p.RequestFormat, p.UsernameField, p.PasswordField,
            p.Enabled, p.TimeoutSeconds, p.CreatedAt, p.UpdatedAt,
            successRules = p.SuccessRules.Select(r => new { r.Id, r.Field, op = r.Operator.ToString(), r.ExpectedValue }).ToList(),
            defaultRoleIds = p.DefaultRoles.Select(r => r.Id).ToList(),
        }));
    }

    public sealed record SuccessRuleInput(string Field, string? Operator, string? ExpectedValue);

    public sealed record ProviderInput(
        string Name, string Api, string? HttpMethod, string? RequestFormat,
        string? UsernameField, string? PasswordField, bool Enabled, int? TimeoutSeconds,
        SuccessRuleInput[] SuccessRules, Guid[]? DefaultRoleIds);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProviderInput input)
    {
        var error = Validate(input);
        if (error is not null) return BadRequest(Err(error));
        var saved = await store.SaveInternalAuthProviderAsync(
            Guid.Empty, input.Name.Trim(), input.Api.Trim(), (input.HttpMethod ?? "POST").Trim(),
            (input.RequestFormat ?? "BodyJson").Trim(), (input.UsernameField ?? "username").Trim(),
            (input.PasswordField ?? "password").Trim(), input.Enabled, input.TimeoutSeconds ?? 15,
            ToRules(input.SuccessRules), input.DefaultRoleIds ?? [], HttpContext.RequestAborted);
        await audit.RecordAsync(AuditCategory.Admin, "INTERNAL_AUTH.CREATE", $"trc_{Guid.NewGuid():N}"[..24], UserId,
            detail: new { name = saved.Name });
        return Ok(saved.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProviderInput input)
    {
        var error = Validate(input);
        if (error is not null) return BadRequest(Err(error));
        try
        {
            var saved = await store.SaveInternalAuthProviderAsync(
                id, input.Name.Trim(), input.Api.Trim(), (input.HttpMethod ?? "POST").Trim(),
                (input.RequestFormat ?? "BodyJson").Trim(), (input.UsernameField ?? "username").Trim(),
                (input.PasswordField ?? "password").Trim(), input.Enabled, input.TimeoutSeconds ?? 15,
                ToRules(input.SuccessRules), input.DefaultRoleIds ?? [], HttpContext.RequestAborted);
            await audit.RecordAsync(AuditCategory.Admin, "INTERNAL_AUTH.UPDATE", $"trc_{Guid.NewGuid():N}"[..24], UserId,
                detail: new { name = saved.Name });
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(Err("INTERNAL_AUTH_NOT_FOUND"));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await store.DeleteInternalAuthProviderAsync(id);
        await audit.RecordAsync(AuditCategory.Admin, "INTERNAL_AUTH.DELETE", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString());
        return NoContent();
    }

    private static string? Validate(ProviderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Api)) return "INTERNAL_AUTH_FIELDS_REQUIRED";
        if (input.SuccessRules is null || input.SuccessRules.Length == 0) return "SUCCESS_RULE_REQUIRED";
        foreach (var rule in input.SuccessRules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field)) return "SUCCESS_RULE_FIELD_REQUIRED";
            if (!Enum.TryParse<SuccessRuleOperator>(rule.Operator, true, out _)) return "SUCCESS_RULE_OPERATOR_INVALID";
        }
        return null;
    }

    private static (string Field, SuccessRuleOperator Operator, string? ExpectedValue)[] ToRules(SuccessRuleInput[] rules)
    {
        return rules.Select(r => (
            r.Field.Trim(),
            Enum.TryParse<SuccessRuleOperator>(r.Operator, true, out var op) ? op : SuccessRuleOperator.NotEmpty,
            string.IsNullOrWhiteSpace(r.ExpectedValue) ? null : r.ExpectedValue)).ToArray();
    }
}
