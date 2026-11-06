using System.Net;
using System.Text;
using System.Text.Json;
using LineBotDemo.Line;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LineBotDemo.Tests;

/// <summary>啟動整個 ASP.NET Core 程式，從 HTTP 打 /webhook；LINE API 換成假的。</summary>
public sealed class WebhookEndpointTests : IClassFixture<WebhookEndpointTests.BotFactory>
{
    public sealed class FakeLineClient : ILineMessagingClient
    {
        public List<(string Token, IReadOnlyList<TextMessage> Messages)> Replies { get; } = [];
        public bool FailNext { get; set; }

        public Task ReplyAsync(string replyToken, IReadOnlyList<TextMessage> messages, CancellationToken ct = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new HttpRequestException("模擬 LINE API 失敗");
            }
            lock (Replies) Replies.Add((replyToken, messages));
            return Task.CompletedTask;
        }
    }

    public sealed class BotFactory : WebApplicationFactory<Program>
    {
        public FakeLineClient Line { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // 非 Development 環境不會讀 user-secrets，測試不受本機真實密鑰影響
            builder.UseEnvironment("Testing");
            builder.UseSetting("Line:ChannelSecret", TestHelpers.ChannelSecret);
            builder.UseSetting("Line:ChannelAccessToken", TestHelpers.AccessToken);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILineMessagingClient>();
                services.AddSingleton<ILineMessagingClient>(Line);
            });
        }
    }

    private readonly BotFactory _factory;
    private readonly HttpClient _client;

    public WebhookEndpointTests(BotFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        lock (factory.Line.Replies) factory.Line.Replies.Clear();
    }

    private static string TextEvent(string token, string text) => JsonSerializer.Serialize(new
    {
        type = "message",
        replyToken = token,
        source = new { type = "user", userId = "U-test" },
        message = new { type = "text", id = "1", text },
    });

    private static string Payload(params string[] events) => $$"""{"destination":"U-bot","events":[{{string.Join(",", events)}}]}""";

    private Task<HttpResponseMessage> PostAsync(string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signature is not null) request.Headers.Add("X-Line-Signature", signature);
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task RepliesToTextMessageFromFaqJson()
    {
        var body = Payload(TextEvent("token-1", "請問幾點開門"));

        var response = await PostAsync(body, TestHelpers.Sign(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (token, messages) = Assert.Single(_factory.Line.Replies);
        Assert.Equal("token-1", token);
        Assert.Contains("營業時間", messages[0].Text);
        Assert.NotNull(messages[0].QuickReply);
    }

    [Fact]
    public async Task RejectsMissingSignature()
    {
        var response = await PostAsync(Payload(TextEvent("t", "hi")), signature: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Line.Replies);
    }

    [Fact]
    public async Task RejectsForgedSignature()
    {
        var body = Payload(TextEvent("t", "hi"));

        var response = await PostAsync(body, TestHelpers.Sign(body, "attacker-secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Line.Replies);
    }

    [Fact]
    public async Task VerifyRequestWithNoEventsReturnsOk()
    {
        var body = """{"destination":"U-bot","events":[]}""";

        var response = await PostAsync(body, TestHelpers.Sign(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Line.Replies);
    }

    [Fact]
    public async Task HandlesFollowStickerAndIgnoredEvents()
    {
        var body = Payload(
            """{"type":"follow","replyToken":"t-follow","source":{"type":"user","userId":"U"}}""",
            """{"type":"message","replyToken":"t-sticker","message":{"type":"sticker","id":"2","packageId":"1","stickerId":"1"}}""",
            """{"type":"unfollow","source":{"type":"user","userId":"U"}}""");

        var response = await PostAsync(body, TestHelpers.Sign(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["t-follow", "t-sticker"], _factory.Line.Replies.Select(r => r.Token));
        Assert.Contains("歡迎", _factory.Line.Replies[0].Messages[0].Text);
    }

    [Fact]
    public async Task OneFailedReplyDoesNotBlockOthers()
    {
        _factory.Line.FailNext = true;
        var body = Payload(TextEvent("t-fail", "說明"), TextEvent("t-ok", "說明"));

        var response = await PostAsync(body, TestHelpers.Sign(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("t-ok", Assert.Single(_factory.Line.Replies).Token);
    }

    [Fact]
    public async Task MalformedJsonWithValidSignatureIsBadRequest()
    {
        const string body = "{not json";

        var response = await PostAsync(body, TestHelpers.Sign(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartupFailsWithClearMessageWhenSecretMissing()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("Line:ChannelSecret", "")
            .UseSetting("Line:ChannelAccessToken", TestHelpers.AccessToken));

        var ex = await Record.ExceptionAsync(() => Task.FromResult(factory.CreateClient()));

        Assert.NotNull(ex);
        Assert.Contains("Line:ChannelSecret", ex.ToString());
    }
}
