using LineBotDemo.Bot;
using LineBotDemo.Line;

namespace LineBotDemo.Tests;

public class ReplyEngineTests
{
    private static BotContent Content() => new()
    {
        ShopName = "測試店",
        Welcome = "歡迎",
        Fallback = "看不懂",
        Unsupported = "只看得懂文字",
        Faq =
        [
            new() { Title = "營業時間", Keywords = ["幾點", "開門", "公休"], Answer = "9 點到 6 點" },
            new() { Title = "停車資訊", Keywords = ["停車", "開車"], Answer = "門口可停機車" },
            new() { Title = "寵物友善", Keywords = ["狗", "貓"], Answer = "歡迎毛小孩" },
            new() { Title = "", Keywords = ["壞掉"], Answer = "缺主題，應被略過" },
        ],
    };

    private static ReplyEngine Engine(BotContent? content = null) =>
        new(new StaticOptionsMonitor<BotContent>(content ?? Content()));

    [Theory]
    [InlineData("請問幾點開門？", "營業時間")]
    [InlineData("可以帶狗嗎", "寵物友善")]
    [InlineData("營業時間", "營業時間")]   // 點快速回覆按鈕送來的主題
    [InlineData("  停車資訊 ", "停車資訊")]
    [InlineData("開車去方便停車嗎", "停車資訊")]
    public void MatchesFaq(string input, string expectedTitle)
    {
        var reply = Engine().Reply(input);
        Assert.Equal(ReplyKind.Faq, reply.Kind);
        Assert.Equal(expectedTitle, reply.FaqTitle);
    }

    [Fact]
    public void PrefersEntryWithMoreKeywordHits()
    {
        // 「開車」命中停車（2 字），「幾點」「開門」命中營業時間（4 字）
        var reply = Engine().Reply("開車過去幾點開門");
        Assert.Equal("營業時間", reply.FaqTitle);
    }

    [Theory]
    [InlineData("說明")]
    [InlineData("HELP")]
    [InlineData("？")]
    public void HelpListsAllValidTopics(string input)
    {
        var reply = Engine().Reply(input);
        Assert.Equal(ReplyKind.Help, reply.Kind);
        var text = Assert.Single(reply.Messages).Text;
        Assert.Contains("・營業時間", text);
        Assert.Contains("・寵物友善", text);
        Assert.DoesNotContain("壞掉", text);
    }

    [Fact]
    public void GreetingReturnsWelcome()
    {
        var reply = Engine().Reply("Hi");
        Assert.Equal(ReplyKind.Greeting, reply.Kind);
        Assert.Equal("歡迎", reply.Messages[0].Text);
    }

    [Fact]
    public void FallsBackWhenNothingMatches()
    {
        var reply = Engine().Reply("你們老闆是誰");
        Assert.Equal(ReplyKind.Fallback, reply.Kind);
        Assert.Equal("看不懂", reply.Messages[0].Text);
        Assert.Null(reply.FaqTitle);
    }

    [Fact]
    public void EveryReplyHasQuickReplyButtonsForValidTopics()
    {
        var quick = Engine().Reply("隨便").Messages[0].QuickReply;
        Assert.NotNull(quick);
        Assert.Equal(["營業時間", "停車資訊", "寵物友善"], quick.Items.Select(i => i.Action.Text));
    }

    [Fact]
    public void QuickReplyRespectsLineLimits()
    {
        var content = Content();
        content.Faq = Enumerable.Range(1, 20)
            .Select(i => new FaqEntry { Title = $"第{i}題－這是一個非常非常非常非常長的主題名稱", Answer = "答" })
            .ToList();

        var items = Engine(content).Welcome().Messages[0].QuickReply!.Items;

        Assert.Equal(QuickReply.MaxItems, items.Count);
        Assert.All(items, i => Assert.True(i.Action.Label.Length <= MessageAction.MaxLabelLength));
        // 按鈕送出的文字仍是完整主題，才能精準命中
        Assert.Equal(content.Faq[0].Title, items[0].Action.Text);
    }

    [Fact]
    public void TruncatesOverlongAnswer()
    {
        var content = Content();
        content.Faq[0].Answer = new string('長', 6000);
        Assert.Equal(5000, Engine(content).Reply("營業時間").Messages[0].Text.Length);
    }

    [Fact]
    public void PicksUpChangedContentWithoutRestart()
    {
        var monitor = new StaticOptionsMonitor<BotContent>(Content());
        var engine = new ReplyEngine(monitor);
        Assert.Equal(ReplyKind.Fallback, engine.Reply("有素食嗎").Kind);

        var updated = Content();
        updated.Faq.Add(new() { Title = "素食", Keywords = ["素食", "吃素"], Answer = "有蛋奶素" });
        monitor.CurrentValue = updated;

        Assert.Equal("素食", engine.Reply("有素食嗎").FaqTitle);
    }
}
