using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.Onboarding;
public sealed class OnboardingStepItem : ObservableObject
{
 public OnboardingStepItem(int index,string label,Action jump){Index=index;Label=label;Command=new RelayCommand(jump);}
 public int Index{get;}public string Label{get;}public ICommand Command{get;}bool current,complete;
 public bool IsCurrent{get=>current;set{if(Set(ref current,value)){Raise(nameof(Glyph));Raise(nameof(AccessibleName));}}}public bool IsComplete{get=>complete;set{if(Set(ref complete,value)){Raise(nameof(Glyph));Raise(nameof(AccessibleName));}}}
 public string Glyph=>IsComplete?"✓":(Index+1).ToString();
 public string AccessibleName=>$"Step {Index+1} of 5, {Label}, {(IsCurrent?"current":IsComplete?"completed":"upcoming")}";
}
public sealed class OnboardingDeviceItem : ObservableObject
{
 DeviceSnapshot device;public OnboardingDeviceItem(DeviceSnapshot device){this.device=device;}public string Id=>device.Id;public string Name=>device.Name;public bool IsConnected=>device.Connected;public string StatusLine=>$"{(device.Connected?"Connected":"Not connected")} · {ClipSync.Windows.MainUI.Infrastructure.TimeFormatter.Format(device.Last??device.Since)}";public string FullTime=>ClipSync.Windows.MainUI.Infrastructure.TimeFormatter.Full(device.Last??device.Since);public void Update(DeviceSnapshot value){device=value;Raise(nameof(Name));Raise(nameof(IsConnected));Raise(nameof(StatusLine));Raise(nameof(FullTime));}
}
public sealed class OnboardingViewModel : ObservableObject, IDisposable
{
 readonly ShellViewModel shell;readonly HashSet<string> initialDevices;HashSet<string> pairingBaseline=[];int step;bool disposed,pairingExpanded;DeviceSnapshot? pairedDuringGuide;
 public OnboardingViewModel(ShellViewModel shell){this.shell=shell;initialDevices=shell.App.Devices.Select(d=>d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);Pairing=new PairingViewModel(shell.App);Devices=new();Steps=new([new(0,"Welcome",()=>Step=0),new(1,"Android app",()=>Step=1),new(2,"Pair",()=>Step=2),new(3,"Use it",()=>Step=3),new(4,"Tray",()=>Step=4)]);BackCommand=new RelayCommand(()=>Step--,()=>Step>0);NextCommand=new RelayCommand(Advance);SkipCommand=new RelayCommand(()=>SkipRequested?.Invoke());OpenWebsiteCommand=new RelayCommand(OpenWebsite);CopyLinkCommand=new RelayCommand(CopyLink);PairAnotherCommand=new RelayCommand(BeginPairing);shell.App.PropertyChanged+=AppChanged;RefreshDevices();UpdateSteps();}
 public event Action? DoneRequested,SkipRequested;
 public PairingViewModel Pairing{get;}public ObservableCollection<OnboardingStepItem> Steps{get;}
 public int Step{get=>step;set{if(value is <0 or >4||!Set(ref step,value))return;UpdateSteps();RaiseAll();}}
 public double Progress=>(Step+1)*20;
 public string StepCounter=>$"Step {Step+1} of 5";
 public string StepTitle=>Step switch{0=>"Copy here. Paste there.",1=>"Get FerryClip on\nyour phone",2=>PairTitle,3=>"Two ways to send text",_=>"FerryClip lives in your tray"};
 public string StepDescription=>Step switch{0=>"FerryClip shares clipboard text between this PC and your Android phone over your own network. No account and no cloud.",1=>"Install FerryClip on Android, then come back here when you are ready to connect it to this PC.",2=>PairDescription,3=>"Copy on one device, then send or paste on the other. Here is exactly what to do.",_=>"FerryClip keeps running in the notification area when its window is hidden."};
 string PairTitle=>pairedDuringGuide is not null?(pairedDuringGuide.Connected?"Phone connected":"Phone paired"):shell.App.Devices.Count==0?"Connect your phone":shell.App.Devices.Any(d=>d.Connected)?"Your phone is connected":"Your phone is paired";
 string PairDescription=>pairedDuringGuide is not null?"The new phone has been paired with this PC.":shell.App.Devices.Count==0?"Pairing starts only when you choose Generate code. Keep both devices reachable on the same Wi-Fi, hotspot, or local VPN.":"This PC already has a paired phone. You can check its connection or add another phone.";
 public bool IsWelcome=>Step==0;public bool IsDownload=>Step==1;public bool IsPair=>Step==2;public bool IsUseIt=>Step==3;public bool IsTray=>Step==4;
 public bool IsNotPaired=>shell.App.Devices.Count==0&&pairedDuringGuide is null;
 public bool ShowPairingPanel=>IsNotPaired||pairingExpanded;
 public bool HasExistingDevices=>shell.App.Devices.Count>0;
 public bool HasConnectedDevice=>shell.App.Devices.Any(d=>d.Connected)||(pairedDuringGuide?.Connected??false);
 public bool HasPairSuccess=>pairedDuringGuide is not null;
 public string PairSuccessText=>pairedDuringGuide is null?"":$"✓  {pairedDuringGuide.Name} · {(pairedDuringGuide.Connected?"Connected":"Paired")}";
 public bool CanGoBack=>Step>0;
 public string TrayPauseText=>shell.App.IsPaused?"Resume sync":"Pause sync";public string TooltipExample=>shell.App.State switch{ConnectionState.Connected=>$"FerryClip - Connected, {shell.App.Devices.Count(d=>d.Connected)} {(shell.App.Devices.Count(d=>d.Connected)==1?"device":"devices")}",ConnectionState.Paused=>"FerryClip - Paused",ConnectionState.Unreachable=>"FerryClip - Not connected",_=>"FerryClip - No devices paired"};public bool ShowSettingsFootnote=>IsUseIt||IsTray;
 public ObservableCollection<OnboardingDeviceItem> Devices{get;}
 
