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
using System.ComponentModel;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.Services;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI;
public partial class MainWindow : Window
{
 bool exiting;readonly ShellViewModel vm;readonly SettingsStore? persistence;IInputElement? restoreFocus;bool keyboard;
 public MainWindow(ShellViewModel vm,SettingsStore? persistence=null){InitializeComponent();this.vm=vm;this.persistence=persistence;DataContext=vm;SizeChanged+=(_,_)=>ResizeSidebar();Loaded+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{FocusManager.SetFocusedElement(this,null);Keyboard.ClearFocus();}),System.Windows.Threading.DispatcherPriority.ApplicationIdle);PreviewKeyDown+=KeyPressed;PreviewMouseDown+=(_,e)=>{keyboard=false;if(vm.DialogOpen&&!DialogHost.IsMouseOver)e.Handled=true;};vm.PropertyChanged+=VmChanged;Closing+=OnClosing;Closed+=(_,_)=>{vm.PropertyChanged-=VmChanged;vm.Dispose();};SourceInitialized+=(_,_)=>{var source=(HwndSource)PresentationSource.FromVisual(this);source.AddHook(ChromeHook);};RestorePlacement();}
 void VmChanged(object? s,PropertyChangedEventArgs e){if(e.PropertyName==nameof(ShellViewModel.DialogOpen)){if(vm.DialogOpen){restoreFocus=Keyboard.FocusedElement;Dispatcher.BeginInvoke(new Action(()=>{if(vm.DialogOpen)CancelButton.Focus();}),System.Windows.Threading.DispatcherPriority.Input);}else if(keyboard&&restoreFocus is not null)Keyboard.Focus(restoreFocus);}}
 void KeyPressed(object s,KeyEventArgs e){keyboard=true;if(vm.DialogOpen){if(e.Key==Key.Enter){vm.ConfirmCommand.Execute(null);e.Handled=true;}else if(e.Key==Key.Escape){vm.CancelCommand.Execute(null);e.Handled=true;}else if(e.Key==Key.Tab && !DialogHost.IsKeyboardFocusWithin){CancelButton.Focus();e.Handled=true;}return;}if(Keyboard.Modifiers==ModifierKeys.Control && e.Key>=Key.D1&&e.Key<=Key.D6){vm.Navigate(ShellViewModel.PageNames[(int)e.Key-(int)Key.D1]);e.Handled=true;}if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.F){vm.FocusHistorySearch();e.Handled=true;}}
 void ResizeSidebar(){bool compact=ActualWidth<1000;SidebarColumn.Width=new(compact?64:240);SidebarBody.Margin=new Thickness(compact?6:16,20,compact?6:16,20);SidebarFooter.Visibility=compact?Visibility.Collapsed:Visibility.Visible;CompactStateDot.Visibility=compact?Visibility.Visible:Visibility.Collapsed;Wordmark.Visibility=compact?Visibility.Collapsed:Visibility.Visible;foreach(var text in Descendants<TextBlock>(this).Where(t=>Equals(t.Tag,"NavLabel")))text.Visibility=compact?Visibility.Collapsed:Visibility.Visible;}
 internal static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T t)yield return t;foreach(var d in Descendants<T>(child))yield return d;}}
 IntPtr ChromeHook(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled)
 {
  if(message==0x24){var info=System.Runtime.InteropServices.Marshal.PtrToStructure<MinMaxInfo>(l);var screen=System.Windows.Forms.Screen.FromHandle(hwnd);var work=screen.WorkingArea;var bounds=screen.Bounds;info.MaxPosition=new NativePoint(work.Left-bounds.Left,work.Top-bounds.Top);info.MaxSize=new NativePoint(work.Width,work.Height);var dpi=VisualTreeHelper.GetDpi(this);info.MinTrackSize=new NativePoint((int)(900*dpi.DpiScaleX),(int)(600*dpi.DpiScaleY));System.Runtime.InteropServices.Marshal.StructureToPtr(info,l,false);handled=true;}return IntPtr.Zero;
 }
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct NativePoint {public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct MinMaxInfo {public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
 void RestorePlacement(){if(persistence is null)return;try{var saved=persistence.Load("window.json",new WindowPlacement());var p=PlacementRecovery.Recover(saved,new[]{SystemParameters.WorkArea});Width=p.Width;Height=p.Height;Left=p.Left!.Value;Top=p.Top!.Value;if(p.Maximized)WindowState=WindowState.Maximized;}catch(Exception ex){WindowStartupLocation=WindowStartupLocation.CenterScreen;vm.Notify("Window placement reset: "+ex.Message);}} void OnClosing(object? s,CancelEventArgs e){if(persistence is not null){var r=WindowState==WindowState.Normal?new Rect(Left,Top,Width,Height):RestoreBounds;try{persistence.Save("window.json",new WindowPlacement(r.Width,r.Height,r.Left,r.Top,WindowState==WindowState.Maximized));persistence.Save("settings.json",vm.App.Settings with {LastPage=vm.Page});}catch(Exception ex){vm.Notify(ex.Message);}if(!exiting&&vm.App.Settings.CloseToTray){e.Cancel=true;Hide();}}}
 public void NavigateTo(string page)=>vm.Navigate(page);
 public void CloseForExit(){exiting=true;Close();}
 void Minimize(object s,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
 void Maximize(object s,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
 void CloseWindow(object s,RoutedEventArgs e)=>Close();
 public void SetPreviewContent(object? content)=>PageHost.Content=content;
}
