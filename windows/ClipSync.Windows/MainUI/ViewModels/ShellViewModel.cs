using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Services;
namespace ClipSync.Windows.MainUI.ViewModels;
public sealed class NavigationItem : ObservableObject
{
 bool selected;
 public NavigationItem(string title,string icon,ICommand command){Title=title;Icon=icon;Command=command;}
 public string Title{get;} public string Icon{get;} public ICommand Command{get;}
 public bool IsSelected{get=>selected;set=>Set(ref selected,value);}
}
public sealed class ShellViewModel : ObservableObject, IDisposable
{
 public static readonly string[] PageNames=["Dashboard","Devices","History","Settings","Help & Docs","About"];
 readonly AppState state;bool disposed;string page="Dashboard",toast="",dialogTitle="",dialogText="";TaskCompletionSource<bool>? confirmation;
 public ShellViewModel(AppState state){this.state=state;foreach(var name in PageNames)Navigation.Register(name,()=>null);RegisterPages();Items=new(PageNames.Select((name,i)=>new NavigationItem(name,new[]{"DashboardIcon","DevicesIcon","HistoryIcon","SettingsIcon","HelpIcon","AboutIcon"}[i],new RelayCommand(()=>Navigate(name)))));DevicesCommand=new RelayCommand(()=>Navigate("Devices"));Navigate(state.Settings.LastPage);state.PropertyChanged+=StateChanged;state.ActionFailed+=Failed;ConfirmCommand=new RelayCommand(()=>CompleteDialog(true));CancelCommand=new RelayCommand(()=>CompleteDialog(false));DismissToastCommand=new RelayCommand(()=>Toast="");}
 void RegisterPages()
 {
  Navigation.Register("Dashboard",()=>new Pages.Dashboard.DashboardPage(this));
  Navigation.Register("Devices",()=>new Pages.Devices.DevicesPage(this));
  Navigation.Register("History",()=>new Pages.History.HistoryPage(this));
  Navigation.Register("Settings",()=>new Pages.Settings.SettingsPage(this));
  Navigation.Register("Help & Docs",()=>new Pages.HelpAbout.HelpPage(this));
  Navigation.Register("About",()=>new Pages.HelpAbout.AboutPage(this));
 }
 public NavigationService Navigation {get;}=new();
 public AppState App=>state;
 public ObservableCollection<NavigationItem> Items{get;}
 public string Page{get=>page;private set=>Set(ref page,value);}
 public string Toast{get=>toast;private set{Set(ref toast,value);Raise(nameof(HasToast));}}
 public bool HasToast=>Toast.Length>0;
 bool showDialogOption,dialogOptionChecked;
 public bool ShowDialogOption{get=>showDialogOption;private set=>Set(ref showDialogOption,value);}
 public bool DialogOptionChecked{get=>dialogOptionChecked;set=>Set(ref dialogOptionChecked,value);}
 public string DialogOptionLabel{get;private set;}="";
 public string DialogTitle{get=>dialogTitle;private set=>Set(ref dialogTitle,value);}
 public string DialogText{get=>dialogText;private set=>Set(ref dialogText,value);}
 public bool DialogOpen=>confirmation is not null;
 public ICommand WebsiteCommand {get;}=new RelayCommand(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://ferryclip.app"){UseShellExecute=true}));
 public ICommand ConfirmCommand{get;} public ICommand CancelCommand{get;} public ICommand DismissToastCommand{get;} public ICommand DevicesCommand{get;}
 public string StatusTitle=>state.State switch {Models.ConnectionState.NoDevices=>"No devices",Models.ConnectionState.Unreachable=>"Not connected",_=>state.State.ToString()};
 public System.Windows.Media.Brush StatusBrush=>new System.Windows.Media.SolidColorBrush(state.State switch { Models.ConnectionState.Paused=>System.Windows.Media.Color.FromRgb(234,187,98), Models.ConnectionState.Connected=>System.Windows.Media.Color.FromRgb(83,202,155), Models.ConnectionState.Unreachable=>System.Windows.Media.Color.FromRgb(255,138,138), _=>System.Windows.Media.Color.FromRgb(171,181,195) });
 public string StatusDescription=>$"{state.Devices.Count(d=>d.Connected)} {(state.Devices.Count(d=>d.Connected)==1?"device":"devices")}"; public string FooterLinkText=>$"v{App.Version} - ferryclip.app ↗";
 public event Action? HistorySearchRequested;
 public void FocusHistorySearch(){Navigate("History");HistorySearchRequested?.Invoke();}
 public void Navigate(string name){if(!PageNames.Contains(name))name="Dashboard";Page=name;Navigation.Navigate(name);if(Items is not null)foreach(var item in Items)item.IsSelected=item.Title==name;}
 public Task<bool> ConfirmAsync(string title,string text,string? optionLabel=null){ShowDialogOption=optionLabel is not null;DialogOptionChecked=false;DialogOptionLabel=optionLabel??"";Raise(nameof(DialogOptionLabel));if(confirmation is not null)throw new InvalidOperationException("A confirmation is already open.");DialogTitle=title;DialogText=text;confirmation=new(TaskCreationOptions.RunContinuationsAsynchronously);Raise(nameof(DialogOpen));return confirmation.Task;}
 void CompleteDialog(bool result){var source=confirmation;confirmation=null;Raise(nameof(DialogOpen));source?.TrySetResult(result);}
 int toastRevision;public async void Notify(string text){var revision=++toastRevision;Toast=text;await Task.Delay(2500);if(!disposed&&revision==toastRevision)Toast="";}
 void Failed(Exception ex){Logging.FileLogger.Instance.Error("Main UI command failed",ex);Notify(ex.Message);}
 void StateChanged(object? sender,PropertyChangedEventArgs e){Raise(nameof(StatusTitle));Raise(nameof(StatusBrush));Raise(nameof(StatusDescription));}
 public void Dispose(){disposed=true;++toastRevision;state.PropertyChanged-=StateChanged;state.ActionFailed-=Failed;CompleteDialog(false);Navigation.Dispose();}
}
