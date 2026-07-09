using Avalonia.Controls;
using TradingBot.App.ViewModels;

namespace TradingBot.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.RefreshCommand.ExecuteAsync(null);
            }
        };
    }
}
