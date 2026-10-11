using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.HelpAbout;
public sealed class HelpPageViewModel:ObservableObject,IDisposable
{
 const string Site="https://ferryclip.app",Docs=Site+"/docs.html",GitHub="https://github.com/Tanishktewetia/FerryClip";
 readonly ShellViewModel shell;bool troubleExpanded;
 public HelpPageViewModel(ShellViewModel shell)
 {
  this.shell=shell;shell.App.PropertyChanged+=AppChanged;WebsiteCommand=new RelayCommand(()=>Open(Site));GitHubCommand=new RelayCommand(()=>Open(GitHub));ReportCommand=new RelayCommand(()=>Open(GitHub+"/issues"));
  CopyAppInfoCommand=new RelayCommand(CopyAppInfo);TroubleCommand=new RelayCommand(()=>TroubleExpanded=!TroubleExpanded);
  Resources=new(){
   new("Quick start","Install and connect in minutes",G("M5,2 L15,2 L20,7 L20,22 L5,22 Z M15,2 L15,7 L20,7 M8,12 L17,12 M8,16 L17,16"),Docs+"#install",shell),
   new("Pairing your phone","Connect with a six-digit code",G("M7,2 L17,2 L17,22 L7,22 Z M10,5 L14,5 M11,19 L13,19 M3,7 L3,17 M21,7 L21,17"),Docs+"#setup",shell),
   new("Troubleshooting","Firewall and network fixes",G("M4,4 L20,4 L20,16 L13,16 L8,21 L8,16 L4,16 Z M8,8 L16,8 M8,12 L13,12"),Docs+"#troubleshooting",shell),
   new("FAQ","Common questions answered",G("M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M9,9 C9,5 16,5 16,9 C16,11 12,11 12,14 M12,18 L12,18.1"),Site+"/#faq",shell),
   new("Privacy & security","How local transfer stays private",G("M12,2 L20,5 L19,13 C18,18 15,21 12,22 C9,21 6,18 5,13 L4,5 Z M9,12 L11,14 L15,10"),Docs+"#privacy",shell),
   new("Release notes","What's new in each version",G("M4,4 L20,4 L20,20 L4,20 Z M8,8 L16,8 M8,12 L16,12 M8,16 L13,16"),GitHub+"/releases",shell)
  };
  TroubleshootingSteps=new(){
   new("1","Same Wi-Fi network","Connect your PC and phone to the same Wi-Fi.",G("M1,7 Q10,-1 19,7 M4,11 Q10,5 16,11 M7,15 Q10,12 13,15 M10,18 L10,18.1")),
   new("2","Allow FerryClip","Allow it through Windows Firewall on private networks.",G("M10,1 L18,4 17,11 Q16,17 10,20 Q4,17 3,11 L2,4 Z M6,10 L9,13 14,8")),
   new("3","Pair again","Generate a new code if your phone was reset or replaced.",G("M3,8 A7,7 0 1 1 4,15 M1,4 L3,8 7,7 M10,6 L10,11 14,13"))
  };
 }
 public ObservableCollection<DocumentationLinkViewModel> Resources{get;}
 public ObservableCollection<TroubleshootingStepViewModel> TroubleshootingSteps{get;}
 public ICommand WebsiteCommand{get;}public ICommand GitHubCommand{get;}public ICommand ReportCommand{get;}public ICommand CopyAppInfoCommand{get;}public ICommand TroubleCommand{get;}
 public string Version=>shell.App.Version;public string Footer=>$"FerryClip v{Version} - ferryclip.app";
 public bool IsConnected=>shell.App.State==ClipSync.Windows.MainUI.Models.ConnectionState.Connected;
 public bool IsWarning=>shell.App.State is ClipSync.Windows.MainUI.Models.ConnectionState.Unreachable or ClipSync.Windows.MainUI.Models.ConnectionState.NoDevices;
 public bool TroubleExpanded{get=>troubleExpanded;private set{if(Set(ref troubleExpanded,value))Raise(nameof(TroubleCollapsed));Raise(nameof(TroubleDetailsVisible));}}
 public bool TroubleCollapsed=>IsConnected&&!TroubleExpanded;
 public bool TroubleDetailsVisible=>!TroubleCollapsed;
 public string TroubleSummary=>TroubleExpanded?"Connected. Need help anyway?":"Connected. Need help anyway?";
 void CopyAppInfo(){var text=$"FerryClip {shell.App.Version}\nWindows {Environment.OSVersion.Version}\nConnection: {shell.StatusTitle}";try{if(!shell.App.CopyAppInfo(text))throw new InvalidOperationException("Clipboard is busy; try again.");shell.Notify("Copied app info");}catch(Exception ex){shell.App.Report(ex);}}
 void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){shell.App.Report(ex);}}
 static Geometry G(string data)=>Geometry.Parse(data);
 public void Dispose(){shell.App.PropertyChanged-=AppChanged;}
 void AppChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is "State" or "Devices" or "IsPaused"){Raise(nameof(IsConnected));Raise(nameof(IsWarning));Raise(nameof(TroubleCollapsed));}}
}
public sealed record TroubleshootingStepViewModel(string Number,string Title,string Description,Geometry Icon);public sealed class DocumentationLinkViewModel
{
 readonly string address;readonly ShellViewModel shell;
 public DocumentationLinkViewModel(string title,string description,Geometry icon,string address,ShellViewModel shell){Title=title;Description=description;Icon=icon;this.address=address;this.shell=shell;OpenCommand=new RelayCommand(Open);}
 public string Title{get;}public string Description{get;}public Geometry Icon{get;}public ICommand OpenCommand{get;}
 void Open(){try{Process.Start(new ProcessStartInfo(address){UseShellExecute=true});}catch(Exception ex){shell.App.Report(ex);}}
}
public sealed class AboutPageViewModel : ObservableObject, IDisposable
{
 readonly ShellViewModel shell;
 bool showingNotices;
 public AboutPageViewModel(ShellViewModel shell)
 {
  this.shell=shell;
  WebsiteCommand=new RelayCommand(()=>Open("https://ferryclip.app/"));
  GitHubCommand=new RelayCommand(()=>Open("https://github.com/Tanishktewetia/FerryClip"));
  ReleasesCommand=new RelayCommand(()=>Open("https://github.com/Tanishktewetia/FerryClip/releases"));
  PrivacyCommand=new RelayCommand(()=>Open("https://ferryclip.app/docs.html#privacy"));
  ShowNoticesCommand=new RelayCommand(()=>ShowingNotices=!ShowingNotices);
  CopyAppInfoCommand=new RelayCommand(CopyAppInfo);
  try { using var stream=typeof(AboutPageViewModel).Assembly.GetManifestResourceStream("ClipSync.Windows.Resources.ThirdPartyNotices.txt"); using var reader=stream is null?null:new StreamReader(stream); ThirdPartyNotices=reader?.ReadToEnd()??"Third-party dependency notices are unavailable."; }
  catch(Exception ex){ThirdPartyNotices="Third-party dependency notices are unavailable.";shell.App.Report(ex);}
 }
 public string VersionLabel=>$"Version {shell.App.Version}";
 public string Tagline=>"Clipboard sync for your PC and phone. Local, private, text-only.";
 public string CopyrightText=>"(c) 2026 FerryClip";
 public string ThirdPartyNotices{get;}
 public bool ShowingNotices{get=>showingNotices;private set{if(showingNotices==value)return;showingNotices=value;Raise(nameof(ShowingNotices));}}
 public bool LicenseAvailable=>false;
 public string LicenseName=>"";
 public IReadOnlyList<AboutFeature> Features{get;}=[
  new("Encrypted on your network","TLS 1.3 between your PC and phone",Geometry.Parse("M4,9 L16,9 16,18 4,18 Z M6,9 L6,6 A4,4 0 0 1 14,6 L14,9 M10,12 L10,15")),
  new("No cloud needed","No internet, account or cloud clipboard",Geometry.Parse("M6,16 C3.8,16 2,14.2 2,12 C2,9.8 3.8,8 6,8 C6.4,4.6 9,2 12,2 C15.4,2 18,4.6 18,8 C20.2,8.2 22,10 22,12 C22,14.2 20.2,16 18,16 Z M3,3 L21,21")),
  new("Text only","Plain-text clipboard sync between Windows and Android",Geometry.Parse("M4,2 L16,2 16,18 4,18 Z M7,6 L13,6 M7,9 L13,9 M7,12 L12,12"))
 ];
 public ICommand WebsiteCommand{get;} public ICommand GitHubCommand{get;} public ICommand ReleasesCommand{get;}
 public ICommand PrivacyCommand{get;} public ICommand ShowNoticesCommand{get;} public ICommand CopyAppInfoCommand{get;}
 void CopyAppInfo(){var info=$"FerryClip {shell.App.Version}\nWindows {Environment.OSVersion.Version}\nConnection: {shell.StatusTitle}";try{if(!shell.App.CopyAppInfo(info))throw new InvalidOperationException("Clipboard is busy; try again.");shell.Notify("App info copied. It was not added to FerryClip history or synced.");}catch(Exception e){shell.App.Report(e);}}
 void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){shell.App.Report(e);}}
 public void Dispose(){}
}
public sealed record AboutFeature(string Title,string Description,Geometry Icon);
