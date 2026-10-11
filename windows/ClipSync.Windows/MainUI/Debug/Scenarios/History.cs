#if DEBUG
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipSync.Windows.Clipboard;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.Services;
using ClipSync.Windows.MainUI.Pages.History;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Debug.Scenarios;
public static class History
{
 public static void Register(ShellViewModel shell)
 {
  shell.Navigation.Register("History",()=>new HistoryPage(shell));
  if(Environment.GetEnvironmentVariable("FERRYCLIP_HISTORY_TESTS") is not {} output)return;
  var frame=new DispatcherFrame();Exception? error=null;
  async void Start(){try{await Run(output);}catch(Exception ex){error=ex;}finally{frame.Continue=false;}}
  System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(Start));Dispatcher.PushFrame(frame);
  if(error is not null)throw new InvalidOperationException("History scenario failed",error);
 }
 static void Assert(bool condition,string text){if(!condition)throw new Exception(text);}
 static async Task Settle(Window window){window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Task.Delay(150);window.UpdateLayout();}
 static void Capture(Window window,string output)
 {
  var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(output);encoder.Save(stream);
 }
 public static async Task Run(string output)
 {
  Directory.CreateDirectory(output);var temp=Path.Combine(Path.GetTempPath(),"FerryClip-history-scenario-"+Guid.NewGuid());Directory.CreateDirectory(temp);
  var errors=new List<string>();var results=new List<string>();
  try
  {
   foreach(var count in new[]{0,14,100,500})
   {
    var file=Path.Combine(temp,count+".dat");var entries=Enumerable.Range(0,count).Select(i=>new ClipboardHistory.Entry(Guid.NewGuid(),i==2?string.Concat(Enumerable.Repeat("Long Unicode text 👩🏽‍💻 漢字\n",1200)):"Synthetic history clip "+i,i%2==0?"Copied":"Received",DateTimeOffset.Now.AddDays(-(i%3)),i==0,i==1)).ToArray();
    File.WriteAllBytes(file,ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(entries),null,DataProtectionScope.CurrentUser));
    var backend=new PreviewBackend();backend.Switch("connected");var store=new ClipboardHistory(file,ex=>errors.Add(ex.ToString()));using var app=new AppState(backend,store,System.Windows.Application.Current.Dispatcher);app.UpdateSettings(app.Settings with{History=new(true,500,0,false)});await app.ReloadHistoryAsync();
    using var shell=new ShellViewModel(app);shell.Navigation.Register("History",()=>new HistoryPage(shell));shell.Navigate("History");var window=new MainWindow(shell){ShowInTaskbar=false};window.Show();await Settle(window);
    var page=(HistoryPage)shell.Navigation.Content!;
    Assert(page.Model.VisibleCount==count,"Count "+count);
    if(count==500){var list=MainWindow.Descendants<System.Windows.Controls.ListBox>(window).Single();var realized=MainWindow.Descendants<System.Windows.Controls.ListBoxItem>(list).Count();Assert(realized<50,"Virtualization realized "+realized);results.Add("PASS virtualization: 500 clips, "+realized+" realized containers");}
    for(int cycle=0;cycle<50;cycle++)
    {
     page=(HistoryPage)shell.Navigation.Content!;shell.Navigate("Dashboard");Assert(page.Model.IsDisposed,"Page disposal");shell.Navigate("History");await Settle(window);Assert(shell.Navigation.Content is HistoryPage,"Real history route");
    }
    page=(HistoryPage)shell.Navigation.Content!;shell.FocusHistorySearch();await Settle(window);page=(HistoryPage)shell.Navigation.Content!;Assert(page.IsKeyboardFocusWithin,"Ctrl+F focus target");
    foreach(var size in new[]{(1100,720),(900,600)})
    {
     window.Width=size.Item1;window.Height=size.Item2;await Settle(window);Capture(window,Path.Combine(output,$"history-{count}-{size.Item1}x{size.Item2}.png"));
     if(count==14)
     {
      page.Model.Search="not-a-clip";await Settle(window);Assert(page.Model.EmptyTitle=="No results","No results");Capture(window,Path.Combine(output,$"noresults-{size.Item1}x{size.Item2}.png"));page.Model.Search="";
      app.UpdateSettings(app.Settings with{History=app.Settings.Policy with{Save=false}});await Settle(window);Capture(window,Path.Combine(output,$"off-retained-{size.Item1}x{size.Item2}.png"));app.UpdateSettings(app.Settings with{History=app.Settings.Policy with{Save=true}});
      app.SetGlobalPaused(true);await Settle(window);Capture(window,Path.Combine(output,$"paused-{size.Item1}x{size.Item2}.png"));app.SetGlobalPaused(false);
     }
    }
    if(count>0)
    {
     var row=page.Model.Items.OfType<HistoryClipViewModel>().First();row.CopyCommand.Execute(null);Assert(backend.LastCopied==row.Text,"Copy action");
     page.Model.PcCommand.Execute(null);Assert(page.Model.Items.OfType<HistoryClipViewModel>().All(r=>r.Direction=="From PC"),"Direction filter");page.Model.AllCommand.Execute(null);
     var sensitive=page.Model.Items.OfType<HistoryClipViewModel>().First(r=>r.Sensitive);Assert(sensitive.Preview.StartsWith("•"),"Initial mask");sensitive.RevealCommand.Execute(null);Assert(sensitive.Revealed,"Reveal");await Task.Delay(10300);Assert(!sensitive.Revealed,"10s remask");sensitive.RevealCommand.Execute(null);page.Model.Remask();Assert(!sensitive.Revealed,"Leave remask");
     var longRow=page.Model.Items.OfType<HistoryClipViewModel>().First(r=>r.Text.Length>1000);shell.DismissToastCommand.Execute(null);page.Model.Search="Long Unicode";await Settle(window);longRow=page.Model.Items.OfType<HistoryClipViewModel>().Single();longRow.ExpandCommand.Execute(null);Assert(longRow.Expanded,"Long Unicode expansion");await Settle(window);Capture(window,Path.Combine(output,$"expanded-{count}-900x600.png"));longRow.ExpandCommand.Execute(null);page.Model.Search="";
     var clear=page.Model.ClearCommand;clear.Execute(null);await Settle(window);Assert(shell.DialogOpen,"Clear confirms");shell.CancelCommand.Execute(null);await Settle(window);Assert(app.History.Count==count,"Cancel preserves history");
    }
    results.Add($"PASS {count}: actual HistoryPage 50 open/leave cycles; both viewport captures; count, focus, disposal"+(count>0?"; copy/filter/mask/remask10s/long Unicode/clear cancel":""));window.Close();
   }
   Assert(errors.Count==0,string.Join("\n",errors));File.WriteAllLines(Path.Combine(output,"history-tests.txt"),results);
  }
  finally {Directory.Delete(temp,true);}
 }
}
#endif
