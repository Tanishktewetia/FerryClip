using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClipSync.Windows.MainUI.Components;
public static class PrimaryActionPolicy
{
 public static string Choose(Models.ConnectionState state,bool pairingActive)=>state==Models.ConnectionState.Paused?"Resume sync":!pairingActive?"Generate code":"Open FerryClip";
}
public abstract class TemplatedComponent : ContentControl
{
 protected TemplatedComponent(string key){var resources=new ResourceDictionary{Source=new Uri("/FerryClip;component/MainUI/Components/Templates.xaml",UriKind.Relative)};Template=(ControlTemplate)resources[key];}
}
public sealed class EmptyState : TemplatedComponent {public EmptyState(string title,string description):base("EmptyTemplate"){DataContext=new{Title=title,Description=description};}}
public sealed class StatusCard : TemplatedComponent
{
 public StatusCard(ViewModels.StatusCardViewModel viewModel):base("StatusTemplate"){DataContext=viewModel;}
 public StatusCard(string title,string description,System.Windows.Media.Brush color):base("StatusTemplate"){var c=(color as SolidColorBrush)?.Color??Colors.Gray;DataContext=new{Title=title,Description=description,Color=color,HasAction=false,IsPrimary=false,ActionLabel="",ActionCommand=(System.Windows.Input.ICommand?)null,Tint=new SolidColorBrush(System.Windows.Media.Color.FromArgb(20,c.R,c.G,c.B))};}
}
public sealed class DirectionBadge : TemplatedComponent {public DirectionBadge(string direction):base("DirectionTemplate"){Content=direction=="Received"?"From phone":"From PC";FontSize=12;}}
public sealed class DeviceRow : TemplatedComponent {public DeviceRow(Models.DeviceSnapshot device):base("DeviceTemplate"){DataContext=new{Name=device.Name,Status=device.Connected?"Connected":"Not connected"};}}

