using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MKVOrchestrator.Core.Models;

namespace MKVOrchestrator.App.Converters;

public sealed class VisualStateBrushConverter : IValueConverter
{
    public IBrush NormalBrush { get; set; } = Brushes.White;
    public IBrush WarningBrush { get; set; } = Brushes.Orange;
    public IBrush TemplateBrush { get; set; } = Brush.Parse("#BD93F9");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string mode && mode.Equals("TemplateOnly", StringComparison.OrdinalIgnoreCase))
        {
            return value is VisualState.Template ? TemplateBrush : NormalBrush;
        }

        return value switch
        {
            VisualState.Warning => WarningBrush,
            VisualState.Template => TemplateBrush,
            _ => NormalBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
