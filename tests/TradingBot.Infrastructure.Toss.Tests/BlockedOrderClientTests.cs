using TradingBot.Infrastructure.Toss;

namespace TradingBot.Infrastructure.Toss.Tests;

public class BlockedOrderClientTests
{
    [Fact]
    public void Live_submission_is_disabled()
    {
        ITossOrderClient client = new BlockedTossOrderClient();
        Assert.False(client.IsLiveSubmissionEnabled);
    }
}
