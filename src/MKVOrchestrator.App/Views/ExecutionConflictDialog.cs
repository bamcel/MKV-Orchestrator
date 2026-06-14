using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MKVOrchestrator.Core.Services;

namespace MKVOrchestrator.App.Views;

public sealed class ExecutionConflictDialog : Window
{
    public ExecutionConflictDialog(IReadOnlyList<FileConflictResult> conflicts)
    {
        Title = "Execution Conflicts Detected";
        Width = 720;
        Height = 460;
        MinWidth = 640;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Margin = new Thickness(18),
            RowSpacing = 12
        };

        var title = new TextBlock
        {
            Text = $"{conflicts.Count} file conflict(s) were detected before execution.",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var message = new TextBlock
        {
            Text = "Choose whether to skip the conflicting files and continue with the remaining files, or cancel the entire run.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85
        };
        Grid.SetRow(message, 1);
        root.Children.Add(message);

        var list = new ListBox
        {
            ItemsSource = conflicts.Select(c => $"{Path.GetFileName(c.FilePath)}  —  {c.Reason}").ToList(),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        Grid.SetRow(list, 2);
        root.Children.Add(list);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };

        var cancelButton = new Button
        {
            Content = "Cancel Run",
            MinWidth = 120
        };
        cancelButton.Click += (_, _) => Close(false);

        var skipButton = new Button
        {
            Content = "Skip Conflicts and Continue",
            MinWidth = 210
        };
        skipButton.Click += (_, _) => Close(true);

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(skipButton);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);

        Content = root;
    }
}
