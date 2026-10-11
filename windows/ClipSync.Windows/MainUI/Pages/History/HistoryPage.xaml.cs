using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using System.Windows.Media;
using System.Windows.Threading;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.History;
public sealed class ExpandedChevronTransformConverter:IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is true?180d:0d;
 public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
public partial class HistoryPage:System.Windows.Controls.UserControl,IDisposable
{
 public HistoryViewModel Model{get;} readonly ShellViewModel shell;bool disposed;
 public HistoryPage(ShellViewModel shell){this.shell=shell;InitializeComponent();shell.HistorySearchRequested+=Requested;Model=new(shell);DataContext=Model;Unloaded+=Leaving;HistoryList.Loaded+=HistoryListLoaded;AddHandler(Keyboard.PreviewKeyDownEvent,new System.Windows.Input.KeyEventHandler(SearchShortcut),true);}
 void Requested()=>Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>{if(!disposed)FocusSearch();}));
 void Leaving(object sender,RoutedEventArgs e)=>Model.Remask();
 void SearchShortcut(object sender,KeyEventArgs e)
 {
  if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.F){FocusSearch();e.Handled=true;return;}
  if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Z&&Model.UndoVisible){Model.UndoCommand.Execute(null);e.Handled=true;return;}
  if(e.OriginalSource is System.Windows.Controls.TextBox)return;
  if(e.Key is Key.Up or Key.Down){MoveSelection(e.Key==Key.Down?1:-1);e.Handled=true;return;}
  if(e.Key==Key.Enter){_ = Model.CopyAsync(HistoryList.SelectedItem as HistoryClipViewModel);e.Handled=true;return;}
  if(e.Key==Key.Delete){_ = Model.DeleteWithUndoAsync(HistoryList.SelectedItem as HistoryClipViewModel);e.Handled=true;}
 }
 void MoveSelection(int direction)
 {
  var index=HistoryList.SelectedIndex;var items=Model.Items;
  for(var next=index+direction;next>=0&&next<items.Count;next+=direction)if(items[next] is HistoryClipViewModel){HistoryList.SelectedIndex=next;HistoryList.ScrollIntoView(items[next]);if(HistoryList.ItemContainerGenerator.ContainerFromIndex(next) is ListBoxItem item)item.Focus();return;}
  if(index<0&&direction>0){for(var next=0;next<items.Count;next++)if(items[next] is HistoryClipViewModel){HistoryList.SelectedIndex=next;HistoryList.ScrollIntoView(items[next]);return;}}
 }
 void PreviewMeasure(object sender,RoutedEventArgs e)
 {
  if(sender is not TextBlock text)return;
  text.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(text.DataContext is HistoryClipViewModel clip)clip.SetCanExpand(IsActuallyTrimmed(text));}));
 }
 static bool IsActuallyTrimmed(TextBlock text){if(text.ActualWidth<=0)return false;var formatted=new FormattedText(text.Text??"",CultureInfo.CurrentUICulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,System.Windows.Media.Brushes.Black,VisualTreeHelper.GetDpi(text).PixelsPerDip){MaxTextWidth=text.ActualWidth};return formatted.Height>text.ActualHeight+0.5;}
 void HistoryListLoaded(object sender,RoutedEventArgs e){var scroll=FindScrollViewer(HistoryList);if(scroll is not null)scroll.ScrollChanged+=HistoryScrollChanged;}
 void HistoryScrollChanged(object sender,ScrollChangedEventArgs e)
 {
  var scroll=FindScrollViewer(HistoryList);if(scroll is null||e.VerticalOffset<=0){StickyHeader.Visibility=Visibility.Collapsed;return;}
  var first=-1;double minTop=double.PositiveInfinity;
  for(var i=0;i<HistoryList.Items.Count;i++)if(HistoryList.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
  {try{var rect=item.TransformToAncestor(scroll).TransformBounds(new Rect(new System.Windows.Point(0,0),item.RenderSize));if(rect.Bottom>0&&rect.Top<minTop){first=i;minTop=rect.Top;}}catch(InvalidOperationException){}}
  if(first<0){StickyHeader.Visibility=Visibility.Collapsed;return;}
  if(HistoryList.Items[first] is not HistoryHeading)while(first>=0&&HistoryList.Items[first] is not HistoryHeading)first--;
  if(first>=0){Model.SetStickyGroup(((HistoryHeading)HistoryList.Items[first]).Title);StickyHeader.Visibility=Visibility.Visible;}else StickyHeader.Visibility=Visibility.Collapsed;
 }
 static ScrollViewer? FindScrollViewer(DependencyObject parent){for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is ScrollViewer viewer)return viewer;var found=FindScrollViewer(child);if(found is not null)return found;}return null;}
 public void FocusSearch(){SearchBox.Focus();SearchBox.SelectAll();}
 public void Dispose(){if(disposed)return;disposed=true;shell.HistorySearchRequested-=Requested;Unloaded-=Leaving;HistoryList.Loaded-=HistoryListLoaded;var scroll=FindScrollViewer(HistoryList);if(scroll is not null)scroll.ScrollChanged-=HistoryScrollChanged;Model.Dispose();DataContext=null;}
}
