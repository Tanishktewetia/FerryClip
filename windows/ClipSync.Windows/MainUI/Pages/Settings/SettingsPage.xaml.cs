using System.Windows;
using System.Windows.Controls;
using ClipSync.Windows.MainUI.ViewModels;

namespace ClipSync.Windows.MainUI.Pages.Settings;

public partial class SettingsPage : System.Windows.Controls.UserControl, IDisposable
{
    readonly SettingsViewModel viewModel;

    public SettingsPage(ShellViewModel shell)
    {
        InitializeComponent();
        viewModel = new SettingsViewModel(shell);
        DataContext = viewModel;
#if DEBUG
        var diagnostics=new System.Windows.Controls.Border {Style=(Style)FindResource("Card"),Margin=new Thickness(0,0,0,16)};
        var content=new StackPanel();content.Children.Add(new TextBlock {Text="Diagnostics",FontSize=16,FontWeight=FontWeights.SemiBold});
        var logs=new System.Windows.Controls.Button {Content="Open log folder",HorizontalAlignment=System.Windows.HorizontalAlignment.Left,Margin=new Thickness(0,12,0,0)};
        logs.Click+=(_,_)=>UI.DesktopActions.OpenLogFolder();content.Children.Add(logs);diagnostics.Child=content;SettingsContent.Children.Add(diagnostics);
#endif
    }

    public void Dispose()
    {
        viewModel.Dispose();
        DataContext = null;
    }
}
