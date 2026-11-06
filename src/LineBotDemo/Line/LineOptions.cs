using System.ComponentModel.DataAnnotations;

namespace LineBotDemo.Line;

/// <summary>
/// LINE Messaging API 的密鑰。不寫在 appsettings.json，請用：
/// <list type="bullet">
/// <item>開發：dotnet user-secrets set "Line:ChannelSecret" "..."</item>
/// <item>正式：環境變數 Line__ChannelSecret、Line__ChannelAccessToken</item>
/// </list>
/// </summary>
public sealed class LineOptions
{
    public const string Section = "Line";

    [Required(ErrorMessage = "缺少 Line:ChannelSecret，請用 user-secrets 或環境變數 Line__ChannelSecret 設定")]
    public string ChannelSecret { get; set; } = "";

    [Required(ErrorMessage = "缺少 Line:ChannelAccessToken，請用 user-secrets 或環境變數 Line__ChannelAccessToken 設定")]
    public string ChannelAccessToken { get; set; } = "";
}
