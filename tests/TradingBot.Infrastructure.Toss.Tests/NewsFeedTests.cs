using TradingBot.Infrastructure.Toss.News;

namespace TradingBot.Infrastructure.Toss.Tests;

public class NewsFeedTests
{
    [Fact]
    public async Task Mock_feed_returns_spcx_headlines()
    {
        var feed = new CompositeSpaceXNewsFeed(http: null, finnhubApiKey: null);
        var items = await feed.GetHeadlinesAsync("SPCX", 5, CancellationToken.None);
        Assert.NotEmpty(items);
        Assert.True(items.Count <= 5);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Title)));
    }

    [Fact]
    public void Material_keyword_detection()
    {
        Assert.True(CompositeSpaceXNewsFeed.LooksMaterial("SEC investigation update"));
        Assert.True(CompositeSpaceXNewsFeed.LooksMaterial("락업 해제 임박"));
        Assert.False(CompositeSpaceXNewsFeed.LooksMaterial("일반 시황 코멘트"));
    }
}
