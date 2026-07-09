namespace TradingBot.Domain;

/// <summary>
/// Structured result of <see cref="LiveReadinessEvaluator"/>.
/// <see cref="LiveReady"/> is never set true by the evaluator under fail-closed defaults.
/// </summary>
public sealed record LiveReadinessEvaluation(
    LiveReadinessStatus Status,
    bool LiveReady,
    bool SafetyIntact,
    string RootDirectory,
    string ArtifactDirectory,
    IReadOnlyList<string> MissingArtifacts,
    IReadOnlyList<string> PresentArtifacts,
    IReadOnlyList<string> Notes);
