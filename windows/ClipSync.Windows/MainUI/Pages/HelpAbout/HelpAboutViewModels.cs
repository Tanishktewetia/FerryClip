using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.HelpAbout;
public sealed class HelpPageViewModel : IDisposable
{
 const string Docs="https://ferryclip.app/docs.html", GitHub="https://github.com/Tanishktewetia/FerryClip";
 readonly ShellViewModel shell;
 public HelpPageViewModel(ShellViewModel shell)
 {
  this.shell=shell;
  Resources=new()
  {
   new("Quick start","Install FerryClip and make your first connection.",G("M5,2 L15,2 L20,7 L20,22 L5,22 Z M15,2 L15,7 L20,7 M8,12 L17,12 M8,16 L17,16"),Docs+"#install",shell),
   new("Pairing your phone","Connect Android to your Windows PC with a one-time code.",G("M7,2 L17,2 L17,22 L7,22 Z M10,5 L14,5 M11,19 L13,19 M3,7 L3,17 M21,7 L21,17"),Docs+"#setup",shell),
   new("Troubleshooting","Fix pairing, network, firewall, or clipboard issues.",G("M4,4 L20,4 L20,16 L13,16 L8,21 L8,16 L4,16 Z M8,8 L16,8 M8,12 L13,12"),Docs+"#troubleshooting",shell),
   new("FAQ","Quick answers about local syncing, Android, and supported content.",G("M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M9,9 C9,5 16,5 16,9 C16,11 12,11 12,14 M12,18 L12,18.1"),"https://ferryclip.app/#faq",shell),
   new("Privacy & security","See how local transfer, pairing, and clipboard data are handled.",G("M12,2 L20,5 L19,13 C18,18 15,21 12,22 C9,21 6,18 5,13 L4,5 Z M9,12 L11,14 L15,10"),Docs+"#privacy",shell),
   new("Release notes","Read changes and download details for published releases.",G("M4,4 L20,4 L20,20 L4,20 Z M8,8 L16,8 M8,12 L16,12 M8,16 L13,16"),GitHub+"/releases",shell)
  };
  TroubleshootingSteps=new()
  {
   new("1","Check the network","Keep both apps running on the same reachable Wi-Fi, hotspot, or VPN-routed network."),
   new("2","Allow the connection","Make sure Windows Firewall allows FerryClip; guest isolation or VPN rules can block local traffic."),
   new("3","Pair again, then test","Generate a fresh code. For phone-to-PC clips, tap Send to PC after copying.")
  };
  WebsiteCommand=new RelayCommand(()=>Open("https://ferryclip.app/")); GitHubCommand=new RelayCommand(()=>Open(GitHub));
 }
 public ObservableCollection<DocumentationLinkViewModel> Resources{get;}
 public ObservableCollection<TroubleshootingStepViewModel> TroubleshootingSteps{get;}
 public ICommand WebsiteCommand{get;} public ICommand GitHubCommand{get;}
 static Geometry G(string s)=>Geometry.Parse(s);
 void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){shell.App.Report(e);}}
 public void Dispose(){}
}
public sealed class DocumentationLinkViewModel
{
 readonly string address; readonly ShellViewModel shell;
 public DocumentationLinkViewModel(string title,string description,Geometry icon,string address,ShellViewModel shell)
 {Title=title;Description=description;Icon=icon;this.address=address;this.shell=shell;OpenCommand=new RelayCommand(Open);}
 public string Title{get;} public string Description{get;} public Geometry Icon{get;} public ICommand OpenCommand{get;}
 void Open(){try{Process.Start(new ProcessStartInfo(address){UseShellExecute=true});}catch(Exception e){shell.App.Report(e);}}
}
public sealed record TroubleshootingStepViewModel(string Number,string Title,string Description);
public sealed class AboutPageViewModel : IDisposable
{
 readonly ShellViewModel shell;
 public AboutPageViewModel(ShellViewModel shell){this.shell=shell;WebsiteCommand=new RelayCommand(()=>Open("https://ferryclip.app/"));GitHubCommand=new RelayCommand(()=>Open("https://github.com/Tanishktewetia/FerryClip"));ReleasesCommand=new RelayCommand(()=>Open("https://github.com/Tanishktewetia/FerryClip/releases"));}
 public string VersionLabel=>$"Version {shell.App.Version}";
 public string Tagline=>"Clipboard sync for your PC and phone. Local, private, text-only.";
 public string LicenseText=>"License: not specified";
 // TODO: choose and publish a license.
 public string CopyrightText=>"© 2026 FerryClip";
 public IReadOnlyList<string> Features{get;}=["Direct, encrypted transfer over your local network","Plain-text clipboard sync between Windows and Android","No account or cloud clipboard"];
 public ICommand WebsiteCommand{get;} public ICommand GitHubCommand{get;} public ICommand ReleasesCommand{get;}
 void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){shell.App.Report(e);}}
 public void Dispose(){}
}
