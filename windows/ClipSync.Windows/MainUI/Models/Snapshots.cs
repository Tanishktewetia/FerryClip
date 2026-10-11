namespace ClipSync.Windows.MainUI.Models;
public enum ConnectionState { Connected, Paused, Unreachable, NoDevices }
public sealed record DeviceSnapshot(string Id,string Name,bool Connected,bool SyncEnabled,string? Address,DateTimeOffset? Since,DateTimeOffset? Last);
public sealed record PairingSnapshot(string? Code,DateTimeOffset? Deadline)
{
 public bool Active=>Code is not null && Deadline>DateTimeOffset.UtcNow;
 public string DisplayCode=>Active && Code!.Length==6?Code[..3]+" "+Code[3..]:"";
 public string Countdown=>Active? $"Expires in {(int)(Deadline!.Value-DateTimeOffset.UtcNow).TotalMinutes:00}:{(Deadline.Value-DateTimeOffset.UtcNow).Seconds:00}" : "";
}
public sealed record HistoryPolicy(bool Save=true,int Limit=100,int RetentionHours=24,bool SkipSensitive=true);
public sealed record UserSettings(bool CloseToTray=true,string Theme="Dark",HistoryPolicy? History=null,bool AutoCopyReceived=true,string LastPage="Dashboard")
{
 public HistoryPolicy Policy=>History??new();
}
public sealed record WindowPlacement(double Width=1100,double Height=720,double? Left=null,double? Top=null,bool Maximized=false);
