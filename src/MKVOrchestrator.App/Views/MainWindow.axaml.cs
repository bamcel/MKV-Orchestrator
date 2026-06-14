using Avalonia.Controls;
using MKVOrchestrator.App.ViewModels;

namespace MKVOrchestrator.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ConfigureViewModel();
        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.InitializeAfterUiReadyAsync();
            }
        };
    }

    private void ConfigureViewModel()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        vm.ConfirmSkipConflictsAsync = async conflicts =>
        {
            var dialog = new ExecutionConflictDialog(conflicts);
            var result = await dialog.ShowDialog<bool?>(this);
            return result == true;
        };

        vm.ShowOutputWindow = (title, lines) =>
        {
            var dialog = new OutputWindow(title, lines);
            _ = dialog.ShowDialog(this);
        };
    }
}
