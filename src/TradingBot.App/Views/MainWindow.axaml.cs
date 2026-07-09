using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView.Avalonia;
using TradingBot.App.ViewModels;

namespace TradingBot.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpenedAsync;
        DataContextChanged += OnDataContextChanged;
        if (PriceChart is not null)
        {
            PriceChart.SizeChanged += OnPriceChartSizeChanged;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.LastCloseValue)
            or nameof(MainWindowViewModel.LastPriceYFraction)
            or nameof(MainWindowViewModel.LastCloseX)
            or nameof(MainWindowViewModel.Series))
        {
            Dispatcher.UIThread.Post(PinLastPriceBadge, DispatcherPriority.Loaded);
        }
    }

    private void OnPriceChartPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not CartesianChart chart)
        {
            return;
        }

        try
        {
            var pos = e.GetPosition(chart);
            var data = chart.ScalePixelsToData(new LvcPointD(pos.X, pos.Y));
            vm.UpdateHoverFromChartX(data.X);
        }
        catch
        {
            // ignore hover mapping failures
        }
    }

    private void OnPriceChartPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ClearHoverOhlc();
        }

        Dispatcher.UIThread.Post(PinLastPriceBadge, DispatcherPriority.Loaded);
    }

    private void OnPriceChartSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        PinLastPriceBadge();
    }

    private void PinLastPriceBadge()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (PriceChart is null || LastPriceBadge is null)
        {
            return;
        }

        var h = PriceChart.Bounds.Height;
        if (h < 10)
        {
            return;
        }

        var badgeH = LastPriceBadge.Bounds.Height > 0 ? LastPriceBadge.Bounds.Height : 20;

        try
        {
            var dataY = vm.LastCloseValue;
            if (dataY > 0 && vm.LastCloseX > 0)
            {
                var pt = PriceChart.ScaleDataToPixels(new LvcPointD(vm.LastCloseX, dataY));
                LastPriceBadge.Margin = new Thickness(
                    0,
                    Math.Clamp(pt.Y - badgeH / 2, 0, h - badgeH),
                    4,
                    0);
                return;
            }
        }
        catch
        {
        }

        var y = vm.LastPriceYFraction * h;
        LastPriceBadge.Margin = new Thickness(
            0,
            Math.Clamp(y - badgeH / 2, 0, h - badgeH),
            4,
            0);
    }

    private async void OnOpenedAsync(object? sender, EventArgs e)
    {
        try
        {
            if (PriceChart is not null)
            {
                PriceChart.SizeChanged -= OnPriceChartSizeChanged;
                PriceChart.SizeChanged += OnPriceChartSizeChanged;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.PropertyChanged += OnViewModelPropertyChanged;

                await vm.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);

                for (var i = 0; i < 50; i++)
                {
                    if (!string.IsNullOrWhiteSpace(vm.LastCloseText)
                        && vm.LastCloseText != "—"
                        && vm.Series.Length > 0)
                    {
                        break;
                    }

                    if (vm.Series.Length == 0
                        && !string.IsNullOrWhiteSpace(vm.OhlcStatusLine)
                        && vm.OhlcStatusLine.Contains("없음", StringComparison.Ordinal))
                    {
                        break;
                    }

                    await Task.Delay(150).ConfigureAwait(true);
                }

                Dispatcher.UIThread.Post(PinLastPriceBadge, DispatcherPriority.Loaded);
                await Task.Delay(50).ConfigureAwait(true);
                Dispatcher.UIThread.Post(PinLastPriceBadge, DispatcherPriority.Background);
            }

            await Task.Delay(1000).ConfigureAwait(true);
            await Dispatcher.UIThread.InvokeAsync(CaptureUiSnapshot);
            await Task.Delay(700).ConfigureAwait(true);
            await Dispatcher.UIThread.InvokeAsync(CaptureUiSnapshot);
        }
        catch
        {
            // never block cockpit
        }
    }

    private void CaptureUiSnapshot()
    {
        try
        {
            PinLastPriceBadge();

            var w = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
            var h = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
            if (w < 200 || h < 200)
            {
                return;
            }

            var pixelSize = new PixelSize(w * 2, h * 2);
            var dpi = new Vector(192, 192);
            using var bmp = new RenderTargetBitmap(pixelSize, dpi);
            bmp.Render(this);

            var dir = Environment.GetEnvironmentVariable("GROK_GOAL_SCRATCH");
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".grok",
                    "ui-snapshots");
            }

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ui-check.png");
            bmp.Save(path);
        }
        catch
        {
            // ignore capture failures
        }
    }
}
