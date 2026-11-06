using System.Text;
using LineBotDemo.Line;
using Microsoft.Extensions.Options;

namespace LineBotDemo.Tests;

public class LineSignatureValidatorTests
{
    private static readonly LineSignatureValidator Validator = new(Options.Create(new LineOptions
    {
        ChannelSecret = TestHelpers.ChannelSecret,
        ChannelAccessToken = TestHelpers.AccessToken,
    }));

    private const string Body = """{"destination":"U123","events":[]}""";

    // 用 PowerShell 的 HMACSHA256 另外算出來的已知值，避免測試只是複製實作
    private const string KnownSignature = "RLxch9319JCgPZGVmTt6ln/cqjxfKIvaZ9mOI5anJnM=";

    [Fact]
    public void AcceptsKnownSignature() =>
        Assert.True(Validator.IsValid(Encoding.UTF8.GetBytes(Body), KnownSignature));

    [Fact]
    public void AcceptsSignatureOfUnicodeBody()
    {
        const string body = """{"events":[{"type":"message","message":{"type":"text","text":"幾點開門？"}}]}""";
        Assert.True(Validator.IsValid(Encoding.UTF8.GetBytes(body), TestHelpers.Sign(body)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!")]
    [InlineData("c2hvcnQ=")] // 合法 Base64 但長度不對
    public void RejectsMissingOrMalformedSignature(string? signature) =>
        Assert.False(Validator.IsValid(Encoding.UTF8.GetBytes(Body), signature));

    [Fact]
    public void RejectsTamperedBody() =>
        Assert.False(Validator.IsValid(Encoding.UTF8.GetBytes(Body.Replace("U123", "U999")), KnownSignature));

    [Fact]
    public void RejectsSignatureFromAnotherSecret() =>
        Assert.False(Validator.IsValid(Encoding.UTF8.GetBytes(Body), TestHelpers.Sign(Body, "other-secret")));
}
