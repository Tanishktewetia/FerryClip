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
 readonly DispatcherTimer timer,undoTimer;
 bool disposed,undoVisible;
 string stickyGroup="";
 string search="",filter="All",undoText="";
 ClipboardHistory.Entry? undoEntry;
 readonly List<HistoryClipViewModel> clips=[];
 public HistoryViewModel(ShellViewModel shell)
 {
  this.shell=shell;
  AllCommand=new RelayCommand(()=>Filter="All"); PcCommand=new RelayCommand(()=>Filter="From PC"); PhoneCommand=new RelayCommand(()=>Filter="From phone");
  ClearCommand=new AsyncCommand(ClearAsync,shell.App.Report); EnableCommand=new RelayCommand(()=>{try{shell.App.UpdateSettings(shell.App.Settings with{History=shell.App.Settings.Policy with{Save=true}});}catch(Exception ex){shell.App.Report(ex);}});
  ResumeCommand=new RelayCommand(()=>{try{shell.App.SetGlobalPaused(false);}catch(Exception ex){shell.App.Report(ex);}}); UndoCommand=new AsyncCommand(UndoAsync,shell.App.Report);
  shell.App.PropertyChanged+=Changed;
  timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(1)};timer.Tick+=Tick;timer.Start();
  undoTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(7)};undoTimer.Tick+=ExpireUndo;Reload();
 }
 public ObservableCollection<object> Items{get;}=[];
 public string Search{get=>search;set{if(Set(ref search,value))Rebuild();}}
 public string Filter{get=>filter;private set{if(Set(ref filter,value)){Raise(nameof(AllSelected));Raise(nameof(PcSelected));Raise(nameof(PhoneSelected));Rebuild();}}}
 public bool AllSelected=>Filter=="All";public bool PcSelected=>Filter=="From PC";public bool PhoneSelected=>Filter=="From phone";
 public int AllCount=>clips.Count;public int PcCount=>clips.Count(c=>c.Direction=="From PC");public int PhoneCount=>clips.Count(c=>c.Direction=="From phone");
 public bool IsPaused=>shell.App.IsPaused;public bool IsOff=>!shell.App.Settings.Policy.Save;public bool HasError=>shell.App.HistoryError is not null;
 public bool HasRows=>VisibleCount>0;public bool ShowEmpty=>!HasRows;public bool CanClear=>clips.Count>0&&!HasError;
 public int VisibleCount{get;private set;}public string CountText=>$"{VisibleCount} of {shell.App.Settings.Policy.Limit} items";
 public string EmptyTitle=>HasError?"History is unavailable":IsOff?"Clipboard history is off":clips.Count==0?"No clipboard history yet":"No results";
 public string EmptyText=>HasError?shell.App.HistoryError!:IsOff?"Turn on history to save new clips. Existing clips are kept.":clips.Count==0?"Text you copy on your PC or receive from your phone will appear here.":"Try another search or choose a different filter.";
 public string StickyGroup{get=>stickyGroup;private set=>Set(ref stickyGroup,value);}
 public void SetStickyGroup(string title)=>StickyGroup=title;
 public bool UndoVisible{get=>undoVisible;private set=>Set(ref undoVisible,value);}public string UndoText{get=>undoText;private set=>Set(ref undoText,value);}
 public ICommand AllCommand{get;}public ICommand PcCommand{get;}public ICommand PhoneCommand{get;}public ICommand ClearCommand{get;}public ICommand EnableCommand{get;}public ICommand ResumeCommand{get;}public ICommand UndoCommand{get;}
 public bool IncludePinned{get;set;}
 public HistoryClipViewModel? FindClip(Guid id)=>clips.FirstOrDefault(c=>c.Id==id);
 public HistoryClipViewModel? AtVisibleIndex(int visibleIndex)
 {
  var rows=Items.OfType<HistoryClipViewModel>().ToArray();return rows.Length==0?null:rows[Math.Clamp(visibleIndex,0,rows.Length-1)];
 }
 public int VisibleRowCount=>Items.OfType<HistoryClipViewModel>().Count();
 public async Task CopyAsync(HistoryClipViewModel? clip){if(clip is not null)await clip.CopyAsync();}
 public async Task DeleteWithUndoAsync(HistoryClipViewModel? clip)
 {
  if(disposed||clip is null)return;
  try{await shell.App.DeleteClip(clip.Id);undoEntry=new ClipboardHistory.Entry(clip.Id,clip.Text,clip.RawDirection,clip.At,clip.Pinned,clip.Sensitive);UndoText="Clip deleted";UndoVisible=true;undoTimer.Stop();undoTimer.Start();}
  catch(Exception ex){shell.App.Report(ex);}
 }
 async Task UndoAsync(){if(disposed||undoEntry is not{} entry)return;undoTimer.Stop();try{await shell.App.RestoreHistoryEntry(entry);undoEntry=null;UndoVisible=false;shell.Notify("Clip restored");}catch(Exception ex){shell.App.Report(ex);}}
 void ExpireUndo(object? sender,EventArgs e){undoTimer.Stop();undoEntry=null;UndoVisible=false;}
 async Task ClearAsync(){if(disposed||!CanClear)return;IncludePinned=false;if(await shell.ConfirmAsync("Clear clipboard history?","Remove saved clips? Pinned clips are kept unless selected below.","Also remove pinned")){if(disposed)return;await shell.App.ClearHistory(shell.DialogOptionChecked);shell.Notify("Clipboard history cleared");}}
 void Changed(object? sender,PropertyChangedEventArgs e){if(disposed)return;if(e.PropertyName==nameof(shell.App.History))Reload();else if(e.PropertyName is "Settings" or "IsPaused" or "HistoryError")Rebuild();}
 void Reload(){foreach(var clip in clips)clip.Dispose();clips.Clear();clips.AddRange(shell.App.History.Select(entry=>new HistoryClipViewModel(entry,shell,this)));Raise(nameof(AllCount));Raise(nameof(PcCount));Raise(nameof(PhoneCount));Rebuild();}
 void Rebuild()
 {
  var matches=clips.Where(c=>(Filter=="All"||c.Direction==Filter)&&(string.IsNullOrWhiteSpace(Search)||(!c.Sensitive&&c.Text.Contains(Search,StringComparison.OrdinalIgnoreCase)))).OrderByDescending(c=>c.At).ToList();
  Items.Clear();VisibleCount=matches.Count;
  var today=DateTime.Today;foreach(var group in new[]{"Pinned","Today","Yesterday","Earlier"}){var rows=matches.Where(c=>(c.Pinned?"Pinned":c.At.LocalDateTime.Date==today?"Today":c.At.LocalDateTime.Date==today.AddDays(-1)?"Yesterday":"Earlier")==group).ToList();if(rows.Count==0)continue;Items.Add(new HistoryHeading(group));foreach(var row in rows)Items.Add(row);}
  foreach(var name in new[]{nameof(CountText),nameof(VisibleCount),nameof(IsPaused),nameof(IsOff),nameof(HasError),nameof(HasRows),nameof(ShowEmpty),nameof(EmptyTitle),nameof(EmptyText),nameof(CanClear),nameof(AllCount),nameof(PcCount),nameof(PhoneCount)})Raise(name);
 }
 void Tick(object? sender,EventArgs e){foreach(var clip in clips)clip.CheckDeadline();}
 public void Remask(){foreach(var clip in clips)clip.Remask();}
 public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Tick-=Tick;undoTimer.Stop();undoTimer.Tick-=ExpireUndo;shell.App.PropertyChanged-=Changed;Remask();Items.Clear();foreach(var clip in clips)clip.Dispose();clips.Clear();}
 public bool IsDisposed=>disposed;
}
public sealed record HistoryHeading(string Title);
public sealed class HistoryClipViewModel:ObservableObject,IDisposable
{
 readonly ClipboardHistory.Entry entry;readonly ShellViewModel shell;readonly HistoryViewModel owner;
 readonly DispatcherTimer copyTimer=new(){Interval=TimeSpan.FromMilliseconds(1500)};
 DateTimeOffset? deadline;bool expanded,canExpand,copied,disposed;
 public HistoryClipViewModel(ClipboardHistory.Entry entry,ShellViewModel shell,HistoryViewModel owner){this.entry=entry;this.shell=shell;this.owner=owner;CopyCommand=new RelayCommand(()=>_ = CopyAsync());PinCommand=new AsyncCommand(()=>shell.App.PinClip(entry.Id,!entry.Pinned),shell.App.Report);DeleteCommand=new RelayCommand(()=>_ = owner.DeleteWithUndoAsync(this));RevealCommand=new RelayCommand(()=>{deadline=deadline is null?DateTimeOffset.UtcNow.AddSeconds(10):null;NotifyPrivacy();});ExpandCommand=new RelayCommand(()=>{if(CanExpand){Expanded=!Expanded;Raise(nameof(Expanded));Raise(nameof(ExpandLabel));}});copyTimer.Tick+=CopyExpired;}
 public Guid Id=>entry.Id;public string Text=>entry.Text;public string RawDirection=>entry.Direction;public string Direction=>entry.Direction=="Received"?"From phone":"From PC";public bool FromPhone=>Direction=="From phone";public DateTimeOffset At=>entry.At;public bool Pinned=>entry.Pinned;public bool Sensitive=>entry.Sensitive;public bool Revealed=>deadline is{} until&&until>DateTimeOffset.UtcNow;public bool Expanded{get=>expanded;set{if(Set(ref expanded,value))Raise(nameof(ExpandLabel));}}
 public bool CanExpand=>canExpand&&(!Sensitive||Revealed);public bool IsCopied=>copied;public string CopyToolTip=>copied?"Copied":"Copy clip";public string RevealLabel=>Revealed?"Hide sensitive clip":"Reveal sensitive clip for 10 seconds";public string PinLabel=>Pinned?"Unpin clip":"Pin clip";public string ExpandLabel=>Expanded?"Collapse text":"Expand full text";
 public string Preview=>Sensitive&&!Revealed?"••••••••••••":ExpandedText.Replace('\r',' ').Replace('\n',' '); public string ExpandedText=>Sensitive&&!Revealed?"••••••••••••":Text;
 public string Metadata{get{var value=TimeFormatter.Format(At);return value.StartsWith("Today, ",StringComparison.Ordinal)?value[7..]:value;}}public string FullTime=>TimeFormatter.Full(At);

