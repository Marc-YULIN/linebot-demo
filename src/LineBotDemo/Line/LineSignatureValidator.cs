using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LineBotDemo.Line;

/// <summary>
/// 驗證 X-Line-Signature：以 channel secret 對「原始請求內容」做 HMAC-SHA256，再 Base64。
/// 確認請求真的來自 LINE，避免有人偽造訊息打你的 webhook。
/// </summary>
public sealed class LineSignatureValidator(IOptions<LineOptions> options)
{
    private const int HashSize = 32; // SHA-256

    public bool IsValid(ReadOnlySpan<byte> body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;

        Span<byte> provided = stackalloc byte[HashSize];
        if (!Convert.TryFromBase64String(signature, provided, out var written) || written != HashSize)
            return false;

        Span<byte> expected = stackalloc byte[HashSize];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.ChannelSecret), body, expected);

        // 固定時間比較，避免時序攻擊
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
