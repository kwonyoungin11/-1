using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class LiveReadinessEvaluatorTests
{
    [Fact]
    public void Evaluate_with_empty_root_is_blocked_missing_evidence_and_live_not_ready()
    {
        using var tmp = TempRoot.Create();

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.BlockedMissingEvidence, result.Status);
        Assert.False(result.LiveReady);
        Assert.True(result.SafetyIntact);
        Assert.Contains(result.MissingArtifacts, m => m.Contains("artifacts/live-readiness", StringComparison.Ordinal));
        Assert.Equal(
            "blocked_missing_evidence",
            LiveReadinessEvaluator.ToOwnerUnlockStatusToken(result.Status));
    }

    [Fact]
    public void Evaluate_with_empty_artifact_dir_lists_required_files()
    {
        using var tmp = TempRoot.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, LiveReadinessEvaluator.RelativeArtifactDirectory));

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.BlockedMissingEvidence, result.Status);
        Assert.False(result.LiveReady);
        Assert.True(result.SafetyIntact);
        Assert.Contains(
            result.MissingArtifacts,
            m => m.Contains("paper-multi-session-export", StringComparison.Ordinal));
        Assert.Contains(LiveReadinessEvaluator.IncidentDrillRecord, result.MissingArtifacts);
        Assert.Contains(LiveReadinessEvaluator.OpenApiRecheckLog, result.MissingArtifacts);
        Assert.Contains(LiveReadinessEvaluator.OwnerUnlockSignoff, result.MissingArtifacts);
        Assert.Contains(
            result.Notes,
            n => n.Contains("optional missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_with_all_required_artifacts_is_ready_for_owner_unlock_but_live_still_false()
    {
        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: false, includeOptionalSmoke: false);

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.ReadyForOwnerUnlock, result.Status);
        Assert.False(result.LiveReady);
        Assert.True(result.SafetyIntact);
        Assert.Empty(result.MissingArtifacts);
        Assert.Contains(LiveReadinessEvaluator.PaperExportTxt, result.PresentArtifacts);
        Assert.Contains(LiveReadinessEvaluator.IncidentDrillRecord, result.PresentArtifacts);
        Assert.Contains(LiveReadinessEvaluator.OpenApiRecheckLog, result.PresentArtifacts);
        Assert.Contains(LiveReadinessEvaluator.OwnerUnlockSignoff, result.PresentArtifacts);
        Assert.Equal(
            "ready_for_owner_unlock",
            LiveReadinessEvaluator.ToOwnerUnlockStatusToken(result.Status));
    }

    [Fact]
    public void Evaluate_accepts_paper_export_json_instead_of_txt()
    {
        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: true, includeOptionalSmoke: false);

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.ReadyForOwnerUnlock, result.Status);
        Assert.Contains(LiveReadinessEvaluator.PaperExportJson, result.PresentArtifacts);
        Assert.DoesNotContain(LiveReadinessEvaluator.PaperExportTxt, result.PresentArtifacts);
    }

    [Fact]
    public void Evaluate_incident_without_iso_date_is_blocked()
    {
        using var tmp = TempRoot.Create();
        var dir = Path.Combine(tmp.Path, LiveReadinessEvaluator.RelativeArtifactDirectory);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, LiveReadinessEvaluator.PaperExportTxt), "session export\n");
        File.WriteAllText(
            Path.Combine(dir, LiveReadinessEvaluator.IncidentDrillRecord),
            "# Incident drill\nNo calendar date here.\n");
        File.WriteAllText(Path.Combine(dir, LiveReadinessEvaluator.OpenApiRecheckLog), "ok\n");
        File.WriteAllText(Path.Combine(dir, LiveReadinessEvaluator.OwnerUnlockSignoff), "template\n");

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.BlockedMissingEvidence, result.Status);
        Assert.Contains(
            result.MissingArtifacts,
            m => m.Contains(LiveReadinessEvaluator.IncidentDrillRecord, StringComparison.Ordinal));
        Assert.Contains(
            result.Notes,
            n => n.Contains("missing YYYY-MM-DD", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_optional_smoke_present_is_recorded_but_not_required()
    {
        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: false, includeOptionalSmoke: true);

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.ReadyForOwnerUnlock, result.Status);
        Assert.Contains(LiveReadinessEvaluator.TossReadSmokeRedactedLog, result.PresentArtifacts);
        Assert.DoesNotContain(
            result.Notes,
            n => n.Contains("optional missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_optional_smoke_residual_file_counts_as_optional_present()
    {
        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: false, includeOptionalSmoke: false);
        var dir = Path.Combine(tmp.Path, LiveReadinessEvaluator.RelativeArtifactDirectory);
        File.WriteAllText(
            Path.Combine(dir, LiveReadinessEvaluator.TossReadSmokeResidual),
            "residual note only\n");

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.ReadyForOwnerUnlock, result.Status);
        Assert.Contains(LiveReadinessEvaluator.TossReadSmokeResidual, result.PresentArtifacts);
    }

    [Fact]
    public void Evaluate_partial_artifacts_remain_blocked_and_live_false()
    {
        using var tmp = TempRoot.Create();
        var dir = Path.Combine(tmp.Path, LiveReadinessEvaluator.RelativeArtifactDirectory);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, LiveReadinessEvaluator.OwnerUnlockSignoff), "# signoff template\n");
        File.WriteAllText(Path.Combine(dir, LiveReadinessEvaluator.OpenApiRecheckLog), "recheck\n");

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);

        Assert.Equal(LiveReadinessStatus.BlockedMissingEvidence, result.Status);
        Assert.False(result.LiveReady);
        Assert.True(result.SafetyIntact);
        Assert.NotEmpty(result.MissingArtifacts);
        Assert.Contains(LiveReadinessEvaluator.OwnerUnlockSignoff, result.PresentArtifacts);
    }

    [Fact]
    public void Safety_defaults_remain_fail_closed_so_evaluator_never_opens_live()
    {
        // Invariant under shipped constants: LiveReady is always false when safety intact.
        Assert.False(TradingSafetyDefaults.AllowLiveOrders);
        Assert.True(TradingSafetyDefaults.KillSwitch);
        Assert.Equal(OrderMode.DryRun, TradingSafetyDefaults.OrderMode);

        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: false, includeOptionalSmoke: true);

        var result = LiveReadinessEvaluator.Evaluate(tmp.Path);
        Assert.False(result.LiveReady);
        Assert.NotEqual(LiveReadinessStatus.BrokenDefaults, result.Status);
    }

    [Fact]
    public void ToOwnerUnlockStatusToken_covers_all_statuses()
    {
        Assert.Equal(
            "blocked_missing_evidence",
            LiveReadinessEvaluator.ToOwnerUnlockStatusToken(LiveReadinessStatus.BlockedMissingEvidence));
        Assert.Equal(
            "ready_for_owner_unlock",
            LiveReadinessEvaluator.ToOwnerUnlockStatusToken(LiveReadinessStatus.ReadyForOwnerUnlock));
        Assert.Equal(
            "broken_defaults",
            LiveReadinessEvaluator.ToOwnerUnlockStatusToken(LiveReadinessStatus.BrokenDefaults));
    }

    [Theory]
    [InlineData(true, true, OrderMode.DryRun)]
    [InlineData(false, false, OrderMode.DryRun)]
    [InlineData(false, true, OrderMode.Live)]
    [InlineData(false, true, OrderMode.Paper)]
    public void Evaluate_with_weakened_safety_snapshot_is_broken_defaults(
        bool allowLive,
        bool killSwitch,
        OrderMode mode)
    {
        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: false, includeOptionalSmoke: true);

        var broken = new LiveReadinessEvaluator.SafetySnapshot(allowLive, killSwitch, mode);
        var result = LiveReadinessEvaluator.Evaluate(tmp.Path, broken);

        Assert.Equal(LiveReadinessStatus.BrokenDefaults, result.Status);
        Assert.False(result.LiveReady);
        Assert.False(result.SafetyIntact);
        Assert.Equal(
            "broken_defaults",
            LiveReadinessEvaluator.ToOwnerUnlockStatusToken(result.Status));
        Assert.NotEmpty(result.Notes);
    }

    [Fact]
    public void Evaluate_with_explicit_safe_snapshot_and_full_artifacts_is_ready()
    {
        using var tmp = TempRoot.Create();
        WriteFullRequiredSet(tmp.Path, paperAsJson: false, includeOptionalSmoke: false);

        var safe = LiveReadinessEvaluator.SafetySnapshot.FromShippedDefaults();
        var result = LiveReadinessEvaluator.Evaluate(tmp.Path, safe);

        Assert.Equal(LiveReadinessStatus.ReadyForOwnerUnlock, result.Status);
        Assert.False(result.LiveReady);
        Assert.True(result.SafetyIntact);
    }

    private static void WriteFullRequiredSet(string root, bool paperAsJson, bool includeOptionalSmoke)
    {
        var dir = Path.Combine(root, LiveReadinessEvaluator.RelativeArtifactDirectory);
        Directory.CreateDirectory(dir);

        if (paperAsJson)
        {
            File.WriteAllText(
                Path.Combine(dir, LiveReadinessEvaluator.PaperExportJson),
                """{"sessions":3,"note":"fixture"}""" + "\n");
        }
        else
        {
            File.WriteAllText(
                Path.Combine(dir, LiveReadinessEvaluator.PaperExportTxt),
                "paper multi-session export fixture\n");
        }

        File.WriteAllText(
            Path.Combine(dir, LiveReadinessEvaluator.IncidentDrillRecord),
            "# Incident drill record\nDate: 2026-07-09\nKill switch exercised.\n");
        File.WriteAllText(
            Path.Combine(dir, LiveReadinessEvaluator.OpenApiRecheckLog),
            "openapi recheck 2026-07-09 ok\n");
        File.WriteAllText(
            Path.Combine(dir, LiveReadinessEvaluator.OwnerUnlockSignoff),
            "# Owner unlock sign-off (template)\n- Owner: ___\n- Date: ___\n");

        if (includeOptionalSmoke)
        {
            File.WriteAllText(
                Path.Combine(dir, LiveReadinessEvaluator.TossReadSmokeRedactedLog),
                "redacted smoke log\n");
        }
    }

    private sealed class TempRoot : IDisposable
    {
        public string Path { get; }

        private TempRoot(string path) => Path = path;

        public static TempRoot Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "tb-live-ready-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempRoot(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup for temp fixtures
            }
        }
    }
}
