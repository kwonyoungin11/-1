using TradingBot.Infrastructure.Toss.News;

namespace TradingBot.Infrastructure.Toss.Tests;

public class NewsFeedTests
{
    [Fact]
    public async Task Feed_without_keys_tries_real_rss_or_empty_no_fake_titles()
    {
        var feed = CompositeSpaceXNewsFeed.FromEnvironment();
        var items = await feed.GetHeadlinesAsync("SPCX", 5, CancellationToken.None);
        // Real RSS may succeed in CI with network; if empty, must not be fake "모의" titles
        Assert.All(items, i => Assert.DoesNotContain("모의", i.Title, StringComparison.Ordinal));
        Assert.True(items.Count <= 5);
        Assert.False(string.IsNullOrWhiteSpace(feed.LastSourceNote));
    }

    [Fact]
    public void Material_keyword_detection()
    {
        Assert.True(CompositeSpaceXNewsFeed.LooksMaterial("SEC investigation update"));
        Assert.True(CompositeSpaceXNewsFeed.LooksMaterial("락업 해제 임박"));
        Assert.False(CompositeSpaceXNewsFeed.LooksMaterial("일반 시황 코멘트"));
    }
}
