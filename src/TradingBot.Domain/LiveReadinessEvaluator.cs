using System.Globalization;
using System.Text.RegularExpressions;

namespace TradingBot.Domain;

/// <summary>
/// Evaluates live-readiness ops artifacts under a repository root.
/// Defaults never auto-enable live: <see cref="LiveReadinessEvaluation.LiveReady"/> is always false
/// when fail-closed defaults hold. Distinguishes missing evidence vs ready-for-owner-unlock.
/// </summary>
public static class LiveReadinessEvaluator
{
    public const string RelativeArtifactDirectory = "artifacts/live-readiness";

    public const string PaperExportTxt = "paper-multi-session-export.txt";
    public const string PaperExportJson = "paper-multi-session-export.json";
    public const string IncidentDrillRecord = "incident-drill-record.md";
    public const string OpenApiRecheckLog = "openapi-recheck.log";
    public const string OwnerUnlockSignoff = "owner-unlock-signoff.md";
    public const string TossReadSmokeRedactedLog = "toss-read-smoke-redacted.log";
    public const string TossReadSmokeResidual = "toss-read-smoke-residual.md";

    private static readonly Regex IsoDatePattern = new(
        @"\b(20\d{2})-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Inspects <paramref name="rootDirectory"/> using shipped
    /// <see cref="TradingSafetyDefaults"/> and required files under <c>artifacts/live-readiness/</c>.
    /// </summary>
    public static LiveReadinessEvaluation Evaluate(string rootDirectory) =>
        Evaluate(rootDirectory, SafetySnapshot.FromShippedDefaults());

    /// <summary>
    /// Inspects artifacts and an explicit safety snapshot (for tests / future runtime wiring).
    /// <see cref="LiveReadinessEvaluation.LiveReady"/> is never set true by this method.
    /// </summary>
    public static LiveReadinessEvaluation Evaluate(string rootDirectory, SafetySnapshot safety)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(safety);

        var root = Path.GetFullPath(rootDirectory);
        var artifactDir = Path.Combine(root, RelativeArtifactDirectory);
        var missing = new List<string>();
        var present = new List<string>();
        var notes = new List<string>();

        if (!AreSafetyDefaultsFailClosed(safety, notes))
        {
            return new LiveReadinessEvaluation(
                Status: LiveReadinessStatus.BrokenDefaults,
                LiveReady: false,
                SafetyIntact: false,
                RootDirectory: root,
                ArtifactDirectory: artifactDir,
                MissingArtifacts: missing,
                PresentArtifacts: present,
                Notes: notes);
        }

        if (!Directory.Exists(artifactDir))
        {
            notes.Add($"artifact directory missing: {RelativeArtifactDirectory}");
            missing.Add(RelativeArtifactDirectory + "/");
            return BuildResult(
                LiveReadinessStatus.BlockedMissingEvidence,
                root,
                artifactDir,
                missing,
                present,
                notes);
        }

        // Required: paper multi-session export (.txt or .json)
        var paperTxt = Path.Combine(artifactDir, PaperExportTxt);
        var paperJson = Path.Combine(artifactDir, PaperExportJson);
        if (File.Exists(paperTxt))
        {
            present.Add(PaperExportTxt);
        }
        else if (File.Exists(paperJson))
        {
            present.Add(PaperExportJson);
        }
        else
        {
            missing.Add($"{PaperExportTxt}|{PaperExportJson}");
        }

        // Required: incident drill with a calendar date YYYY-MM-DD
        var incidentPath = Path.Combine(artifactDir, IncidentDrillRecord);
        if (!File.Exists(incidentPath))
        {
            missing.Add(IncidentDrillRecord);
        }
        else if (!ContainsIsoDate(File.ReadAllText(incidentPath)))
        {
            missing.Add($"{IncidentDrillRecord}#date");
            notes.Add($"{IncidentDrillRecord} present but missing YYYY-MM-DD date");
        }
        else
        {
            present.Add(IncidentDrillRecord);
        }

        RequireFile(artifactDir, OpenApiRecheckLog, present, missing);
        RequireFile(artifactDir, OwnerUnlockSignoff, present, missing);

        // Optional: Toss read smoke (redacted log OR residual note)
        var smokeLog = Path.Combine(artifactDir, TossReadSmokeRedactedLog);
        var smokeResidual = Path.Combine(artifactDir, TossReadSmokeResidual);
        if (File.Exists(smokeLog))
        {
            present.Add(TossReadSmokeRedactedLog);
        }
        else if (File.Exists(smokeResidual))
        {
            present.Add(TossReadSmokeResidual);
        }
        else
        {
            notes.Add(
                $"optional missing: {TossReadSmokeRedactedLog} or {TossReadSmokeResidual}");
        }

        var status = missing.Count == 0
            ? LiveReadinessStatus.ReadyForOwnerUnlock
            : LiveReadinessStatus.BlockedMissingEvidence;

        return BuildResult(status, root, artifactDir, missing, present, notes);
    }

