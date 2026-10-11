using System.Windows;
using ClipSync.Windows.MainUI.Models;
namespace ClipSync.Windows.MainUI.Services;
/// <summary>All inputs are WPF desktop DIPs, never mixed with physical pixels.</summary>
public static class PlacementRecovery
{
 public static WindowPlacement Recover(WindowPlacement saved,IReadOnlyList<Rect> workAreas)
 {
  if(workAreas.Count==0)throw new ArgumentException("No work areas");
  double width=double.IsFinite(saved.Width)?Math.Max(900,saved.Width):1100,height=double.IsFinite(saved.Height)?Math.Max(600,saved.Height):720;
  var target=workAreas[0];bool found=false;
  if(saved.Left is {} x&&saved.Top is {} y&&double.IsFinite(x)&&double.IsFinite(y))foreach(var area in workAreas){if(area.IntersectsWith(new Rect(x,y,Math.Min(width,160),40))){target=area;found=true;break;}}
  width=Math.Min(width,Math.Max(900,target.Width));height=Math.Min(height,Math.Max(600,target.Height));
  var left=found?saved.Left!.Value:target.Left+(target.Width-width)/2;var top=found?saved.Top!.Value:target.Top+(target.Height-height)/2;
  left=Math.Clamp(left,target.Left,Math.Max(target.Left,target.Right-width));top=Math.Clamp(top,target.Top,Math.Max(target.Top,target.Bottom-height));
  return new(width,height,left,top,saved.Maximized);
 }
}