 public bool StartWithWindows{get=>shell.App.StartupEnabled;set{if(value==StartWithWindows)return;try{shell.App.SetStartup(value);}catch(Exception ex){shell.App.Report(ex);}Raise();}}
 public string StartupState=>StartWithWindows?"On":"Off";
 public ICommand BackCommand{get;}public ICommand NextCommand{get;}public ICommand SkipCommand{get;}public ICommand OpenWebsiteCommand{get;}public ICommand CopyLinkCommand{get;}public ICommand PairAnotherCommand{get;}
 public void OpenPairing(){pairingExpanded=true;pairingBaseline=shell.App.Devices.Select(d=>d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);Raise(nameof(ShowPairingPanel));}
 void BeginPairing()=>OpenPairing();
 void Advance(){if(Step==4)DoneRequested?.Invoke();else Step++;}
 void OpenWebsite(){try{Process.Start(new ProcessStartInfo("https://ferryclip.app"){UseShellExecute=true});}catch(Exception ex){shell.App.Report(ex);}}
 void CopyLink(){try{if(!shell.App.CopyAppInfo("https://ferryclip.app"))throw new InvalidOperationException("Clipboard is busy; try again.");shell.Notify("Link copied");}catch(Exception ex){shell.App.Report(ex);}}
 void AppChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is "Devices" or "State" or "Pairing" or "StartupEnabled"){if(e.PropertyName=="Devices"){if(pairedDuringGuide is not null)pairedDuringGuide=shell.App.Devices.FirstOrDefault(d=>d.Id==pairedDuringGuide.Id)??pairedDuringGuide;var baseline=pairingExpanded?pairingBaseline:initialDevices;var added=shell.App.Devices.FirstOrDefault(d=>!baseline.Contains(d.Id));if(added is not null)pairedDuringGuide=added;RefreshDevices();}foreach(var p in new[]{nameof(IsNotPaired),nameof(ShowPairingPanel),nameof(HasExistingDevices),nameof(HasConnectedDevice),nameof(HasPairSuccess),nameof(PairSuccessText),nameof(StepTitle),nameof(StepDescription),nameof(StartWithWindows),nameof(StartupState),nameof(TrayPauseText),nameof(TooltipExample),nameof(ShowSettingsFootnote)})Raise(p);}}
 void RefreshDevices(){var current=shell.App.Devices;foreach(var old in Devices.Where(x=>!current.Any(d=>d.Id==x.Id)).ToArray())Devices.Remove(old);foreach(var d in current){var item=Devices.FirstOrDefault(x=>x.Id==d.Id);if(item is null)Devices.Add(new(d));else item.Update(d);}}
 void UpdateSteps(){for(var i=0;i<Steps.Count;i++){Steps[i].IsCurrent=i==Step;Steps[i].IsComplete=i<Step;}}
 void RaiseAll(){foreach(var p in new[]{nameof(StepCounter),nameof(StepTitle),nameof(StepDescription),nameof(CanGoBack),nameof(IsWelcome),nameof(IsDownload),nameof(IsPair),nameof(IsUseIt),nameof(IsTray),nameof(IsNotPaired),nameof(ShowPairingPanel),nameof(HasExistingDevices),nameof(HasConnectedDevice),nameof(HasPairSuccess),nameof(PairSuccessText),nameof(StartWithWindows),nameof(StartupState),nameof(TrayPauseText),nameof(TooltipExample),nameof(ShowSettingsFootnote)})Raise(p);((RelayCommand)BackCommand).Refresh();}
 public void Dispose(){if(disposed)return;disposed=true;shell.App.PropertyChanged-=AppChanged;Pairing.Dispose();}
}
