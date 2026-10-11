#if DEBUG
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using ClipSync.Windows.Clipboard;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.Services;
using ClipSync.Windows.MainUI.ViewModels;
using ClipSync.Windows.MainUI.Components;
namespace ClipSync.Windows.MainUI.Debug;
public sealed class PreviewBackend : IAppBackend
{
 public event Action? Changed;
 public IReadOnlyList<DeviceSnapshot> Devices{get;private set;}=[];
 bool paused; public bool Paused{get=>paused;set{paused=value;Changed?.Invoke();}}
 public PairingSnapshot Pairing{get;private set;}=new(null,null);
 public bool StartupEnabled=>false;
 public void Switch(string state){paused=state=="paused";Devices=state=="no-devices"?[]:[new("fixture-phone","Preview phone",state!="unreachable",true,"[::ffff:192.168.1.37]:48546",DateTimeOffset.UtcNow.AddMinutes(-8),DateTimeOffset.UtcNow.AddMinutes(-8))];Pairing=state=="pairing-active"?new("583914",DateTimeOffset.UtcNow.AddMinutes(5)):state=="pairing-expired"?new(null,DateTimeOffset.UtcNow.AddMinutes(-1)):new(null,null);Changed?.Invoke();}
 public void SetStartup(bool enabled)=>throw new NotSupportedException("Preview never writes registry");
 public void ExpireSoon(){Pairing=new("583914",DateTimeOffset.UtcNow.AddMilliseconds(1100));Changed?.Invoke();}
 public void BeginPairing(){Pairing=new("583914",DateTimeOffset.UtcNow.AddMinutes(5));Changed?.Invoke();}
 public void SetDeviceSync(string id,bool enabled){Devices=Devices.Select(d=>d.Id==id?d with{SyncEnabled=enabled}:d).ToArray();Changed?.Invoke();}
 public void Rename(string id,string name){Devices=Devices.Select(d=>d.Id==id?d with{Name=name}:d).ToArray();Changed?.Invoke();}
 public void Forget(string id){Devices=Devices.Where(d=>d.Id!=id).ToArray();Changed?.Invoke();}
 public string? LastCopied {get;private set;} public bool Copy(string text){LastCopied=text;return true;}public void Dispose(){}
}
public static class PreviewRunner
{
 public static readonly string[] States=["connected","paused","unreachable","no-devices","pairing-idle","pairing-active","pairing-expired","history-empty","history-off","history-100","paused-populated","masked-populated"];
 sealed class BindingListener : TraceListener {public List<string> Errors{get;}=[];public override void Write(string? s){if(!string.IsNullOrWhiteSpace(s))Errors.Add(s);} public override void WriteLine(string? s)=>Write(s);}
 public static bool TryStart(System.Windows.Application app,string[] args)
 {
  if(args.Length==0 || args[0] is not ("--screenshot" or "--smoke-main-ui" or "--gallery"))return false;
  app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  app.Dispatcher.BeginInvoke(new Action(async()=>{try{await Run(app,args);}catch(Exception ex){Console.Error.WriteLine(ex);app.Shutdown(1);}}));return true;
 }
 sealed class DisposalProbe(Action disposed):IDisposable {public void Dispose()=>disposed();}
 static async Task Settle(Window w){w.UpdateLayout();await w.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(120);w.UpdateLayout();}
 static void Capture(Window w,string output){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(output);png.Save(stream);}
 public static FrameworkElement Gallery(ShellViewModel vm,PreviewBackend backend)
 {
  var panel=new StackPanel();void Add(FrameworkElement control){control.Margin=new Thickness(0,0,0,12);panel.Children.Add(control);}
  var statusVm=new StatusCardViewModel(vm.App);var statusCard=new StatusCard(statusVm);statusCard.Unloaded+=(_,_)=>statusVm.Dispose();Add(statusCard);
  var states=new ComboBox{ItemsSource=States,SelectedItem=vm.App.IsPaused?"paused":vm.App.Pairing.Active?"pairing-active":"connected",Width=240,HorizontalAlignment=HorizontalAlignment.Left};states.SelectionChanged+=(_,_)=>{backend.Switch((string)states.SelectedItem);vm.App.Refresh();};Add(states);
  var row=new StackPanel{Orientation=Orientation.Horizontal};foreach(var label in new[]{"Primary","Secondary","Disabled","Primary disabled","Confirm dialog","Toast"}){var b=new Button{Content=label,Margin=new Thickness(0,0,8,0),IsEnabled=label is not ("Disabled" or "Primary disabled")};if(label is "Primary" or "Primary disabled"){b.Content=label;b.Style=(Style)System.Windows.Application.Current.MainWindow.FindResource("PrimaryButton");b.Click+=(_,_)=>vm.Notify("Gallery action completed");}if(label=="Secondary")b.Click+=(_,_)=>vm.Notify("Secondary action completed");if(label=="Confirm dialog")b.Click+=async(_,_)=>await vm.ConfirmAsync("Confirm action","This isolated gallery never changes live data.");if(label=="Toast")b.Click+=(_,_)=>vm.Notify("Preview notification");row.Children.Add(b);}Add(row);
  Add(new ScrollViewer{Width=320,Height=46,HorizontalAlignment=HorizontalAlignment.Left,HorizontalScrollBarVisibility=ScrollBarVisibility.Visible,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=new TextBlock{Text=new string('W',100)}});Add(new TextBox{Style=(Style)System.Windows.Application.Current.MainWindow.FindResource("MainTextBox"),Text="Editable text / keyboard focus",Width=320,Height=44,HorizontalAlignment=HorizontalAlignment.Left});Add(new CheckBox{Content="Save history",IsChecked=true});Add(new ToggleButton{Content="Selected toggle",IsChecked=true,Width=180,HorizontalAlignment=HorizontalAlignment.Left});Add(new Button{Content="⧉",HorizontalAlignment=HorizontalAlignment.Left,Style=(Style)System.Windows.Application.Current.MainWindow.FindResource("IconButton"),Command=new Infrastructure.RelayCommand(()=>vm.Notify("Copy preview action")),ToolTip="Copy"});var pairingVm=new PairingViewModel(vm.App);var pairing=new PairingPanel{DataContext=pairingVm};pairing.Unloaded+=(_,_)=>pairingVm.Dispose();Add(pairing);Add(new DeviceRow(new("preview","Preview phone",true,true,null,null,null)));foreach(var clip in vm.App.History)Add(new HistoryRow{DataContext=new HistoryRowViewModel(clip,vm.App)});Add(new EmptyState("No clipboard history","New clips will appear here when history is enabled."));
  return new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 }
 static void RegisterPageScenarios(ShellViewModel shell)
 {
  foreach(var type in typeof(PreviewRunner).Assembly.GetTypes().Where(t=>t.Namespace=="ClipSync.Windows.MainUI.Debug.Scenarios"))
  {
   var register=type.GetMethod("Register",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static,null,new[]{typeof(ShellViewModel)},null);
   register?.Invoke(null,new object[]{shell});
  }
 }
 static async Task Run(System.Windows.Application app,string[] args)
 {
  bool smoke=args[0]=="--smoke-main-ui",gallery=args[0]=="--gallery";
  string page=smoke||gallery?"Dashboard":args.ElementAtOrDefault(1)??throw new ArgumentException("Missing page");string state=smoke||gallery?"connected":args.ElementAtOrDefault(2)??"";
  if(!ShellViewModel.PageNames.Contains(page)&&page!="gallery")throw new ArgumentException("Unknown page");if(!States.Contains(state))throw new ArgumentException("Unknown state");
  double width=1100,height=720;var sizeIndex=Array.IndexOf(args,"--size");if(sizeIndex>=0){var parts=args[sizeIndex+1].Split('x');width=int.Parse(parts[0]);height=int.Parse(parts[1]);if(width<900||height<600)throw new ArgumentException("Minimum is 900x600");}
  var temp=Path.Combine(Path.GetTempPath(),"FerryClip-preview-"+Guid.NewGuid());var listener=new BindingListener();var exceptions=new List<string>();System.Windows.Threading.DispatcherUnhandledExceptionEventHandler uiError=(_,e)=>exceptions.Add(e.Exception.ToString());EventHandler<UnobservedTaskExceptionEventArgs> taskError=(_,e)=>exceptions.Add(e.Exception.ToString());app.DispatcherUnhandledException+=uiError;TaskScheduler.UnobservedTaskException+=taskError;PresentationTraceSources.DataBindingSource.Listeners.Add(listener);PresentationTraceSources.DataBindingSource.Switch.Level=SourceLevels.Error;
  try { var backend=new PreviewBackend();backend.Switch(state=="paused-populated"?"paused":state=="masked-populated"?"connected":state);var history=new ClipboardHistory(Path.Combine(temp,"history.dat"), ex=>exceptions.Add(ex.ToString()));using var facade=new AppState(backend,history,app.Dispatcher);var vm=new ShellViewModel(facade);RegisterPageScenarios(vm);vm.Navigate(page=="gallery"?"Dashboard":page);var window=new MainWindow(vm){Width=width,Height=height,ShowInTaskbar=false};app.MainWindow=window;window.Show();await Settle(window);
  if(state=="history-off")facade.UpdateSettings(facade.Settings with{History=new(false,100,24,true)});if(state=="history-100"){history.Configure(new(true,100,0,false));for(int i=0;i<100;i++)await history.RecordAsync("Synthetic clip "+i,"Copied");await facade.ReloadHistoryAsync();}if(state is "paused-populated" or "masked-populated")
  {
   history.Configure(new(true,100,0,false));
   await history.RecordAsync("https://ferryclip.app/docs.html","Copied");
   await history.RecordAsync("Meeting notes: discuss clipboard sync and local network improvements.","Received");
   await history.RecordAsync("Sensitive preview value","Received",true);
   await facade.ReloadHistoryAsync();
  }
  if(gallery||page=="gallery"){await history.RecordAsync("Private preview phrase","Received",true);history.Configure(new(true,100,0,false));await history.RecordAsync("Sensitive fixture — reveal to view","Received",true);await facade.ReloadHistoryAsync();window.SetPreviewContent(Gallery(vm,backend));}
  if(smoke){
  TimeFormatterTests.Run();
  using(var settingsVm=new Pages.Settings.SettingsViewModel(vm))
  {
   settingsVm.CloseToTray=false;if(facade.Settings.CloseToTray)throw new Exception("Close-to-tray setting");
   settingsVm.HistoryLimit=500;settingsVm.Retention="Never";settingsVm.SaveHistory=false;settingsVm.SkipSensitive=false;
   if(facade.Settings.Policy!=new HistoryPolicy(false,500,0,false))throw new Exception("Settings history policy");
   settingsVm.PauseAll=true;if(!facade.IsPaused)throw new Exception("Settings pause");settingsVm.PauseAll=false;
   settingsVm.SaveHistory=true;settingsVm.HistoryLimit=100;settingsVm.Retention="After 24 hours";settingsVm.SkipSensitive=true;
  }
  var optionConfirm=vm.ConfirmAsync("Clear test","Isolated option","Also remove pinned");vm.DialogOptionChecked=true;vm.ConfirmCommand.Execute(null);if(!await optionConfirm||!vm.DialogOptionChecked)throw new Exception("Pinned confirmation option");
  if(Pages.Devices.DeviceCardViewModel.PlainIPv4("[::ffff:192.168.1.37]:48546")!="192.168.1.37")throw new Exception("Plain IPv4 normalization");
  window.SetPreviewContent(Gallery(vm,backend));await Settle(window);await NativeInput.Run(window,kind=>Capture(window,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args.ElementAtOrDefault(1)??temp+"/smoke.txt"))!,"native-"+kind+".png")));window.SetPreviewContent(null);Keyboard.ClearFocus();await HistoryPolicyTests.Run(temp);using(var statusModel=new StatusCardViewModel(facade)){backend.Switch("connected");facade.Refresh();if(statusModel.IsPrimary||statusModel.ActionLabel!="Pause sync")throw new Exception("Connected status action policy");statusModel.ActionCommand.Execute(null);if(!facade.IsPaused||!statusModel.IsPrimary||statusModel.ActionLabel!="Resume sync")throw new Exception("Paused status action policy");statusModel.ActionCommand.Execute(null);if(facade.IsPaused)throw new Exception("Resume action failed");}var created=0;var disposedPages=0;vm.Navigation.Register("History",()=>{created++;return new DisposalProbe(()=>disposedPages++);});vm.Navigate("History");vm.Navigate("Dashboard");if(created!=1||disposedPages!=1)throw new Exception("Factory lifecycle failed");using(var pairingVm=new PairingViewModel(facade)){backend.Switch("pairing-active");facade.Refresh();pairingVm.CopyCommand.Execute(null);if(backend.LastCopied!="583914")throw new Exception("Pair code copy failed");backend.ExpireSoon();facade.Refresh();await Task.Delay(2200);if(pairingVm.Active||!pairingVm.Expired||pairingVm.CopyCommand.CanExecute(null))throw new Exception("Pair expiry failed");}if(Keyboard.FocusedElement is System.Windows.Controls.Button)throw new Exception("Unexpected button autofocus");foreach(var name in ShellViewModel.PageNames){vm.Navigate(name);await Settle(window);}for(int i=0;i<50;i++){vm.Navigate("History");vm.Navigate("Dashboard");}foreach(var s in States){backend.Switch(s);facade.Refresh();await Settle(window);if(string.IsNullOrEmpty(PrimaryActionPolicy.Choose(facade.State,facade.Pairing.Active)))throw new Exception("Primary policy failed");}foreach(var w in new[]{900,999,1000,1100,1400}){window.Width=w;window.Height=600;await Settle(window);}window.Activate();var navButton=MainWindow.Descendants<Button>(window).First(b=>Equals(b.ToolTip,"Dashboard"));navButton.Focus();if(!navButton.IsKeyboardFocused)throw new Exception("Native WPF keyboard focus unavailable");var key=new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,Key.Tab){RoutedEvent=Keyboard.PreviewKeyDownEvent};window.RaiseEvent(key);var confirmation=vm.ConfirmAsync("Test","Focus and cancellation");await Settle(window);if(!MainWindow.Descendants<StackPanel>(window).Any(p=>p.Name=="DialogHost"&&p.IsKeyboardFocusWithin))throw new Exception("Keyboard dialog focus failed");vm.CancelCommand.Execute(null);if(await confirmation)throw new Exception("Dialog cancel failed");if(!navButton.IsKeyboardFocused)throw new Exception("Keyboard focus restore failed");await history.RecordAsync("fixture clip","Copied");await facade.ReloadHistoryAsync();var rowVm=new HistoryRowViewModel(facade.History.Single() with{Sensitive=true},facade);if(rowVm.Preview!="••••••••")throw new Exception("Sensitive masking failed");rowVm.RevealCommand.Execute(null);if(rowVm.Preview!="fixture clip")throw new Exception("Sensitive reveal failed");rowVm.CopyCommand.Execute(null);if(backend.LastCopied!="fixture clip")throw new Exception("History copy failed");await facade.PinClip(facade.History.Single().Id,true);await facade.ClearHistory();if(facade.History.Count!=1)throw new Exception("Pinned clear failed");await facade.ClearHistory(true);if(facade.History.Count!=0)throw new Exception("Clear all failed");if(exceptions.Count>0)throw new Exception(string.Join("\\n",exceptions));if(listener.Errors.Count>0)throw new Exception(string.Join("\n",listener.Errors));var output=args.ElementAtOrDefault(1)??Path.Combine(temp,"smoke.txt");Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,"PASS: six routes; 50 history cycles; ten states; five widths; dialog cancellation; isolated encrypted pin/clear; zero binding errors; history legacy 0/14/100/500, caps 50/100/500, concurrent record, unreadable preservation, retention, long Unicode, skip/save policy; active copy and expired copy disabled.\nNative physical hover/pressed/reverse-tab and dialog reverse-tab/Escape; native WPF focus/restore; factory dispose; sensitive reveal/copy; deterministic removed-monitor/negative-monitor/nonfinite recovery; injected Windows format boundary verified. Actual mixed-DPI hardware and real clipboard ownership are not simulated.");}
  else if(!gallery){await Settle(window);if(exceptions.Count>0)throw new Exception(string.Join("\\n",exceptions));if(listener.Errors.Count>0)throw new Exception(string.Join("\n",listener.Errors));var target=args.ElementAtOrDefault(3)??throw new ArgumentException("Missing PNG path");Capture(window,target);if(page=="Settings"){var settingsScroll=MainWindow.Descendants<ScrollViewer>(window).First(v=>v.Content is StackPanel);settingsScroll.ScrollToEnd();await Settle(window);Capture(window,Path.Combine(Path.GetDirectoryName(target)!,Path.GetFileNameWithoutExtension(target)+"-bottom.png"));}if(page=="gallery"){var text=MainWindow.Descendants<TextBox>(window).First();text.Focus();await Settle(window);Capture(window,Path.Combine(Path.GetDirectoryName(target)!,Path.GetFileNameWithoutExtension(target)+"-focus.png"));var scroll=MainWindow.Descendants<ScrollViewer>(window).First(v=>v.Content is StackPanel);scroll.ScrollToEnd();await Settle(window);Capture(window,Path.Combine(Path.GetDirectoryName(target)!,Path.GetFileNameWithoutExtension(target)+"-bottom.png"));}}
  else {var done=new TaskCompletionSource();window.Closed+=(_,_)=>done.TrySetResult();await done.Task;}
  window.Close();app.Shutdown(0); } finally {PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);app.DispatcherUnhandledException-=uiError;TaskScheduler.UnobservedTaskException-=taskError;if(Directory.Exists(temp))Directory.Delete(temp,true);}
 }
}
#endif