 public ICommand CopyCommand{get;}public ICommand PinCommand{get;}public ICommand DeleteCommand{get;}public ICommand RevealCommand{get;}public ICommand ExpandCommand{get;}
 public void SetCanExpand(bool value){if(canExpand==value)return;canExpand=value;Raise(nameof(CanExpand));}
 public async Task CopyAsync(){if(disposed)return;try{shell.App.CopyClip(Id);copied=true;copyTimer.Stop();copyTimer.Start();Raise(nameof(IsCopied));Raise(nameof(CopyToolTip));shell.Notify("Copied");}catch(Exception ex){shell.App.Report(ex);}}
 void CopyExpired(object? s,EventArgs e){copyTimer.Stop();copied=false;Raise(nameof(IsCopied));Raise(nameof(CopyToolTip));}
 public void Remask(){deadline=null;Expanded=false;NotifyPrivacy();}
 public void CheckDeadline(){Raise(nameof(Metadata));if(deadline is{} until&&until<=DateTimeOffset.UtcNow)Remask();}
 void NotifyPrivacy(){foreach(var p in new[]{nameof(Revealed),nameof(Preview),nameof(RevealLabel),nameof(CanExpand),nameof(Expanded)})Raise(p);}
 public void Dispose(){if(disposed)return;disposed=true;copyTimer.Stop();copyTimer.Tick-=CopyExpired;}
}
