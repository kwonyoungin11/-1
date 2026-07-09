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
    }

    [Fact]
    public void CreateSafeDefaults_matches_hard_defaults()
    {
        var settings = TradingSafetySettings.CreateSafeDefaults();
        Assert.False(settings.AllowLiveOrders);
        Assert.True(settings.KillSwitch);
        Assert.Equal(OrderMode.DryRun, settings.OrderMode);
    }

    [Fact]
    public void SecretRedactor_masks_account_and_token()
    {
        Assert.Contains("****", SecretRedactor.MaskAccount("1234567890"), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-value", SecretRedactor.MaskToken("secret-token-value"), StringComparison.Ordinal);
    }
}
