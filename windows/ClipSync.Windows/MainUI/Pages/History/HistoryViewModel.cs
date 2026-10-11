using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using ClipSync.Windows.Clipboard;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.History;

public sealed class HistoryViewModel : ObservableObject, IDisposable
{
 readonly ShellViewModel shell;
 readonly DispatcherTimer timer;
 bool disposed;
 string search="", filter="All";
 readonly List<HistoryClipViewModel> clips=[];
 public HistoryViewModel(ShellViewModel shell)
 {
  this.shell=shell;
  AllCommand=new RelayCommand(()=>Filter="All");
  PcCommand=new RelayCommand(()=>Filter="From PC");
  PhoneCommand=new RelayCommand(()=>Filter="From phone");
  ClearCommand=new AsyncCommand(ClearAsync,shell.App.Report);
  EnableCommand=new RelayCommand(()=>{try{shell.App.UpdateSettings(shell.App.Settings with {History=shell.App.Settings.Policy with {Save=true}});}catch(Exception ex){shell.App.Report(ex);}});
  ResumeCommand=new RelayCommand(()=>{try{shell.App.SetGlobalPaused(false);}catch(Exception ex){shell.App.Report(ex);}});
  shell.App.PropertyChanged+=Changed;
  timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(250)};
  timer.Tick+=Tick;timer.Start();Reload();
 }
 public ObservableCollection<object> Items {get;}=[];
 public string Search {get=>search;set{if(Set(ref search,value))Rebuild();}}
 public string Filter {get=>filter;private set{if(Set(ref filter,value)){Raise(nameof(AllSelected));Raise(nameof(PcSelected));Raise(nameof(PhoneSelected));Rebuild();}}}
 public bool AllSelected=>Filter=="All";
 public bool PcSelected=>Filter=="From PC";
 public bool PhoneSelected=>Filter=="From phone";
 public bool IsPaused=>shell.App.IsPaused;
 public bool IsOff=>!shell.App.Settings.Policy.Save;
 public bool HasError=>shell.App.HistoryError is not null;
 public bool HasRows=>VisibleCount>0;
 public bool ShowEmpty=>!HasRows;
 public bool CanClear=>clips.Count>0&&!HasError;
 public int VisibleCount {get;private set;}
 public string CountText=>$"{VisibleCount} of {shell.App.Settings.Policy.Limit} items";
 public string EmptyTitle=>HasError?"History is unavailable":IsOff?"Clipboard history is off":clips.Count==0?"No clipboard history yet":"No results";
 public string EmptyText=>HasError?shell.App.HistoryError!:IsOff?"Turn on history to save new clips. Existing clips are kept.":clips.Count==0?"Text you copy on your PC or receive from your phone will appear here.":"Try another search or choose a different filter.";
 public ICommand AllCommand {get;} public ICommand PcCommand {get;} public ICommand PhoneCommand {get;}
 public ICommand ClearCommand {get;} public ICommand EnableCommand {get;} public ICommand ResumeCommand {get;}
 // This option is exposed for the lead-owned confirmation checkbox integration.
 public bool IncludePinned {get;set;}
 async Task ClearAsync()
 {
  if(disposed||!CanClear)return;
  IncludePinned=false;
  if(await shell.ConfirmAsync("Clear clipboard history?","Remove saved clips? Pinned clips are kept unless selected below.","Also remove pinned"))
  {if(disposed)return;await shell.App.ClearHistory(shell.DialogOptionChecked);shell.Notify("Clipboard history cleared");}
 }
 void Changed(object? sender,PropertyChangedEventArgs e)
 {
  if(disposed)return;
  if(e.PropertyName==nameof(shell.App.History))Reload();
  else if(e.PropertyName is "Settings" or "IsPaused" or "HistoryError")Rebuild();
 }
 void Reload(){foreach(var clip in clips)clip.Remask();clips.Clear();clips.AddRange(shell.App.History.Select(e=>new HistoryClipViewModel(e,shell)));Rebuild();}
 void Rebuild()
 {
  Items.Clear();var matches=clips.Where(c=>(Filter=="All"||c.Direction==Filter)&&
   (string.IsNullOrWhiteSpace(Search)||(!c.Sensitive&&c.Text.Contains(Search,StringComparison.OrdinalIgnoreCase)))).OrderByDescending(c=>c.At).ToList();
  VisibleCount=matches.Count;
  var today=DateTime.Today;
  foreach(var group in new[]{"Pinned","Today","Yesterday","Earlier"})
  {
   var rows=matches.Where(c=>(c.Pinned?"Pinned":c.At.LocalDateTime.Date==today?"Today":c.At.LocalDateTime.Date==today.AddDays(-1)?"Yesterday":"Earlier")==group).ToList();
   if(rows.Count==0)continue;Items.Add(new HistoryHeading(group));foreach(var row in rows)Items.Add(row);
  }
  foreach(var name in new[]{nameof(CountText),nameof(VisibleCount),nameof(IsPaused),nameof(IsOff),nameof(HasError),nameof(HasRows),nameof(ShowEmpty),nameof(EmptyTitle),nameof(EmptyText),nameof(CanClear)})Raise(name);
 }
 void Tick(object? sender,EventArgs e){foreach(var clip in clips)clip.CheckDeadline();}
 public void Remask(){foreach(var clip in clips)clip.Remask();}
 public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Tick-=Tick;shell.App.PropertyChanged-=Changed;Remask();Items.Clear();clips.Clear();}
 public bool IsDisposed=>disposed;
}
public sealed record HistoryHeading(string Title);
public sealed class HistoryClipViewModel : ObservableObject
{
 readonly ClipboardHistory.Entry entry;
 DateTimeOffset? deadline;
 bool expanded;
 public HistoryClipViewModel(ClipboardHistory.Entry entry,ShellViewModel shell)
 {
  this.entry=entry;
  CopyCommand=new RelayCommand(()=>{try{shell.App.CopyClip(entry.Id);shell.Notify("Copied to clipboard");}catch(Exception ex){shell.App.Report(ex);}});
  PinCommand=new AsyncCommand(()=>shell.App.PinClip(entry.Id,!entry.Pinned),shell.App.Report);
  DeleteCommand=new AsyncCommand(async()=>{if(await shell.ConfirmAsync("Delete this clip?","This removes the saved clip from history."))await shell.App.DeleteClip(entry.Id);},shell.App.Report);
  RevealCommand=new RelayCommand(()=>{deadline=deadline is null?DateTimeOffset.UtcNow.AddSeconds(10):null;NotifyPrivacy();});
  ExpandCommand=new RelayCommand(()=>{if(CanExpand){expanded=!expanded;Raise(nameof(Expanded));Raise(nameof(Preview));Raise(nameof(ExpandLabel));}});
 }
 public Guid Id=>entry.Id; public string Text=>entry.Text; public DateTimeOffset At=>entry.At;
 public bool Pinned=>entry.Pinned; public bool Sensitive=>entry.Sensitive;
 public bool Revealed=>deadline is not null;
 public bool Expanded=>expanded;
 public bool CanExpand=>(!Sensitive||Revealed)&&(Text.Length>110||Text.Contains('\n'));
 public string Preview=>Sensitive&&!Revealed?"••••••••••••":expanded?Text:Text.Replace('\r',' ').Replace('\n',' ');
 public string Direction=>entry.Direction=="Received"?"From phone":"From PC";
 public bool FromPhone=>Direction=="From phone";
 public string Metadata=>At.LocalDateTime.ToString("g");
 public string RevealLabel=>Revealed?"Hide sensitive clip":"Reveal sensitive clip for 10 seconds";
 public string PinLabel=>Pinned?"Unpin clip":"Pin clip";
 public string ExpandLabel=>Expanded?"Collapse text":"Expand text";
 public ICommand CopyCommand{get;}public ICommand PinCommand{get;}public ICommand DeleteCommand{get;}public ICommand RevealCommand{get;}public ICommand ExpandCommand{get;}
 public void Remask(){deadline=null;expanded=false;NotifyPrivacy();}
 public void CheckDeadline(){if(deadline<=DateTimeOffset.UtcNow)Remask();}
 void NotifyPrivacy(){foreach(var p in new[]{nameof(Revealed),nameof(Preview),nameof(RevealLabel),nameof(CanExpand),nameof(Expanded)})Raise(p);}
}