    /// <summary>Wire status name used by shell automation (snake_case).</summary>
    public static string ToOwnerUnlockStatusToken(LiveReadinessStatus status) => status switch
    {
        LiveReadinessStatus.BlockedMissingEvidence => "blocked_missing_evidence",
        LiveReadinessStatus.ReadyForOwnerUnlock => "ready_for_owner_unlock",
        LiveReadinessStatus.BrokenDefaults => "broken_defaults",
        _ => "blocked_missing_evidence",
    };

    /// <summary>
    /// Runtime safety view for readiness evaluation. Constructed from shipped defaults
    /// or injected for unit tests that assert BrokenDefaults.
    /// </summary>
    public sealed record SafetySnapshot(
        bool AllowLiveOrders,
        bool KillSwitch,
        OrderMode OrderMode)
    {
        public static SafetySnapshot FromShippedDefaults() => new(
            TradingSafetyDefaults.AllowLiveOrders,
            TradingSafetyDefaults.KillSwitch,
            TradingSafetyDefaults.OrderMode);
    }

    private static LiveReadinessEvaluation BuildResult(
        LiveReadinessStatus status,
        string root,
        string artifactDir,
        List<string> missing,
        List<string> present,
        List<string> notes)
    {
        // LIVE_READY is never true from this evaluator alone.
        return new LiveReadinessEvaluation(
            Status: status,
            LiveReady: false,
            SafetyIntact: status != LiveReadinessStatus.BrokenDefaults,
            RootDirectory: root,
            ArtifactDirectory: artifactDir,
            MissingArtifacts: missing,
            PresentArtifacts: present,
            Notes: notes);
    }

    private static bool AreSafetyDefaultsFailClosed(SafetySnapshot safety, List<string> notes)
    {
        var ok = true;

        if (safety.AllowLiveOrders)
        {
            notes.Add("AllowLiveOrders is true (must be false)");
            ok = false;
        }

        if (!safety.KillSwitch)
        {
            notes.Add("KillSwitch is false (must be true)");
            ok = false;
        }

        if (safety.OrderMode != OrderMode.DryRun)
        {
            notes.Add($"OrderMode is {safety.OrderMode} (must be DryRun)");
            ok = false;
        }

        return ok;
    }

    private static void RequireFile(
        string artifactDir,
        string fileName,
        List<string> present,
        List<string> missing)
    {
        if (File.Exists(Path.Combine(artifactDir, fileName)))
        {
            present.Add(fileName);
        }
        else
        {
            missing.Add(fileName);
        }
    }

    private static bool ContainsIsoDate(string content)
    {
        foreach (Match match in IsoDatePattern.Matches(content))
        {
            if (DateTime.TryParseExact(
                    match.Value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                return true;
            }
        }

        return false;
    }
}
