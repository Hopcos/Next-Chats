using Microsoft.AspNetCore.Mvc;
using NextChats.Core.Abstractions;
using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Api.Controllers;

/// <summary>管理端：Prompt 配置（多 Prompt + 启用开关）</summary>
[Route("api/admin/prompts")]
public sealed class AdminPromptController(IAdminStore store, IConfigStore config, IAuditLogger audit) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await store.ListPromptsAsync());

    public sealed record PromptInput(string Name, string? Description, string? Summary, string Content, bool Enabled, string[]? Tags);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PromptInput input)
    {
        var prompt = Map(input, new Prompt());
        var saved = await store.CreatePromptAsync(prompt);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Config, "PROMPT.CREATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, saved.Name);
        return Ok(saved.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PromptInput input)
    {
        var row = await store.GetPromptAsync(id);
        if (row is null) return NotFound(Err("PROMPT_NOT_FOUND"));
        row = Map(input, row);
        await store.UpdatePromptAsync(row);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Config, "PROMPT.UPDATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, row.Name);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await store.DeletePromptAsync(id);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Config, "PROMPT.DELETE", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString());
        return NoContent();
    }

    private static Prompt Map(PromptInput input, Prompt p)
    {
        p.Name = input.Name;
        p.Description = input.Description;
        p.Summary = input.Summary;
        p.Content = input.Content;
        p.Enabled = input.Enabled;
        p.TagsJson = input.Tags is null ? null : System.Text.Json.JsonSerializer.Serialize(input.Tags);
        return p;
    }
}

/// <summary>管理端：Skill 配置（服务端统一多套，启用开关，懒加载元工具）</summary>
[Route("api/admin/skills")]
public sealed class AdminSkillController(IAdminStore store, IConfigStore config, IAuditLogger audit) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await store.ListSkillsAsync());

    public sealed record SkillInput(
        string Name, string? Description, string? Summary, string MetaToolName, string Instruction,
        bool Enabled, string? ExampleInput, string? ExampleOutput, string? ModelOverride, int? MaxNestedSteps);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SkillInput input)
    {
        var skill = Map(input, new Skill());
        var saved = await store.CreateSkillAsync(skill);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Config, "SKILL.CREATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, saved.Name);
        return Ok(saved.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SkillInput input)
    {
        var row = await store.GetSkillAsync(id);
        if (row is null) return NotFound(Err("SKILL_NOT_FOUND"));
        row = Map(input, row);
        await store.UpdateSkillAsync(row);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Config, "SKILL.UPDATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, row.Name);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await store.DeleteSkillAsync(id);
        await config.InvalidateConfigCacheAsync();
        await audit.RecordAsync(AuditCategory.Config, "SKILL.DELETE", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString());
        return NoContent();
    }

    private static Skill Map(SkillInput input, Skill s)
    {
        s.Name = input.Name;
        s.Description = input.Description;
        s.Summary = input.Summary;
        s.MetaToolName = input.MetaToolName;
        s.Instruction = input.Instruction;
        s.Enabled = input.Enabled;
        s.ExampleInput = input.ExampleInput;
        s.ExampleOutput = input.ExampleOutput;
        s.ModelOverride = input.ModelOverride;
        s.MaxNestedSteps = input.MaxNestedSteps ?? 4;
        return s;
    }
}
