using System.Windows.Controls;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.HelpAbout;
public partial class AboutPage : System.Windows.Controls.UserControl, IDisposable
{
 AboutPageViewModel viewModel; bool disposed;
 public AboutPage(ShellViewModel shell){InitializeComponent();viewModel=new(shell);DataContext=viewModel;}
 public void Dispose(){if(disposed)return;disposed=true;viewModel.Dispose();DataContext=null;}
}