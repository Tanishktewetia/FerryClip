using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Controls;
using ClipSync.Windows.MainUI.ViewModels;

namespace ClipSync.Windows.MainUI.Pages.Dashboard;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is bool active && active?Visibility.Collapsed:Visibility.Visible;
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}

public partial class DashboardPage : System.Windows.Controls.UserControl, IDisposable
{
    private readonly DashboardViewModel model;
    private Window? host;
    private bool disposed;

    public DashboardPage(ShellViewModel shell)
    {
        InitializeComponent();
        model = new DashboardViewModel(shell);
        DataContext = model;
        Loaded += AttachWindow;
        Unloaded += DetachWindow;
    }

    private void AttachWindow(object sender, System.Windows.RoutedEventArgs e)
    {
        if (disposed)
            return;

        var nextHost = Window.GetWindow(this);
        if (ReferenceEquals(host, nextHost))
            return;

        if (host is not null)
            host.SizeChanged -= Resize;

        host = nextHost;
        if (host is not null)
        {
            host.SizeChanged += Resize;
            LayoutColumns(host.ActualWidth);
        }
    }

    private void DetachWindow(object sender, System.Windows.RoutedEventArgs e)
    {
        if (host is null)
            return;

        host.SizeChanged -= Resize;
        host = null;
    }

    private void Resize(object sender, System.Windows.SizeChangedEventArgs e) => LayoutColumns(e.NewSize.Width);

    private void LayoutColumns(double width)
    {
        var stacked = width < 1000;
        Columns.ColumnDefinitions[1].Width = new GridLength(stacked ? 0 : 18);
        Columns.ColumnDefinitions[2].Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(DevicesCard, stacked ? 1 : 0);
        Grid.SetColumn(DevicesCard, stacked ? 0 : 2);
        DevicesCard.Margin = new Thickness(0, stacked ? 18 : 0, 0, 0);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        DetachWindow(this, new System.Windows.RoutedEventArgs());
        Loaded -= AttachWindow;
        Unloaded -= DetachWindow;
        model.Dispose();
    }
}
