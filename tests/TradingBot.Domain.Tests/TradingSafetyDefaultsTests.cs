using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class TradingSafetyDefaultsTests
{
    [Fact]
    public void Defaults_are_fail_closed()
    {
        Assert.False(TradingSafetyDefaults.AllowLiveOrders);
        Assert.True(TradingSafetyDefaults.KillSwitch);
        Assert.Equal(OrderMode.DryRun, TradingSafetyDefaults.OrderMode);
        Assert.True(TradingSafetyDefaults.MarketDataMaxStalenessSeconds > 0);
        Assert.Equal(5, TradingSafetyDefaults.MarketDataMaxStalenessSeconds);
    }

    [Fact]
    public void Live_orders_remain_blocked_by_default_constants()
    {
        // Invariant: hard-coded defaults must never open a live path.
        Assert.False(TradingSafetyDefaults.AllowLiveOrders);
        Assert.True(TradingSafetyDefaults.KillSwitch);
        Assert.NotEqual(OrderMode.Live, TradingSafetyDefaults.OrderMode);
        Assert.Equal(OrderMode.DryRun, TradingSafetyDefaults.OrderMode);
    }

    [Fact]
    public void CreateSafeDefaults_matches_hard_defaults()
    {
        var settings = TradingSafetySettings.CreateSafeDefaults();
        Assert.False(settings.AllowLiveOrders);
        Assert.True(settings.KillSwitch);
        Assert.Equal(OrderMode.DryRun, settings.OrderMode);
        Assert.Equal(
            TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
            settings.MarketDataMaxStalenessSeconds);
        Assert.NotEqual(OrderMode.Live, settings.OrderMode);
    }

    [Fact]
    public void New_TradingSafetySettings_uses_fail_closed_property_defaults()
    {
        var settings = new TradingSafetySettings();
        Assert.False(settings.AllowLiveOrders);
        Assert.True(settings.KillSwitch);
        Assert.Equal(OrderMode.DryRun, settings.OrderMode);
        Assert.Equal(
            TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
            settings.MarketDataMaxStalenessSeconds);
    }

    [Fact]
    public void SecretRedactor_masks_account_and_token()
    {
        Assert.Contains("****", SecretRedactor.MaskAccount("1234567890"), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-value", SecretRedactor.MaskToken("secret-token-value"), StringComparison.Ordinal);
    }
}
