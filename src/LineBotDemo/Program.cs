using System.Text.Json;
using LineBotDemo.Bot;
using LineBotDemo.Line;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// faq.json：回覆內容，存檔後自動重新載入
builder.Configuration.AddJsonFile("faq.json", optional: false, reloadOnChange: true);

// 密鑰只從 user-secrets／環境變數讀，缺少時啟動就失敗並說明原因
builder.Services.AddOptions<LineOptions>()
    .BindConfiguration(LineOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<BotContent>().BindConfiguration(BotContent.Section);

builder.Services.AddSingleton<LineSignatureValidator>();
builder.Services.AddSingleton<ReplyEngine>();
builder.Services.AddScoped<WebhookHandler>();
builder.Services.AddHttpClient<ILineMessagingClient, LineMessagingClient>(http =>
{
    http.BaseAddress = new Uri(LineMessagingClient.BaseAddress);
    http.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

var faqCount = app.Services.GetRequiredService<IOptions<BotContent>>().Value.Faq.Count(f => f.IsValid);
if (faqCount == 0)
    throw new InvalidOperationException("faq.json 沒有任何有效的常見問題（每題需要 Title 與 Answer）");
app.Logger.LogInformation("已載入 {Count} 則常見問題", faqCount);

app.MapGet("/", () => "LINE Bot demo is running. Webhook: POST /webhook");

app.MapPost("/webhook", async (
    HttpRequest request, LineSignatureValidator validator, WebhookHandler handler,
    ILogger<Program> logger, CancellationToken ct) =>
{
    // 簽章要用「原始」內容計算，所以先讀成 bytes，驗證通過才解析 JSON
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, ct);
    var body = buffer.ToArray();

    if (!validator.IsValid(body, request.Headers["X-Line-Signature"]))
    {
        logger.LogWarning("X-Line-Signature 驗證失敗，拒絕請求");
        return Results.Unauthorized();
    }

    WebhookPayload? payload;
    try
    {
        payload = JsonSerializer.Deserialize<WebhookPayload>(body, LineJson.Options);
    }
    catch (JsonException)
    {
        return Results.BadRequest();
    }
    if (payload is null) return Results.BadRequest();

    // LINE Console 的「Verify」按鈕會送 events 為空的請求，直接回 200
    await handler.HandleAsync(payload, ct);
    return Results.Ok();
});

app.Run();

/// <summary>讓整合測試（WebApplicationFactory）可以參考。</summary>
public partial class Program;
