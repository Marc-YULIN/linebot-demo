using LineBotDemo.Line;

namespace LineBotDemo.Bot;

/// <summary>處理 LINE 送來的事件，決定回覆並呼叫 Reply API。</summary>
public sealed class WebhookHandler(ReplyEngine engine, ILineMessagingClient line, ILogger<WebhookHandler> logger)
{
    public async Task HandleAsync(WebhookPayload payload, CancellationToken ct)
    {
        foreach (var ev in payload.Events ?? [])
        {
            try
            {
                var reply = ev switch
                {
                    { Type: "message", Message: { Type: "text", Text: { } text } } => engine.Reply(text),
                    { Type: "message" } => engine.Unsupported(),
                    { Type: "follow" } => engine.Welcome(),
                    _ => null, // 封鎖、加入群組等事件本範例不處理
                };
                if (reply is null || string.IsNullOrEmpty(ev.ReplyToken)) continue;

                // 只記錄回覆類型與命中的 FAQ，不記錄使用者 ID 與訊息內容
                logger.LogInformation("事件 {EventType} → 回覆 {ReplyKind} {FaqTitle}", ev.Type, reply.Kind, reply.FaqTitle);
                await line.ReplyAsync(ev.ReplyToken, reply.Messages, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 單一事件失敗不影響其他事件；仍回 200 給 LINE，避免重送
                logger.LogError(ex, "處理事件 {EventType} 失敗", ev.Type);
            }
        }
    }
}
