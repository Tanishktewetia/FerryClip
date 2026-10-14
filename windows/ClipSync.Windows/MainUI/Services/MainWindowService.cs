using System.Windows;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Services;
public sealed class MainWindowService : IDisposable
{
 readonly AppState state;readonly SettingsStore? settings;MainWindow? window;bool closeNoticeShown;public Action<string>? Notice {get;set;}
 public MainWindowService(AppState state,SettingsStore? settings=null){this.state=state;this.settings=settings;}
 public MainWindow Open(string? page=null,bool onboarding=false)
 {
  System.Windows.Application.Current.Dispatcher.VerifyAccess();var current=window;
  if(current is null){current=new MainWindow(new ShellViewModel(state),settings,onboarding);window=current;current.TrayNotice=message=>{if(closeNoticeShown)return;closeNoticeShown=true;Notice?.Invoke(message);};current.Closed+=(_,_)=>window=null;}
  else if(onboarding)current.EnterOnboarding();
  if(page is not null)current.NavigateTo(page);if(!current.IsVisible)current.Show();if(current.WindowState==WindowState.Minimized)current.WindowState=WindowState.Normal;current.Activate();return current;
 }
 public void Dispose(){window?.CloseForExit();window=null;}
}
