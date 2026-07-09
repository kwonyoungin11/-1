using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TradingBot.App.ViewModels;

/// <summary>
/// Chip for VMAR ↔ SPCX stock-kind switcher (bindable UI contract).
/// </summary>
public partial class StockKindChipVm : ObservableObject
{
    private readonly Action<string> _onSelect;

    public StockKindChipVm(
        string kindLabel,
        string tickerLabel,
        string displayName,
        string subtitle,
        Action<string> onSelect)
    {
        KindLabel = kindLabel ?? throw new ArgumentNullException(nameof(kindLabel));
        TickerLabel = tickerLabel ?? throw new ArgumentNullException(nameof(tickerLabel));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Subtitle = subtitle ?? throw new ArgumentNullException(nameof(subtitle));
        _onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
    }

    /// <summary>Enum ToString e.g. "비전마린" / "스페이스X".</summary>
    public string KindLabel { get; }

    /// <summary>Ticker e.g. "VMAR" / "SPCX".</summary>
    public string TickerLabel { get; }

    /// <summary>Short Korean display name.</summary>
    public string DisplayName { get; }

    /// <summary>One-line owner hint for the chip subtitle.</summary>
    public string Subtitle { get; }

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => _onSelect(KindLabel);
}
