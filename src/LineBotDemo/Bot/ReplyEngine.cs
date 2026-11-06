using LineBotDemo.Line;
using Microsoft.Extensions.Options;

namespace LineBotDemo.Bot;

public enum ReplyKind { Help, Greeting, Faq, Fallback, Unsupported, Welcome }

/// <summary>回覆結果；<see cref="FaqTitle"/> 用來記錄命中哪一題，方便之後調整關鍵字。</summary>
public sealed record BotReply(ReplyKind Kind, IReadOnlyList<TextMessage> Messages, string? FaqTitle = null);

/// <summary>決定要回什麼（純邏輯，不呼叫 LINE，方便測試）。</summary>
public sealed class ReplyEngine(IOptionsMonitor<BotContent> content)
{
    private const int MaxTextLength = 5000; // LINE 文字訊息上限

    private static readonly string[] HelpCommands = ["說明", "選單", "help", "menu", "?", "？"];
    private static readonly string[] Greetings = ["你好", "您好", "哈囉", "嗨", "hi", "hello"];

    public BotReply Reply(string text)
    {
        var bot = content.CurrentValue;
        var faq = bot.Faq.Where(f => f.IsValid).ToList();
        var input = text.Trim();

        if (HelpCommands.Any(c => input.Equals(c, StringComparison.OrdinalIgnoreCase)))
            return Build(ReplyKind.Help, HelpText(bot, faq), faq);

        if (Greetings.Any(g => input.Equals(g, StringComparison.OrdinalIgnoreCase)))
            return Build(ReplyKind.Greeting, bot.Welcome, faq);

        if (FindFaq(input, faq) is { } hit)
            return Build(ReplyKind.Faq, hit.Answer, faq, hit.Title);

        return Build(ReplyKind.Fallback, bot.Fallback, faq);
    }

    /// <summary>使用者加好友時的歡迎訊息。</summary>
    public BotReply Welcome()
    {
        var bot = content.CurrentValue;
        return Build(ReplyKind.Welcome, bot.Welcome, bot.Faq.Where(f => f.IsValid).ToList());
    }

    /// <summary>收到貼圖、圖片等非文字訊息時。</summary>
    public BotReply Unsupported()
    {
        var bot = content.CurrentValue;
        return Build(ReplyKind.Unsupported, bot.Unsupported, bot.Faq.Where(f => f.IsValid).ToList());
    }

    /// <summary>
    /// 找最符合的 FAQ：先比對主題全文（點快速回覆按鈕送來的就是主題），
    /// 再比關鍵字，命中的關鍵字越長、越多分數越高；同分取 faq.json 中較前面的。
    /// </summary>
    private static FaqEntry? FindFaq(string input, IReadOnlyList<FaqEntry> faq)
    {
        var exact = faq.FirstOrDefault(f => input.Equals(f.Title.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        return faq
            .Select(f => (Entry: f, Score: f.Keywords.Append(f.Title)
                .Where(k => !string.IsNullOrWhiteSpace(k) && input.Contains(k.Trim(), StringComparison.OrdinalIgnoreCase))
                .Sum(k => k.Trim().Length)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score) // 穩定排序，同分保留原順序
            .Select(x => x.Entry)
            .FirstOrDefault();
    }

    private static string HelpText(BotContent bot, IReadOnlyList<FaqEntry> faq) =>
        faq.Count == 0
            ? bot.Fallback
            : $"{bot.ShopName}可以回答這些問題，點下方按鈕或直接輸入：\n" +
              string.Join("\n", faq.Select(f => $"・{f.Title}"));

    private static BotReply Build(ReplyKind kind, string text, IReadOnlyList<FaqEntry> faq, string? faqTitle = null)
    {
        if (text.Length > MaxTextLength) text = text[..MaxTextLength];
        return new BotReply(kind, [new TextMessage(text, QuickReplyFor(faq))], faqTitle);
    }

    /// <summary>每則回覆都附上 FAQ 主題按鈕，使用者不用打字就能繼續問。</summary>
    private static QuickReply? QuickReplyFor(IReadOnlyList<FaqEntry> faq)
    {
        if (faq.Count == 0) return null;
        var items = faq
            .Take(QuickReply.MaxItems)
            .Select(f =>
            {
                var title = f.Title.Trim();
                var label = title.Length > MessageAction.MaxLabelLength ? title[..MessageAction.MaxLabelLength] : title;
                return new QuickReplyItem(new MessageAction(label, title));
            })
            .ToList();
        return new QuickReply(items);
    }
}
