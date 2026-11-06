namespace LineBotDemo.Bot;

/// <summary>
/// 機器人的回覆內容，來自 faq.json 的 "Bot" 區段。
/// 店家改 faq.json 存檔後會自動重新載入，不用重開程式。
/// </summary>
public sealed class BotContent
{
    public const string Section = "Bot";

    public string ShopName { get; set; } = "";
    public string Welcome { get; set; } = "";
    public string Fallback { get; set; } = "";
    public string Unsupported { get; set; } = "";
    public List<FaqEntry> Faq { get; set; } = [];
}

public sealed class FaqEntry
{
    /// <summary>問題主題，也是快速回覆按鈕上的文字。</summary>
    public string Title { get; set; } = "";

    /// <summary>訊息包含任一關鍵字就算命中。</summary>
    public List<string> Keywords { get; set; } = [];

    public string Answer { get; set; } = "";

    public bool IsValid => !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Answer);
}
