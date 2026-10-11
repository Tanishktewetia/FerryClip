using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Services;
namespace ClipSync.Windows.MainUI.ViewModels;
/// <summary>Dashboard status action: Resume owns primary when globally paused; Pause is always secondary.</summary>
public sealed class StatusCardViewModel : ObservableObject, IDisposable
{
 readonly AppState app;
 public StatusCardViewModel(AppState app){this.app=app;ActionCommand=new RelayCommand(()=>{try{app.SetGlobalPaused(!app.IsPaused);}catch(Exception ex){app.Report(ex);}});app.PropertyChanged+=Changed;}
 public string Title=>app.State switch{Models.ConnectionState.NoDevices=>"No devices",Models.ConnectionState.Unreachable=>"Not connected",_=>app.State.ToString()};
 public string Description {get{var n=app.Devices.Count(d=>d.Connected);return $"{n} {(n==1?"device":"devices")} on your local network";}}
 public System.Windows.Media.Brush Color=>new SolidColorBrush(app.IsPaused?System.Windows.Media.Color.FromRgb(234,187,98):app.State==Models.ConnectionState.Connected?System.Windows.Media.Color.FromRgb(83,202,155):System.Windows.Media.Color.FromRgb(171,181,195));
 public System.Windows.Media.Brush Tint {get{var c=((SolidColorBrush)Color).Color;return new SolidColorBrush(System.Windows.Media.Color.FromArgb(20,c.R,c.G,c.B));}}
 public bool HasAction=>app.IsPaused||app.Devices.Any(d=>d.Connected);
 public bool IsPrimary=>app.IsPaused;
 public string ActionLabel=>app.IsPaused?"Resume sync":"Pause sync";
 public ICommand ActionCommand{get;}
 void Changed(object? sender,PropertyChangedEventArgs e){foreach(var p in new[]{nameof(Title),nameof(Description),nameof(Color),nameof(Tint),nameof(HasAction),nameof(IsPrimary),nameof(ActionLabel)})Raise(p);}
 public void Dispose()=>app.PropertyChanged-=Changed;
}
