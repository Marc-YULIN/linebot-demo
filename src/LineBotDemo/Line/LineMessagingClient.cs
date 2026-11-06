using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace LineBotDemo.Line;

public interface ILineMessagingClient
{
    Task ReplyAsync(string replyToken, IReadOnlyList<TextMessage> messages, CancellationToken ct = default);
}

/// <summary>呼叫 LINE Reply API（https://api.line.me/v2/bot/message/reply）。</summary>
public sealed class LineMessagingClient(HttpClient http, IOptions<LineOptions> options) : ILineMessagingClient
{
    public const string BaseAddress = "https://api.line.me/";

    public async Task ReplyAsync(string replyToken, IReadOnlyList<TextMessage> messages, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/bot/message/reply")
        {
            Content = JsonContent.Create(new ReplyRequest(replyToken, messages), options: LineJson.Options),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ChannelAccessToken);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // LINE 回傳的錯誤內容只有原因說明，不含密鑰，可以記錄
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"LINE Reply API 失敗：{(int)response.StatusCode} {detail}", null, response.StatusCode);
        }
    }
}
