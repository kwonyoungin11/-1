using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingBot.App.Services;
using TradingBot.Domain;
using TradingBot.Ui;

namespace TradingBot.App.ViewModels;

/// <summary>Mac 데스크톱 자동매매 콕핏 — 한국어 전용, 실주문 조작 없음.</summary>
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
        ApplyDashboard(CockpitDashboardModel.CreateSafeDefault());
    }

    [ObservableProperty] private string _title = "자동매매 콕핏";
    [ObservableProperty] private string _safetyHeadline = string.Empty;
    [ObservableProperty] private string _botStateMessage = string.Empty;
    [ObservableProperty] private string _connectionSummary = string.Empty;
    [ObservableProperty] private string _marketSummary = string.Empty;
    [ObservableProperty] private string _accountSummary = string.Empty;
    [ObservableProperty] private string _nextAction = string.Empty;
    [ObservableProperty] private string _liveLockLabel = "잠김";
    [ObservableProperty] private string _killSwitchLabel = "켜짐";
    [ObservableProperty] private string _orderModeLabel = "연습";
    [ObservableProperty] private string _statusLine = "대기";
    [ObservableProperty] private string _dryRunLabel = "모의 기록 0건";
    [ObservableProperty] private string _paperLabel = "가상 체결 0건";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<string> RiskLines { get; } = new();
    public ObservableCollection<string> CandidateLines { get; } = new();
    public ObservableCollection<string> BlockLines { get; } = new();

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
            StatusLine = "불러오는 중…";
            var dash = await _harness.GetDashboardAsync().ConfigureAwait(true);
            ApplyDashboard(dash);
            var evidence = _harness.GetEvidenceCounts();
            DryRunLabel = $"모의 기록 {evidence.DryRun}건";
            PaperLabel = $"가상 체결 {evidence.Paper}건";
            StatusLine = "갱신 완료 · 실주문 없음";
        }
        catch (Exception)
        {
            StatusLine = "오류 · 실주문은 하지 않습니다";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyDashboard(CockpitDashboardModel dash)
    {
        var s = dash.Snapshot;
        SafetyHeadline = ToKoreanSafety(s.SafetyHeadline);
        BotStateMessage = s.BotStateOwnerMessage;
        ConnectionSummary = s.ConnectionSummary;
        MarketSummary = s.MarketSessionSummary;
        AccountSummary = s.AccountMaskedSummary;
        NextAction = s.NextActionOwnerMessage;
        LiveLockLabel = s.LiveLock switch
        {
            LiveLockState.Locked => "잠김",
            LiveLockState.UnlockPendingApproval => "승인 대기",
            LiveLockState.Unlocked => "열림(주의)",
            _ => "알 수 없음",
        };
        KillSwitchLabel = s.KillSwitchActive ? "켜짐" : "꺼짐";
        OrderModeLabel = s.OrderMode switch
        {
            OrderMode.DryRun => "연습",
            OrderMode.Paper => "가상매매",
            OrderMode.Live => "실거래(주의)",
            _ => "알 수 없음",
        };

        RiskLines.Clear();
        foreach (var r in dash.RiskGates)
        {
            var mark = r.Passed ? "통과" : "차단";
            RiskLines.Add($"[{mark}] {r.Title} — {r.OwnerMessage}");
        }

        CandidateLines.Clear();
        if (dash.OrderCandidates.Count == 0)
        {
            CandidateLines.Add("현재 후보 없음 · 실주문 아님");
        }
        else
        {
            foreach (var c in dash.OrderCandidates)
            {
                var side = c.Side is "BUY" or "Buy" ? "매수후보" : c.Side is "SELL" or "Sell" ? "매도후보" : c.Side;
                CandidateLines.Add($"{c.Symbol} · {side} · 수량 {c.Quantity} · {c.Status}");
            }
        }

        BlockLines.Clear();
        foreach (var b in s.RecentBlockMessages.Take(5))
        {
            BlockLines.Add(b);
        }
    }

    private static string ToKoreanSafety(string headline)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            return "실거래 잠김 · 실제 주문은 나가지 않습니다";
        }

        return headline
            .Replace("Live orders blocked", "실거래 잠김", StringComparison.OrdinalIgnoreCase)
            .Replace("dry_run", "연습", StringComparison.OrdinalIgnoreCase);
    }
}
