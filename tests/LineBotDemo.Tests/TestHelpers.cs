using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace LineBotDemo.Tests;

internal static class TestHelpers
{
    /// <summary>測試用的假密鑰，不是真的 LINE channel secret。</summary>
    public const string ChannelSecret = "test-channel-secret";
    public const string AccessToken = "test-access-token";

    public static string Sign(string body, string secret = ChannelSecret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}

/// <summary>固定值的 IOptionsMonitor，單元測試用。</summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
