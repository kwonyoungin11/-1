using TradingBot.Application;
using TradingBot.Domain;
using TradingBot.Infrastructure.Toss;
using TradingBot.Observability;
using TradingBot.Orders;
using TradingBot.Risk;

namespace TradingBot.Web.Services;

/// <summary>
/// DI for mock read-only cockpit. Live orders disabled by construction.
/// </summary>
public static class CockpitServiceRegistration
{
    /// <summary>
    /// Registers mock portfolio, safety defaults, dry-run/paper ledgers, blocked live router, cockpit harness.
    /// </summary>
    public static IServiceCollection AddTradingBotCockpit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Fail-closed safety: live orders disallowed by default, KILL_SWITCH=true, ORDER_MODE=dry_run
        services.AddSingleton(CreateWebSafetySettings());

        // Toss live HTTP stays off; mock clients only.
        services.AddSingleton(new TossOptions { AllowLiveHttp = false });
        services.AddSingleton<IReadOnlyPortfolioService>(_ => ReadOnlyPortfolioService.CreateMock());

        services.AddSingleton<OrderCandidatePipeline>();
        services.AddSingleton<LiveOrderGate>();
        services.AddSingleton<RiskGate>();

        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        services.AddSingleton<IDryRunLedger, InMemoryDryRunLedger>();
        services.AddSingleton<IPaperLedger, InMemoryPaperLedger>();
        services.AddSingleton<DryRunOrderRouter>();
        services.AddSingleton<PaperOrderRouter>();
        services.AddSingleton(sp =>
            new BlockedLiveOrderRouter(sp.GetRequiredService<TradingSafetySettings>()));

        services.AddSingleton<WebHarness>();

        return services;
    }

    public static TradingSafetySettings CreateWebSafetySettings() =>
        new()
        {
            AllowLiveOrders = false,
            KillSwitch = true,
            OrderMode = OrderMode.DryRun,
            MaxOrderNotional = 50_000m,
            MarketDataMaxStalenessSeconds = TradingSafetyDefaults.MarketDataMaxStalenessSeconds,
        };
}
