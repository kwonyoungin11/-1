using TradingBot.App.Services;
using TradingBot.Ui;

namespace TradingBot.App.Tests;

public class AppHarnessTests
{
    [Fact]
    public async Task GetDashboardAsync_keeps_live_locked_from_real_app_harness()
    {
        var harness = AppHarness.CreateDefault();
        var dash = await harness.GetDashboardAsync();

        Assert.Equal(LiveLockState.Locked, dash.Snapshot.LiveLock);
        Assert.False(dash.Snapshot.AllowLiveOrders);
        Assert.True(dash.Snapshot.KillSwitchActive);
        Assert.False(dash.IsLiveTradingVisuallyOpen);
        Assert.Contains("실거래", dash.Snapshot.SafetyHeadline, StringComparison.Ordinal);

        var evidence = harness.GetEvidenceCounts();
        Assert.True(evidence.LiveBlocked);
        Assert.True(evidence.DryRun >= 0);
        Assert.True(evidence.Paper >= 0);
    }

    [Fact]
    public void Project_is_desktop_winexe()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "TradingBot.App", "TradingBot.App.csproj");
        var text = File.ReadAllText(path);
        Assert.Contains("<OutputType>WinExe</OutputType>", text, StringComparison.Ordinal);
        Assert.Contains("Avalonia.Desktop", text, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TradingBot.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("sln not found");
    }
}
