using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
namespace ClipSync.Windows.MainUI.Infrastructure;
public sealed class IconConverter : IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>System.Windows.Application.Current.MainWindow?.TryFindResource(value?.ToString()??"")??Geometry.Empty;
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}

