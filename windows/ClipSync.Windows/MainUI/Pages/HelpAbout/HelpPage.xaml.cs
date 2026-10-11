using System.Windows.Controls;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.HelpAbout;
public partial class HelpPage : System.Windows.Controls.UserControl, IDisposable
{
 readonly HelpPageViewModel viewModel; bool disposed;
 public HelpPage(ShellViewModel shell){InitializeComponent();viewModel=new(shell);DataContext=viewModel;}
 public void Dispose(){if(disposed)return;disposed=true;viewModel.Dispose();DataContext=null;}
}