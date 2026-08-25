using Microsoft.EntityFrameworkCore;
using NextChats.Core.Entities;
using NextChats.Infrastructure.Data;
using NextChats.Infrastructure.Services;
using Serilog;

namespace NextChats.Api;

/// <summary>
/// 种子数据：内置角色 + 管理员账号（admin / admin123）+ 演示 LLM(Mock) + 默认 Prompt + 示例 Skill。
/// 内容为英文（界面默认语言为英文；用户可自行编辑 Prompt/Skill 内容）。
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(this IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NextChatsDbContext>>();
        var security = scope.ServiceProvider.GetRequiredService<NextChats.Core.Abstractions.ISecurityService>();
        await using var ctx = await db.CreateDbContextAsync();

        if (await ctx.Roles.AnyAsync())
        {
            return;
        }

        var adminRole = new AppRole { Name = "Administrator", Code = "admin", Description = "Full administrative access", IsSystem = true };
        var userRole = new AppRole { Name = "User", Code = "user", Description = "Chat and personal settings", IsSystem = true };
        ctx.Roles.AddRange(adminRole, userRole);

        var (hash, salt) = security.HashPassword("admin123");
        var admin = new AppUser
        {
            Username = "admin",
            DisplayName = "Administrator",
            Email = "admin@nextchats.local",
            PasswordHash = hash,
            PasswordSalt = salt,
            Roles = [adminRole, userRole],
        };
        ctx.Users.Add(admin);

        var mockProvider = new LlmProvider
        {
            Name = "Mock Demo Provider",
            Kind = NextChats.Core.Domain.LlmProviderKind.Mock,
            Enabled = true,
            Priority = 10,
            IsHealthy = true,
            Models =
            {
                new LlmModel { Name = "Mock Demo Model", Enabled = true, IsVision = true, ContextWindow = 32_768, Priority = 1 },
                new LlmModel { Name = "Mock Fast Mini", Enabled = true, IsVision = false, ContextWindow = 8_192, Priority = 2, PriceInPer1K = 0.01m, PriceOutPer1K = 0.02m },
            },
        };
        ctx.LlmProviders.Add(mockProvider);

        var defaultPrompt = new Prompt
        {
            Name = "General Assistant",
            Description = "Default system prompt template",
            Summary = "General conversational ability: answer questions based on tools and context; ask for clarification when ambiguous; never fabricate facts.",
            Enabled = true,
            TagsJson = "[\"default\"]",
            Content = """
You are a helpful AI assistant "Next Chats".

{{#section identity}}
- Current user: {{user.name}} (id={{user.id}})
- Session: {{session_id}}
- Server time: {{time}}
{{/section}}

## Capabilities
You can use the following MCP tools:
{{tools}}

Available Skill meta-tools (call when appropriate; instructions load lazily):
{{skills}}

## Answering guidelines
1. Give conclusions first, then necessary reasoning; do not repeat the user's input.
2. Call tools when needed and answer with tool results; when a tool fails, state it honestly and offer an alternative.
3. For sensitive operations such as delete/send/finance, explain the impact before acting.
4. When uncertain, say so clearly; never fabricate facts.

{{#section trace}}
(TraceId: {{trace_id}})
{{/section}}
""",
        };
        ctx.Prompts.Add(defaultPrompt);

        var deepAnalyzeSkill = new Skill
        {
            Name = "Deep Analysis",
            Description = "Structured deep analysis of the input material: background, key points, risks, and recommendations.",
            Summary = "Structured deep analysis: background / key points / risks / recommendations",
            MetaToolName = "skill_deep_analyze",
            Enabled = true,
            Instruction = """
You are a senior analyst. Perform a structured deep analysis of the material provided by the user. Follow the template below strictly:

## 1. Background overview (2-3 sentences)
## 2. Key points (bulleted, ~1 sentence each)
## 3. Potential risks (if any, state the risk level clearly)
## 4. Recommended actions (prioritized, at most 5)

Do not fabricate information that is not in the material; state clearly when information is insufficient.

User material:
{{input}}
""",
            ExampleInput = "Please analyze how this conversation could be improved: ...",
            MaxNestedSteps = 3,
        };
        ctx.Skills.Add(deepAnalyzeSkill);

        // 普通用户角色绑定默认 Prompt 与 Skill（MCP 由管理端后续绑定）
        userRole.Prompts = [defaultPrompt];
        userRole.Skills = [deepAnalyzeSkill];

        await ctx.SaveChangesAsync();
        Log.Information("数据库种子数据已初始化（admin/admin123）");
    }
}
