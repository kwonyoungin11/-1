namespace TradingBot.Ui;

/// <summary>
/// One order-candidate row for home / candidate list binding.
/// Candidates are proposals only — never live submissions.
/// <see cref="IsLive"/> is hard-coded false until a future live-readiness phase.
/// </summary>
public sealed class OrderCandidateRowViewModel
{
    public required string Symbol { get; init; }
    public required string Side { get; init; }
    public required decimal Quantity { get; init; }
    public decimal? LimitPrice { get; init; }
    public string? ClientOrderId { get; init; }
    public required string Status { get; init; }

    /// <summary>
    /// Always false. Binding surfaces must not show "live order" for candidates.
    /// Live execution UI is forbidden until readiness gates and owner approval.
    /// </summary>
    public bool IsLive => false;
}
