using BriefappTodoList.Api.Domain;
using BriefappTodoList.Api.Domain.OpenClaw;

namespace BriefappTodoList.Api.Services.OpenClaw;

// ── ST-77: IChannelFormatter ──────────────────────────────────────────────────

public interface IChannelFormatter
{
    ChannelType Channel { get; }
    FormattedMessage Format(string content, string? templateHint = null);
}

// ── ST-79: SmartTruncator ─────────────────────────────────────────────────────

/// <summary>ST-79: Intelligent truncation preserving code blocks and paragraphs.</summary>
public static class SmartTruncator
{
    public static TruncatedResult Truncate(string text, int maxChars, bool preserveCodeBlocks = true)
    {
        if (text.Length <= maxChars)
            return new TruncatedResult(text, false, text.Length);

        var suffix  = "\n\n📎 _(Resposta truncada — veja o dashboard para versão completa)_";
        var budget  = maxChars - suffix.Length;

        if (!preserveCodeBlocks)
        {
            var cut = text[..budget].LastIndexOf('\n');
            return new TruncatedResult($"{text[..(cut > 0 ? cut : budget)]}{suffix}", true, text.Length);
        }

        // Preserve code blocks: never cut inside ```
        var result = TruncateRespectingCodeBlocks(text, budget);
        return new TruncatedResult($"{result}{suffix}", true, text.Length);
    }

    private static string TruncateRespectingCodeBlocks(string text, int maxChars)
    {
        var inCode   = false;
        var lastSafe = 0;
        var i        = 0;

        while (i < text.Length && i < maxChars)
        {
            if (i + 2 < text.Length && text[i] == '`' && text[i + 1] == '`' && text[i + 2] == '`')
            {
                inCode = !inCode;
                i += 3;
                continue;
            }
            if (!inCode && (text[i] == '\n' || text[i] == ' '))
                lastSafe = i;
            i++;
        }

        return i >= text.Length ? text : text[..(lastSafe > 0 ? lastSafe : maxChars)];
    }
}

// ── ST-77: WhatsAppFormatter ──────────────────────────────────────────────────

/// <summary>ST-77: Formats Briefapp responses for WhatsApp (max 4096 chars).</summary>
public sealed class WhatsAppFormatter : IChannelFormatter
{
    public ChannelType Channel => ChannelType.WhatsApp;

    public FormattedMessage Format(string content, string? templateHint = null)
    {
        // WA uses *bold*, _italic_, ```code```
        var formatted = content
            .Replace("**", "*")               // md bold → wa bold
            .Replace("__", "_");              // md italic → wa italic

        var truncated = SmartTruncator.Truncate(formatted, 4096);
        return new FormattedMessage(truncated.Text, truncated.WasTruncated, truncated.OriginalLength, Channel);
    }
}

// ── ST-77: SlackFormatter ─────────────────────────────────────────────────────

/// <summary>ST-77: Formats Briefapp responses for Slack Block Kit (max 3000 chars per section).</summary>
public sealed class SlackFormatter : IChannelFormatter
{
    public ChannelType Channel => ChannelType.Slack;

    public FormattedMessage Format(string content, string? templateHint = null)
    {
        // Build a simple Block Kit JSON with a single section + code block if detected
        var hasCode = content.Contains("```");

        string blockJson;
        if (hasCode)
        {
            var truncated = SmartTruncator.Truncate(content, 2900);
            blockJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                blocks = new[]
                {
                    new { type = "section", text = new { type = "mrkdwn", text = truncated.Text } }
                }
            });
            return new FormattedMessage(blockJson, truncated.WasTruncated, truncated.OriginalLength, Channel);
        }

        var trunc = SmartTruncator.Truncate(content, 3000);
        blockJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            blocks = new[]
            {
                new { type = "section", text = new { type = "mrkdwn", text = trunc.Text } }
            }
        });
        return new FormattedMessage(blockJson, trunc.WasTruncated, trunc.OriginalLength, Channel);
    }
}

// ── ST-77: TelegramFormatter ──────────────────────────────────────────────────

/// <summary>ST-77: Formats Briefapp responses for Telegram HTML (max 4096 chars).</summary>
public sealed class TelegramFormatter : IChannelFormatter
{
    public ChannelType Channel => ChannelType.Telegram;

    public FormattedMessage Format(string content, string? templateHint = null)
    {
        // Telegram supports HTML: <b>bold</b>, <code>...</code>, <pre>...</pre>
        var formatted = System.Text.RegularExpressions.Regex.Replace(content,
            @"```(\w*)\n?([\s\S]*?)```",
            m => $"<pre><code class=\"language-{m.Groups[1].Value}\">{EscapeHtml(m.Groups[2].Value)}</code></pre>");

        formatted = System.Text.RegularExpressions.Regex.Replace(formatted,
            @"\*\*(.*?)\*\*", m => $"<b>{m.Groups[1].Value}</b>");

        formatted = System.Text.RegularExpressions.Regex.Replace(formatted,
            @"`(.*?)`", m => $"<code>{EscapeHtml(m.Groups[1].Value)}</code>");

        var truncated = SmartTruncator.Truncate(formatted, 4096);
        return new FormattedMessage(truncated.Text, truncated.WasTruncated, truncated.OriginalLength, Channel);
    }

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

// ── ST-78: OutboundTemplateEngine ─────────────────────────────────────────────

/// <summary>
/// ST-78: Simple template engine using string interpolation.
/// Templates are embedded strings; file-based templates can be added in production.
/// </summary>
public sealed class OutboundTemplateEngine
{
    private static readonly Dictionary<string, string> Templates = new()
    {
        ["task_completed"] = "✅ *Task Completed*\n\n*Task:* {task_title}\n*Duration:* {duration}\n*Model:* {model}\n\n{result}",
        ["task_failed"]    = "❌ *Task Failed*\n\n*Task:* {task_title}\n*Reason:* {failure_reason}\n*Attempts:* {retry_count}",
        ["sprint_summary"] = "📊 *Sprint Summary — {sprint_name}*\n\n✅ Done: {done_count}\n⏳ In Progress: {in_progress_count}\n📋 Todo: {todo_count}\n\n*Velocity:* {velocity} pts",
        ["budget_alert"]   = "⚠️ *Budget Alert*\n\n*Scope:* {scope}/{scope_id}\n*Utilization:* {utilization_pct}%\n*Remaining:* {remaining_tokens} tokens",
    };

    public string RenderTemplate(string templateName, ChannelType channel, Dictionary<string, string> variables)
    {
        if (!Templates.TryGetValue(templateName, out var template))
            return variables.TryGetValue("fallback", out var fb) ? fb : $"[{templateName}]";

        return variables.Aggregate(template, (current, kv) =>
            current.Replace($"{{{kv.Key}}}", kv.Value));
    }

    public bool TemplateExists(string name) => Templates.ContainsKey(name);
}
