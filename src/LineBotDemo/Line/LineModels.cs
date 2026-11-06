using System.Text.Json;
using System.Text.Json.Serialization;

namespace LineBotDemo.Line;

// LINE webhook 與 Messaging API 用到的資料結構（只取本範例需要的欄位）
// 規格：https://developers.line.biz/en/reference/messaging-api/

public sealed record WebhookPayload(string? Destination, IReadOnlyList<WebhookEvent>? Events);

public sealed record WebhookEvent(string Type, string? ReplyToken, EventMessage? Message);

public sealed record EventMessage(string Type, string? Text);

public sealed record TextMessage(string Text, QuickReply? QuickReply = null)
{
    public string Type => "text";
}

/// <summary>訊息下方的快速回覆按鈕，最多 13 個。</summary>
public sealed record QuickReply(IReadOnlyList<QuickReplyItem> Items)
{
    public const int MaxItems = 13;
}

public sealed record QuickReplyItem(MessageAction Action)
{
    public string Type => "action";
}

/// <summary>點按鈕後，用使用者的身分送出 <see cref="Text"/>。</summary>
public sealed record MessageAction(string Label, string Text)
{
    public const int MaxLabelLength = 20;
    public string Type => "message";
}

public sealed record ReplyRequest(string ReplyToken, IReadOnlyList<TextMessage> Messages);

public static class LineJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
