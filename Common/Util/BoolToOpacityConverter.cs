using System.Globalization;

namespace NBNavApp;

public class BoolToOpacityConverter : IValueConverter
{
    public double TrueOpacity { get; set; } = 1;
    public double FalseOpacity { get; set; } = 0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? TrueOpacity : FalseOpacity;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
