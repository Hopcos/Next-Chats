using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextChats.Core.Abstractions;
using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Api.Controllers;

/// <summary>用户收藏的对话（按用户隔离；一对提问+回答；去重、重命名、删除）</summary>
[ApiController]
[Authorize]
[Route("api/chat/favorites")]
public sealed class FavoritesController(IChatStore chat, IAuditLogger audit) : ApiControllerBase
{
    public sealed record CreateFavoriteRequest(
        Guid? QuestionMessageId,
        string? Question,
        string? Answer,
        string? Title);

    public sealed record RenameFavoriteRequest(string Title);

    /// <summary>收藏列表（新建在前）</summary>
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await chat.ListFavoritesAsync(UserId));

    /// <summary>收藏一对提问+回答；若已存在则返回 409（前端提示“该对话已收藏”）</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFavoriteRequest? req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Question) && string.IsNullOrWhiteSpace(req.Answer))
        {
            return BadRequest(Err("EMPTY_FAVORITE"));
        }

        // 去重：同一用户对同一来源问题消息只收藏一次
        var existing = await chat.FindFavoriteByQuestionAsync(UserId, req.QuestionMessageId);
        if (existing is not null)
        {
            // 前端据 code 提示“该对话已收藏”
            return Conflict(Err("FAVORITE_DUPLICATED"));
        }

        var favorite = new UserFavorite
        {
            UserId = UserId,
            QuestionMessageId = req.QuestionMessageId,
            QuestionText = req.Question,
            AnswerText = req.Answer,
            Title = (req.Title?.Trim() is { Length: > 0 } t ? t : SummarizeTitle(req.Question)),
        };
        var created = await chat.AddFavoriteAsync(favorite);
        await audit.RecordAsync(AuditCategory.Chat, "FAVORITE.CREATE", $"trc_{Guid.NewGuid():N}"[..24], UserId, created.Id.ToString());
        return Ok(created);
    }

    /// <summary>重命名收藏</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameFavoriteRequest req)
    {
        var ok = await chat.RenameFavoriteAsync(UserId, id, req.Title);
        if (!ok) return NotFound(Err("FAVORITE_NOT_FOUND"));
        return NoContent();
    }

    /// <summary>删除收藏（确认后由前端调用）</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await chat.DeleteFavoriteAsync(UserId, id);
        if (!ok) return NotFound(Err("FAVORITE_NOT_FOUND"));
        await audit.RecordAsync(AuditCategory.Chat, "FAVORITE.DELETE", $"trc_{Guid.NewGuid():N}"[..24], UserId, id.ToString());
        return NoContent();
    }

    /// <summary>默认标题：提问前 28 字符（固定长度展示，可手工重命名）</summary>
    private static string SummarizeTitle(string? question)
    {
        var text = (question ?? "").Trim();
        if (text.Length <= 28) return text.Length > 0 ? text : "Favorite";
        return text[..28].TrimEnd() + "…";
    }
}
