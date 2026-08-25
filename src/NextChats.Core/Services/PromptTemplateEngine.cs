using System.Text;
using System.Text.RegularExpressions;
using NextChats.Core.Abstractions;

namespace NextChats.Core.Services;

/// <summary>
/// Prompt 模板引擎：轻量 Handlebar 风格 —— {{var}} 插值、{{#if var}}…{{/if}}、
/// {{#each list}}…{{/each}}（循环内 {{this}} / {{this.prop}}）、
/// {{#section name}}…{{/section}} 具名片段（仅当变量 _enabledSections 包含 name 时输出）。
/// </summary>
public sealed partial class PromptTemplateEngine : IPromptTemplateEngine
{
    private sealed record Token(TokenKind Kind, string Text);

    private enum TokenKind { Text, Var, If, Each, Section, End }

    private readonly record struct Ctx(IReadOnlyDictionary<string, object?> Vars);

    public string Render(string template, IReadOnlyDictionary<string, object?> variables)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);
        var tokens = Tokenize(template);
        var sb = new StringBuilder(template.Length + 256);
        var pos = 0;
        RenderTokens(tokens, ref pos, new Ctx(variables), sb);
        return sb.ToString().TrimEnd('\n');
    }

    private static void RenderTokens(IReadOnlyList<Token> tokens, ref int pos, Ctx ctx, StringBuilder sb)
    {
        while (pos < tokens.Count)
        {
            var token = tokens[pos];
            switch (token.Kind)
            {
                case TokenKind.Text:
                    sb.Append(token.Text);
                    pos++;
                    break;

                case TokenKind.Var:
                    sb.Append(FormatValue(Resolve(ctx.Vars, token.Text)));
                    pos++;
                    break;

                case TokenKind.If:
                {
                    pos++;
                    var truthy = IsTruthy(Resolve(ctx.Vars, token.Text));
                    var inner = new StringBuilder();
                    RenderTokens(tokens, ref pos, ctx, inner);
                    if (truthy) sb.Append(inner);
                    break;
                }
                case TokenKind.Each:
                {
                    pos++;
                    var items = (Resolve(ctx.Vars, token.Text) as System.Collections.IEnumerable)?.Cast<object?>().ToList() ?? [];
                    var innerSb = new StringBuilder();
                    var consumedPos = pos;
                    foreach (var item in items)
                    {
                        var scoped = new Dictionary<string, object?>(ctx.Vars) { ["this"] = item };
                        var itemSb = new StringBuilder();
                        var p = consumedPos;
                        RenderTokens(tokens, ref p, new Ctx(scoped), itemSb);
                        sb.Append(itemSb);
                    }
                    // 跳到块末尾
                    var depth = 1;
                    while (pos < tokens.Count && depth > 0)
                    {
                        var t = tokens[pos++];
                        if (t.Kind is TokenKind.If or TokenKind.Each or TokenKind.Section) depth++;
                        else if (t.Kind == TokenKind.End) depth--;
                    }
                    break;
                }
                case TokenKind.Section:
                {
                    pos++;
                    var name = token.Text;
                    var enabled = (ctx.Vars.TryGetValue("_enabledSections", out var e)
                        && e is System.Collections.IEnumerable en
                        && en.Cast<object?>().Any(v => v?.ToString() == name));
                    var inner = new StringBuilder();
                    RenderTokens(tokens, ref pos, ctx, inner);
                    if (enabled) sb.Append(inner);
                    break;
                }
                case TokenKind.End:
                    return;
            }
        }
    }

    private static List<Token> Tokenize(string template)
    {
        var tokens = new List<Token>();
        var regex = TokenRegex();
        var pos = 0;
        foreach (Match m in regex.Matches(template))
        {
            if (m.Index > pos) tokens.Add(new Token(TokenKind.Text, template[pos..m.Index]));
            var inside = m.Groups[1].Value.Trim();
            if (inside.StartsWith("#", StringComparison.Ordinal))
            {
                var body = inside[1..].Trim();
                if (body.StartsWith("if ", StringComparison.Ordinal))
                {
                    tokens.Add(new Token(TokenKind.If, body[3..].Trim()));
                }
                else if (body.StartsWith("each ", StringComparison.Ordinal))
                {
                    tokens.Add(new Token(TokenKind.Each, body[5..].Trim()));
                }
                else if (body.StartsWith("section ", StringComparison.Ordinal))
                {
                    tokens.Add(new Token(TokenKind.Section, body[8..].Trim()));
                }
                else
                {
                    tokens.Add(new Token(TokenKind.If, body)); // 兼容裸 {{#name}} 当 if
                }
            }
            else if (inside.StartsWith("/", StringComparison.Ordinal))
            {
                tokens.Add(new Token(TokenKind.End, inside[1..].Trim()));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Var, inside));
            }
            pos = m.Index + m.Length;
        }
        if (pos < template.Length) tokens.Add(new Token(TokenKind.Text, template[pos..]));
        return tokens;
    }

    private static object? Resolve(IReadOnlyDictionary<string, object?> vars, string path)
    {
        if (path == "this") return vars.TryGetValue("this", out var t) ? t : null;

        var parts = path.Split('.');
        object? cur = vars.TryGetValue(parts[0], out var v) ? v : null;
        foreach (var p in parts.Skip(1))
        {
            if (cur is null) return null;
            var prop = cur.GetType().GetProperty(p);
            if (prop is not null)
            {
                cur = prop.GetValue(cur);
            }
            else if (cur is System.Collections.IDictionary d)
            {
                cur = d[p];
            }
            else
            {
                return null;
            }
        }
        return cur;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "",
            string s => s,
            DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm:ss"),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? "",
        };
    }

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrWhiteSpace(s),
        int i => i != 0,
        long l => l != 0,
        double d => d != 0,
        Guid g => g != Guid.Empty,
        System.Collections.IEnumerable e => e.Cast<object?>().Any(),
        _ => true,
    };

    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}")]
    private static partial Regex TokenRegex();
}
