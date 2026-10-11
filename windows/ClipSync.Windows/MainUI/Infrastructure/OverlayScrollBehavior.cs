using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace ClipSync.Windows.MainUI.Infrastructure;
public static class OverlayScrollBehavior
{
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(OverlayScrollBehavior),new PropertyMetadata(false,Changed));
 public static void SetEnabled(DependencyObject target,bool value)=>target.SetValue(EnabledProperty,value);
 public static bool GetEnabled(DependencyObject target)=>(bool)target.GetValue(EnabledProperty);
 static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs e)
 {
  if(target is not ScrollViewer scroll || e.NewValue is not true)return;
  var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1200)};
  ScrollBarOpacity(0);
  void ScrollBarOpacity(double opacity){if(scroll.Template?.FindName("PART_VerticalScrollBar",scroll) is FrameworkElement bar)bar.Opacity=opacity;}
  void Show(){ScrollBarOpacity(1);timer.Stop();timer.Start();}
  timer.Tick+=(_,_)=>{if(scroll.Template?.FindName("PART_VerticalScrollBar",scroll) is FrameworkElement bar && bar.IsMouseOver)return;ScrollBarOpacity(0);timer.Stop();};
  scroll.Loaded+=(_,_)=>ScrollBarOpacity(0);
  scroll.ScrollChanged+=(_,_)=>Show();scroll.MouseMove+=(_,_)=>Show();scroll.PreviewKeyDown+=(_,_)=>Show();scroll.Unloaded+=(_,_)=>timer.Stop();
 }
}
