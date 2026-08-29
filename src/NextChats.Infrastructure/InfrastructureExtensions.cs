using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NextChats.Core.Abstractions;
using NextChats.Core.Services;
using NextChats.Infrastructure.Data;
using NextChats.Infrastructure.Services;

namespace NextChats.Infrastructure;

public static class InfrastructureExtensions
{
    /// <summary>注册基础设施：SQLite + EF Core 池化、缓存、安全、审计、数据存储、引擎</summary>
    public static IServiceCollection AddNextChatsInfrastructure(this IServiceCollection services, IConfiguration configuration, string? connectionString = null)
    {
        var conn = connectionString
            ?? configuration.GetConnectionString("Default")
            ?? "Data Source=data/nextchats.db";

        // ---------- 缓存 / 安全 / 审计 / 中断 ----------
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<ISessionCancellationRegistry, SessionCancellationRegistry>();

        // ---------- EF Core（SQLite，可切换 MySQL8） ----------
        services.AddDbContextFactory<NextChatsDbContext>(options =>
            options.UseSqlite(conn, sqlite => sqlite.MigrationsAssembly("NextChats.Infrastructure")));

        // ---------- 数据存储（单例 + DbContext 池化） ----------
        services.AddSingleton<NextChatsStore>();
        services.AddSingleton<IConfigStore>(sp => sp.GetRequiredService<NextChatsStore>());
        services.AddSingleton<IChatStore>(sp => sp.GetRequiredService<NextChatsStore>());
        services.AddSingleton<IAdminStore>(sp => sp.GetRequiredService<NextChatsStore>());

        // ---------- HTTP ----------
        services.AddHttpClient();
        // llm 客户端不设总超时：长思考（分钟级推理）+ 长流式输出可能远超 180s，
        // 总超时会在推理中途掐断连接。首 token 等待上限由供应商 TimeoutSeconds 在客户端内控制。
        services.AddHttpClient("llm", client => client.Timeout = System.Threading.Timeout.InfiniteTimeSpan);
        services.AddHttpClient("mcp", client => client.Timeout = TimeSpan.FromSeconds(120));
        services.AddSingleton<IHttpClientProvider, HttpClientProvider>();

        // ---------- 引擎 ----------
        services.AddSingleton<IPromptTemplateEngine, PromptTemplateEngine>();
        services.AddSingleton<IPolicyEngine, PolicyEngine>();
        services.AddSingleton<IMcpDriver, McpDriver>();
        services.AddSingleton<IApprovalCoordinator, ApprovalCoordinator>();
        services.AddSingleton<ILlmRouter, LlmRouter>();
        services.AddSingleton<IContextManager, ContextManager>();
        services.AddSingleton<ISkillExecutionEngine, SkillExecutionEngine>();
        services.AddSingleton<IAgentLoopEngine, AgentLoopEngine>();
        services.AddSingleton<IChatOrchestrator, ChatOrchestrator>();

        return services;
    }

    /// <summary>建库（EnsureCreated；生产可切换 EF Migrations）</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NextChatsDbContext>>();
        await using var ctx = await db.CreateDbContextAsync();
        await ctx.Database.EnsureCreatedAsync();
        await EnsureCompatibleSchemaAsync(ctx);
    }

    /// <summary>
    /// 轻量兼容迁移：EnsureCreated 对已存在数据库不会追加新列，这里为增量字段补列。
    /// 生产建议切换 EF Migrations 后移除。
    /// </summary>
    private static async Task EnsureCompatibleSchemaAsync(NextChatsDbContext ctx)
    {
        var conn = ctx.Database.GetDbConnection();
        var opened = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
            opened = true;
        }
        try
        {
            await AddColumnIfMissingAsync(conn, "LlmModels", "ThinkingEffort", "INTEGER NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync(conn, "LlmProviders", "ThinkingParam", "TEXT NOT NULL DEFAULT 'None'");
            await AddColumnIfMissingAsync(conn, "McpServers", "Instructions", "TEXT");
            await AddTableIfMissingAsync(conn, "UserFavorites", """
                CREATE TABLE "UserFavorites" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_UserFavorites" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "QuestionText" TEXT,
                    "AnswerText" TEXT,
                    "QuestionMessageId" TEXT,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_UserFavorites_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX "IX_UserFavorites_UserId_CreatedAt" ON "UserFavorites" ("UserId", "CreatedAt");
                CREATE INDEX "IX_UserFavorites_UserId_QuestionMessageId" ON "UserFavorites" ("UserId", "QuestionMessageId") WHERE "QuestionMessageId" IS NOT NULL;
                """);
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }

    private static async Task AddColumnIfMissingAsync(System.Data.Common.DbConnection conn, string table, string column, string definition)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
        var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
        if (exists) return;
        await using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
        await alter.ExecuteNonQueryAsync();
    }

    /// <summary>轻量建表：EnsureCreated 对已存在的库不会追加新表，这里幂等补建（表已存在则跳过）</summary>
    private static async Task AddTableIfMissingAsync(System.Data.Common.DbConnection conn, string table, string createSql)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = '{table}'";
        var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
        if (exists) return;
        await using var create = conn.CreateCommand();
        create.CommandText = createSql;
        await create.ExecuteNonQueryAsync();
    }
}
