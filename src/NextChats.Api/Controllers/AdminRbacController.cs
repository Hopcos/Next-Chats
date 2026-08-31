using Microsoft.AspNetCore.Mvc;
using NextChats.Core.Abstractions;
using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Api.Controllers;

/// <summary>管理端：用户管理（RBAC）</summary>
[Route("api/admin/users")]
public sealed class AdminUserController(IAdminStore store, ISecurityService security, IAuditLogger audit) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var users = await store.ListUsersAsync();
        return Ok(users.Select(u => new
        {
            u.Id, u.Username, u.DisplayName, u.Email, status = u.Status.ToString(), authType = u.AuthType,
            u.CreatedAt, u.LastLoginAt,
            roles = u.Roles.Select(r => new { r.Id, r.Name, r.Code }).ToList(),
        }));
    }

    public sealed record UserInput(string Username, string? DisplayName, string? Email, string? Password, string? Status, Guid[]? RoleIds);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Password))
        {
            return BadRequest(Err("PASSWORD_REQUIRED"));
        }
        var (hash, salt) = security.HashPassword(input.Password);
        var user = new AppUser
        {
            Username = input.Username,
            DisplayName = input.DisplayName,
            Email = input.Email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Status = Enum.TryParse<UserStatus>(input.Status, true, out var s) ? s : UserStatus.Active,
        };
        var saved = await store.CreateUserAsync(user, input.RoleIds ?? []);
        await audit.RecordAsync(AuditCategory.Admin, "USER.CREATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, saved.Username);
        return Ok(saved.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserInput input)
    {
        var user = await store.GetUserAsync(id);
        if (user is null) return NotFound(Err("USER_NOT_FOUND"));
        user.DisplayName = input.DisplayName;
        user.Email = input.Email;
        if (!string.IsNullOrWhiteSpace(input.Password))
        {
            var (hash, salt) = security.HashPassword(input.Password);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
        }
        if (!string.IsNullOrWhiteSpace(input.Status)) user.Status = Enum.Parse<UserStatus>(input.Status, true);
        await store.UpdateUserAsync(user);
        if (input.RoleIds is not null) await store.SetUserRolesAsync(id, input.RoleIds);
        // 禁用（非 Active）→ 立即撤销该用户全部刷新令牌：其已签发的 access 到期后无法再续期
        if (user.Status != UserStatus.Active)
        {
            await store.RevokeRefreshTokensForUserAsync(id);
        }
        await audit.RecordAsync(AuditCategory.Admin, "USER.UPDATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, user.Username);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (id == UserId) return BadRequest(Err("CANNOT_DELETE_SELF"));
        await store.DeleteUserAsync(id);
        await audit.RecordAsync(AuditCategory.Admin, "USER.DELETE", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString());
        return NoContent();
    }
}

/// <summary>管理端：角色管理（角色 ↔ MCP / Prompt / Skill 绑定）</summary>
[Route("api/admin/roles")]
public sealed class AdminRoleController(IAdminStore store, IConfigStore config, IAuditLogger audit) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var roles = await store.ListRolesAsync(includeBindings: true);
        return Ok(roles.Select(r => new
        {
            r.Id, r.Name, r.Code, r.Description, r.IsSystem,
            mcpServerIds = r.McpServers.Select(m => m.Id).ToList(),
            promptIds = r.Prompts.Select(p => p.Id).ToList(),
            skillIds = r.Skills.Select(s => s.Id).ToList(),
            modelIds = r.Models.Select(m => m.Id).ToList(),
            userCount = 0,
        }));
    }

    public sealed record RoleInput(string Name, string Code, string? Description);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RoleInput input)
    {
        var role = await store.CreateRoleAsync(new AppRole { Name = input.Name, Code = input.Code, Description = input.Description });
        await audit.RecordAsync(AuditCategory.Admin, "ROLE.CREATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, role.Name);
        return Ok(role.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleInput input)
    {
        var role = await store.GetRoleAsync(id);
        if (role is null) return NotFound(Err("ROLE_NOT_FOUND"));
        role.Name = input.Name;
        role.Description = input.Description;
        await store.UpdateRoleAsync(role);
        await audit.RecordAsync(AuditCategory.Admin, "ROLE.UPDATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, role.Name);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await store.DeleteRoleAsync(id);
            await audit.RecordAsync(AuditCategory.Admin, "ROLE.DELETE", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString());
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "BAD_REQUEST", message = ex.Message });
        }
    }

    public sealed record BindingsInput(Guid[] McpServerIds, Guid[] PromptIds, Guid[] SkillIds, Guid[] ModelIds);

    /// <summary>角色 ↔ MCP/Prompt/Skill/LLM模型 绑定</summary>
    [HttpPut("{id:guid}/bindings")]
    public async Task<IActionResult> SetBindings(Guid id, [FromBody] BindingsInput input)
    {
        await store.SetRoleBindingsAsync(id, input.McpServerIds, input.PromptIds, input.SkillIds, input.ModelIds);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Admin, "ROLE.BINDINGS", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString(),
            detail: new { mcpCount = input.McpServerIds.Length, promptCount = input.PromptIds.Length, skillCount = input.SkillIds.Length, modelCount = input.ModelIds.Length });
        return NoContent();
    }
}
