using System.Windows;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Services;
public sealed class MainWindowService : IDisposable
{
 readonly AppState state;readonly SettingsStore? settings;MainWindow? window;
 public MainWindowService(AppState state,SettingsStore? settings=null){this.state=state;this.settings=settings;}
 public MainWindow Open(string? page=null)
 {
  System.Windows.Application.Current.Dispatcher.VerifyAccess();
  if(window is null){window=new MainWindow(new ShellViewModel(state),settings);window.Closed+=(_,_)=>window=null;}
  if(page is not null)window.NavigateTo(page);
  if(!window.IsVisible)window.Show();if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;
  window.Activate();return window;
 }
 public void Dispose(){window?.CloseForExit();window=null;}
}
