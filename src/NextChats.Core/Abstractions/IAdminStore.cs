using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Core.Abstractions;

/// <summary>管理端 CRUD 存储</summary>
public interface IAdminStore
{
    // ---------- 用户 / 角色 ----------
    Task<IReadOnlyList<AppUser>> ListUsersAsync(CancellationToken ct = default);

    Task<AppUser?> GetUserAsync(Guid id, bool includeRoles = false, CancellationToken ct = default);

    Task<AppUser?> GetUserByNameAsync(string username, CancellationToken ct = default);

    /// <summary>按 (AuthType, Username) 组合唯一查询用户（内部鉴权用户与 default 用户可同名）</summary>
    Task<AppUser?> GetUserAsync(string authType, string username, bool includeRoles = true, CancellationToken ct = default);

    Task<AppUser> CreateUserAsync(AppUser user, IEnumerable<Guid> roleIds, CancellationToken ct = default);

    Task UpdateUserAsync(AppUser user, CancellationToken ct = default);

    Task SetUserRolesAsync(Guid userId, IEnumerable<Guid> roleIds, CancellationToken ct = default);

    Task DeleteUserAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AppRole>> ListRolesAsync(bool includeBindings = false, CancellationToken ct = default);

    Task<AppRole?> GetRoleAsync(Guid id, CancellationToken ct = default);

    Task<AppRole> CreateRoleAsync(AppRole role, CancellationToken ct = default);

    Task UpdateRoleAsync(AppRole role, CancellationToken ct = default);

    Task DeleteRoleAsync(Guid id, CancellationToken ct = default);

    /// <summary>角色 ↔ 资源绑定（Mcp/Prompt/Skill/LLM模型）</summary>
    Task SetRoleBindingsAsync(Guid roleId, Guid[] mcpIds, Guid[] promptIds, Guid[] skillIds, Guid[] modelIds, CancellationToken ct = default);

    // ---------- LLM Provider ----------
    Task<IReadOnlyList<LlmProvider>> ListProvidersAsync(CancellationToken ct = default);

    Task<LlmProvider?> GetProviderAsync(Guid id, CancellationToken ct = default);

    Task<LlmProvider> CreateProviderAsync(LlmProvider provider, CancellationToken ct = default);

    Task UpdateProviderAsync(LlmProvider provider, CancellationToken ct = default);

    Task DeleteProviderAsync(Guid id, CancellationToken ct = default);

    // ---------- LLM Model（供应商下的模型，每个模型独立配置） ----------
    Task<LlmModel?> GetModelAsync(Guid modelId, CancellationToken ct = default);

    Task<LlmModel> AddModelAsync(LlmModel model, CancellationToken ct = default);

    Task UpdateModelAsync(LlmModel model, CancellationToken ct = default);

    Task DeleteModelAsync(Guid modelId, CancellationToken ct = default);

    // ---------- MCP Server ----------
    Task<IReadOnlyList<McpServer>> ListMcpServersAsync(bool includeItems = false, CancellationToken ct = default);

    Task<McpServer?> GetMcpServerAsync(Guid id, bool includeItems = false, CancellationToken ct = default);

    Task<McpServer> CreateMcpServerAsync(McpServer server, CancellationToken ct = default);

    Task UpdateMcpServerAsync(McpServer server, CancellationToken ct = default);

    Task DeleteMcpServerAsync(Guid id, CancellationToken ct = default);

    /// <summary>替换自动带出的能力项（tools/prompts/resources），保留用户禁用状态</summary>
    Task SyncMcpCatalogAsync(Guid serverId, IReadOnlyList<McpCatalogItem> discovered, CancellationToken ct = default);

    Task SetMcpItemEnabledAsync(Guid itemId, bool enabled, CancellationToken ct = default);

    // ---------- Prompt ----------
    Task<IReadOnlyList<Prompt>> ListPromptsAsync(CancellationToken ct = default);

    Task<Prompt?> GetPromptAsync(Guid id, CancellationToken ct = default);

    Task<Prompt> CreatePromptAsync(Prompt prompt, CancellationToken ct = default);

    Task UpdatePromptAsync(Prompt prompt, CancellationToken ct = default);

    Task DeletePromptAsync(Guid id, CancellationToken ct = default);

    // ---------- Skill ----------
    Task<IReadOnlyList<Skill>> ListSkillsAsync(CancellationToken ct = default);

    Task<Skill?> GetSkillAsync(Guid id, CancellationToken ct = default);

    Task<Skill> CreateSkillAsync(Skill skill, CancellationToken ct = default);

    Task UpdateSkillAsync(Skill skill, CancellationToken ct = default);

    Task DeleteSkillAsync(Guid id, CancellationToken ct = default);

    // ---------- 内部鉴权（acs / ucs…） ----------
    Task<InternalAuthProvider?> GetInternalAuthProviderByNameAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<InternalAuthProvider>> ListInternalAuthProvidersAsync(CancellationToken ct = default);

    /// <summary>新建（id=Guid.Empty）或整体更新一条内部鉴权配置（成功判定规则与默认角色全量替换）</summary>
    Task<InternalAuthProvider> SaveInternalAuthProviderAsync(
        Guid id, string name, string api, string httpMethod, string requestFormat,
        string usernameField, string passwordField, bool enabled, int timeoutSeconds,
        IReadOnlyList<(string Field, SuccessRuleOperator Operator, string? ExpectedValue)> successRules,
        Guid[] roleIds, CancellationToken ct = default);

    Task DeleteInternalAuthProviderAsync(Guid id, CancellationToken ct = default);

    // ---------- 审计 ----------
    Task<IReadOnlyList<AuditLog>> QueryAuditLogsAsync(Guid? userId, DateTimeOffset from, DateTimeOffset to, int take, CancellationToken ct = default);
}
