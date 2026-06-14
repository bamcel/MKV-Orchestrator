using Avalonia;
using Avalonia.Controls;

namespace MKVOrchestrator.App.Controls;

public partial class OrchestratorSection : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<OrchestratorSection, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<object?> SectionContentProperty =
        AvaloniaProperty.Register<OrchestratorSection, object?>(nameof(SectionContent));

    public OrchestratorSection()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }
}
