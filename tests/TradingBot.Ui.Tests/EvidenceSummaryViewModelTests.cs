using TradingBot.Ui;

namespace TradingBot.Ui.Tests;

public class EvidenceSummaryViewModelTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(3, 7)]
    [InlineData(12, 4)]
    public void FromCounts_always_sets_LivePathBlocked_true(int dry, int paper)
    {
        var vm = EvidenceSummaryViewModel.FromCounts(dry, paper);

        Assert.True(vm.LivePathBlocked);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 5)]
    [InlineData(10, 1)]
    public void FromCounts_propagates_counts_from_arguments(int dry, int paper)
    {
        var vm = EvidenceSummaryViewModel.FromCounts(dry, paper);

        Assert.Equal(dry, vm.DryRunAcceptedCount);
        Assert.Equal(paper, vm.PaperFillCount);
    }

    [Fact]
    public void FromCounts_owner_message_is_korean_and_mentions_live_blocked()
    {
        var dry = 4;
        var paper = 2;
        var vm = EvidenceSummaryViewModel.FromCounts(dry, paper);

        Assert.False(string.IsNullOrWhiteSpace(vm.OwnerMessage));
        Assert.Contains(dry.ToString(), vm.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains(paper.ToString(), vm.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("실주문", vm.OwnerMessage, StringComparison.Ordinal);
        Assert.Contains("차단", vm.OwnerMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_LivePathBlocked_is_true_when_constructed_without_override()
    {
        // Assert default on the model itself (not a hard-coded unrelated constant).
        var vm = new EvidenceSummaryViewModel
        {
            DryRunAcceptedCount = 0,
            PaperFillCount = 0,
            OwnerMessage = "테스트",
        };

        Assert.True(vm.LivePathBlocked);
    }

    [Fact]
    public void FromCounts_does_not_allow_caller_to_open_live_path()
    {
        // Even large evidence counts stay fail-closed for live.
        var vm = EvidenceSummaryViewModel.FromCounts(dryRunAcceptedCount: 1000, paperFillCount: 1000);

        Assert.True(vm.LivePathBlocked);
        Assert.Equal(1000, vm.DryRunAcceptedCount);
        Assert.Equal(1000, vm.PaperFillCount);
        Assert.Contains("차단", vm.OwnerMessage, StringComparison.Ordinal);
    }
}
