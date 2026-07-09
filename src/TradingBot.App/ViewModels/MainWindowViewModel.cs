using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingBot.App.Services;
using TradingBot.Ui;

namespace TradingBot.App.ViewModels;

/// <summary>Desktop cockpit main window — owner-facing, no live order actions.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppHarness _harness;

    public MainWindowViewModel()
        : this(AppHarness.CreateDefault())
    {
    }

    public MainWindowViewModel(AppHarness harness)
    {
        _harness = harness ?? throw new ArgumentNullException(nameof(harness));
        var safe = CockpitDashboardModel.CreateSafeDefault();
        ApplyDashboard(safe);
    }

    [ObservableProperty]
    private string _title = "TradingBot 콕핏 (Mac 앱)";

    [ObservableProperty]
    private string _safetyHeadline = string.Empty;

    [ObservableProperty]
    private string _botStateMessage = string.Empty;

    [ObservableProperty]
    private string _connectionSummary = string.Empty;

    [ObservableProperty]
    private string _marketSummary = string.Empty;

    [ObservableProperty]
    private string _accountSummary = string.Empty;

    [ObservableProperty]
    private string _nextAction = string.Empty;

    [ObservableProperty]
    private string _liveLockLabel = "잠김";

    [ObservableProperty]
    private string _killSwitchLabel = "ON";

    [ObservableProperty]
    private string _orderModeLabel = "dry_run";

    [ObservableProperty]
    private string _statusLine = "대기";

    [ObservableProperty]
    private int _dryRunCount;

    [ObservableProperty]
    private int _paperFillCount;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<RiskGateRowViewModel> RiskGates { get; } = new();

    public ObservableCollection<OrderCandidateRowViewModel> Candidates { get; } = new();

    public ObservableCollection<string> AuditLines { get; } = new();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusLine = "새로고침 중…";
            var dash = await _harness.GetDashboardAsync().ConfigureAwait(true);
            ApplyDashboard(dash);
            var evidence = _harness.GetEvidenceCounts();
            DryRunCount = evidence.DryRun;
            PaperFillCount = evidence.Paper;
            StatusLine = "갱신 완료 — 실주문 없음";
        }
        catch (Exception ex)
        {
            StatusLine = $"오류 (실주문 없음): {ex.GetType().Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyDashboard(CockpitDashboardModel dash)
    {
        var s = dash.Snapshot;
        SafetyHeadline = s.SafetyHeadline;
        BotStateMessage = s.BotStateOwnerMessage;
        ConnectionSummary = s.ConnectionSummary;
        MarketSummary = s.MarketSessionSummary;
        AccountSummary = s.AccountMaskedSummary;
        NextAction = s.NextActionOwnerMessage;
        LiveLockLabel = s.LiveLock.ToString();
        KillSwitchLabel = s.KillSwitchActive ? "ON" : "OFF";
        OrderModeLabel = s.OrderMode.ToString();

        RiskGates.Clear();
        foreach (var r in dash.RiskGates)
        {
            RiskGates.Add(r);
        }

        Candidates.Clear();
        foreach (var c in dash.OrderCandidates)
        {
            Candidates.Add(c);
        }

        AuditLines.Clear();
        foreach (var line in s.RecentAuditLines)
        {
            AuditLines.Add(line);
        }
    }
}
