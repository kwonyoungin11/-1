using TradingBot.Infrastructure.Toss;

namespace TradingBot.Infrastructure.Toss.Tests;

/// <summary>
/// EnvFile parsing with synthetic strings only — never real secrets, never log values.
/// </summary>
public class EnvFileTests
{
    [Fact]
    public void ParseLines_skips_blank_comments_and_invalid()
    {
        var map = EnvFile.ParseLines(
        [
            "",
            "   ",
            "# comment only",
            "  # indented comment",
            "NO_EQUALS_SIGN",
            "TOSS_ALLOW_LIVE_HTTP=false",
            "SYNTH_CLIENT_ID=synthetic-client-id-not-real",
            "SYNTH_CLIENT_SECRET=synthetic-secret-not-real",
        ]);

        Assert.Equal(3, map.Count);
        Assert.Equal("false", map["TOSS_ALLOW_LIVE_HTTP"]);
        Assert.Equal("synthetic-client-id-not-real", map["SYNTH_CLIENT_ID"]);
        Assert.Equal("synthetic-secret-not-real", map["SYNTH_CLIENT_SECRET"]);
        Assert.False(map.ContainsKey("NO_EQUALS_SIGN"));
    }

    [Fact]
    public void ParseLines_strips_matching_single_and_double_quotes()
    {
        var map = EnvFile.ParseLines(
        [
            "DOUBLE=\"quoted-value\"",
            "SINGLE='quoted-value'",
            "MIXED=\"still-ends-wrong'",
            "EMPTY_QUOTES=\"\"",
            "SPACES_AROUND =  spaced  ",
        ]);

        Assert.Equal("quoted-value", map["DOUBLE"]);
        Assert.Equal("quoted-value", map["SINGLE"]);
        // Mismatched quotes are left as-is after trim
        Assert.Equal("\"still-ends-wrong'", map["MIXED"]);
        Assert.Equal(string.Empty, map["EMPTY_QUOTES"]);
        Assert.Equal("spaced", map["SPACES_AROUND"]);
    }

    [Fact]
    public void ParseLines_last_duplicate_key_wins()
    {
        var map = EnvFile.ParseLines(
        [
            "KEY=first",
            "KEY=second",
        ]);

        Assert.Equal("second", map["KEY"]);
    }

    [Fact]
    public void ParseLines_ignores_empty_key()
    {
        var map = EnvFile.ParseLines(["=value-only", "  =also-empty-key"]);
        Assert.Empty(map);
    }

    [Fact]
    public void ParseFile_reads_synthetic_temp_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "tradingbot-envfile-" + Guid.NewGuid().ToString("N") + ".env");
        try
        {
            File.WriteAllText(
                path,
                """
                # synthetic fixture — not real credentials
                TOSS_ALLOW_LIVE_HTTP=false
                TOSS_CLIENT_ID=synth-id-000
                TOSS_CLIENT_SECRET=synth-secret-000
                """);

            var map = EnvFile.ParseFile(path);
            Assert.Equal("false", map["TOSS_ALLOW_LIVE_HTTP"]);
            Assert.Equal("synth-id-000", map["TOSS_CLIENT_ID"]);
            Assert.Equal("synth-secret-000", map["TOSS_CLIENT_SECRET"]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadMergedWithProcess_process_env_wins_over_file()
    {
        var uniqueKey = "TB_SYNTH_ENV_" + Guid.NewGuid().ToString("N");
        var repoRoot = Path.Combine(Path.GetTempPath(), "tb-env-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        try
        {
            File.WriteAllText(Path.Combine(repoRoot, ".env"), $"{uniqueKey}=from_file\n");
            Environment.SetEnvironmentVariable(uniqueKey, "from_process");

            var map = EnvFile.LoadMergedWithProcess(repoRoot);
            Assert.Equal("from_process", map[uniqueKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueKey, null);
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadMergedWithProcess_uses_file_when_process_unset()
    {
        var uniqueKey = "TB_SYNTH_FILE_ONLY_" + Guid.NewGuid().ToString("N");
        var repoRoot = Path.Combine(Path.GetTempPath(), "tb-env-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        try
        {
            // Ensure process does not define the key
            Environment.SetEnvironmentVariable(uniqueKey, null);
            File.WriteAllText(Path.Combine(repoRoot, ".env"), $"{uniqueKey}=from_file_only\n");

            var map = EnvFile.LoadMergedWithProcess(repoRoot);
            Assert.Equal("from_file_only", map[uniqueKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueKey, null);
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ParseLines_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => EnvFile.ParseLines(null!));
    }
}
