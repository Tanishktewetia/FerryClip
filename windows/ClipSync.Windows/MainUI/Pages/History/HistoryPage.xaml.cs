using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows;
using System.Windows.Input;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.History;
public partial class HistoryPage : System.Windows.Controls.UserControl, IDisposable
{
 public HistoryViewModel Model {get;}
 readonly ShellViewModel shell;
 bool disposed;
 public HistoryPage(ShellViewModel shell)
 {
  this.shell=shell;InitializeComponent();shell.HistorySearchRequested+=Requested;Model=new HistoryViewModel(shell);DataContext=Model;
  Unloaded+=Leaving;
  AddHandler(Keyboard.PreviewKeyDownEvent,new System.Windows.Input.KeyEventHandler(SearchShortcut),true);
 }
 void Requested()=>Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>{if(!disposed)FocusSearch();}));
 void Leaving(object sender,RoutedEventArgs e)=>Model.Remask();
 void SearchShortcut(object sender,KeyEventArgs e){if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.F){FocusSearch();e.Handled=true;}}
 public void FocusSearch(){SearchBox.Focus();SearchBox.SelectAll();}
 public void Dispose(){if(disposed)return;disposed=true;shell.HistorySearchRequested-=Requested;Unloaded-=Leaving;Model.Dispose();DataContext=null;}
}
