namespace TradingBot.Web.Tests;

public class MvpRoutePresenceTests
{
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

        throw new InvalidOperationException("TradingBot.sln not found from test base.");
    }

    public static TheoryData<string, string> Routes => new()
    {
        { "Home.razor", "/" },
        { "BotState.razor", "/bot-state" },
        { "LiveLock.razor", "/live-lock" },
        { "Risk.razor", "/risk" },
        { "Candidates.razor", "/candidates" },
        { "Account.razor", "/account" },
        { "Audit.razor", "/audit" },
        { "Settings.razor", "/settings" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void Mvp_page_declares_expected_route_and_is_not_stub(string fileName, string route)
    {
        var path = Path.Combine(FindRepoRoot(), "src", "TradingBot.Web", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), $"missing shipped page {path}");
        var text = File.ReadAllText(path);
        Assert.Contains($"@page \"{route}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("준비 중", text, StringComparison.Ordinal);
    }
}
