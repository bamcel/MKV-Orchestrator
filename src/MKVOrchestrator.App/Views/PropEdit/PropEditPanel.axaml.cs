using Avalonia.Controls;
using MKVOrchestrator.App.ViewModels;

namespace MKVOrchestrator.App.Views.PropEdit;

public partial class PropEditPanel : UserControl
{
    public PropEditPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => EnsureState();
        DataContextChanged += (_, _) => EnsureState();
    }

    private void EnsureState()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EnsurePropEditState();
        }
    }
}
