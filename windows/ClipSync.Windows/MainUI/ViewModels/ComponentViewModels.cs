using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using ClipSync.Windows.Clipboard;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Services;
namespace ClipSync.Windows.MainUI.ViewModels;
public sealed class HistoryRowViewModel : ObservableObject
{
 readonly ClipboardHistory.Entry entry; bool revealed, pinned;
 public HistoryRowViewModel(ClipboardHistory.Entry entry,AppState app){this.entry=entry;pinned=entry.Pinned;CopyCommand=new RelayCommand(()=>{try{app.CopyClip(entry.Id);}catch(Exception ex){app.Report(ex);}});PinCommand=new AsyncCommand(async()=>{await app.PinClip(entry.Id,!pinned);pinned=!pinned;Raise(nameof(PinLabel));},app.Report);DeleteCommand=new AsyncCommand(()=>app.DeleteClip(entry.Id),app.Report);RevealCommand=new RelayCommand(()=>{revealed=!revealed;Raise(nameof(Preview));Raise(nameof(RevealLabel));});}
 public bool Sensitive=>entry.Sensitive;
 public string PinLabel=>pinned?"Unpin":"Pin";
 public string RevealLabel=>revealed?"Hide":"Reveal";
 public string Preview=>entry.Sensitive&&!revealed?"••••••••":entry.Text.Replace('\r',' ').Replace('\n',' ');
 public string Metadata=>$"{(entry.Direction=="Received"?"From phone":"From PC")} · {entry.At.LocalDateTime:g}";
 public ICommand CopyCommand{get;}public ICommand PinCommand{get;}public ICommand DeleteCommand{get;}public ICommand RevealCommand{get;}
}
public sealed class PairingViewModel : ObservableObject, IDisposable
{
 readonly DispatcherTimer timer; readonly RelayCommand copy; bool disposed;
 public AppState App{get;} public ICommand GenerateCommand{get;} public ICommand CopyCommand=>copy;
 public bool Active=>App.Pairing.Active;
 public bool Expired=>!Active&&App.Pairing.Deadline is not null;
 public string Code=>App.Pairing.DisplayCode;
 public string Countdown=>App.Pairing.Countdown;
 public string ButtonLabel=>Active?"Regenerate":"Generate code";
 public string Hint=>Active?"Enter this code on your phone":"Creates a six-digit code to pair your phone";
 public bool IsPrimary=>Components.PrimaryActionPolicy.Choose(App.State,Active)=="Generate code";
 void Updated(object? s,PropertyChangedEventArgs e)=>Tick();
 void Tick(){foreach(var p in new[]{nameof(Active),nameof(Expired),nameof(Code),nameof(Countdown),nameof(ButtonLabel),nameof(Hint),nameof(IsPrimary)})Raise(p);copy.Refresh();}
 public void Dispose(){if(disposed)return;disposed=true;timer.Stop();App.PropertyChanged-=Updated;}
 public PairingViewModel(AppState app){App=app;GenerateCommand=new RelayCommand(()=>{try{app.BeginPairing();}catch(Exception ex){app.Report(ex);}});copy=new RelayCommand(()=>{try{app.CopyPairingCode();}catch(Exception ex){app.Report(ex);}},()=>Active);timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(1)};timer.Tick+=(_,_)=>Tick();App.PropertyChanged+=Updated;timer.Start();}
}
