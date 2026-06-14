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

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is VisualState.Warning ? WarningBrush : NormalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
