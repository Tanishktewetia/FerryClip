using System.Windows;
using System.Windows.Threading;
using ClipSync.Windows.Clipboard;
using ClipSync.Windows.Transport;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.UI;
namespace ClipSync.Windows.MainUI.Services;
public interface IAppBackend : IDisposable
{
 event Action? Changed;
 IReadOnlyList<DeviceSnapshot> Devices {get;}
 bool Paused {get;set;}
 PairingSnapshot Pairing {get;}
 bool StartupEnabled {get;}
 void SetStartup(bool enabled);
 void BeginPairing(); void SetDeviceSync(string id,bool enabled); void Rename(string id,string name); void Forget(string id); bool Copy(string text);
}
public sealed class LiveBackend : IAppBackend
{
 readonly SyncServer server; readonly ClipboardWatcher clipboard; string? code;
 public LiveBackend(SyncServer server,ClipboardWatcher clipboard){this.server=server;this.clipboard=clipboard;server.ConnectionChanged+=ConnectionChanged;server.PairingCodeAvailable+=CodeAvailable;server.Notice+=Notice;}
 public event Action? Changed;
 void ConnectionChanged(bool _)=>Changed?.Invoke(); void CodeAvailable(string c){code=c;Changed?.Invoke();} void Notice(string _)=>Changed?.Invoke();
 public IReadOnlyList<DeviceSnapshot> Devices=>server.Phones.Select(p=>new DeviceSnapshot(p.Id,p.Name,p.Connected,p.Enabled&&!p.Paused,p.Address,p.Since,p.Last)).ToArray();
 public bool Paused{get=>server.Paused;set{server.Paused=value;Changed?.Invoke();}}
 public PairingSnapshot Pairing=>new(code,server.PairingExpiresAt);
 public bool StartupEnabled=>StartupManager.IsEnabled;
 public void SetStartup(bool value){if(!StartupManager.SetEnabled(value))throw new InvalidOperationException("Could not update Windows startup registration.");}
 public void BeginPairing()=>server.BeginPairing(); public void SetDeviceSync(string id,bool enabled)=>server.PausePhone(id,!enabled);
 public void Rename(string id,string name)=>server.RenamePhone(id,name); public void Forget(string id)=>server.ForgetPhone(id);
 public bool Copy(string text)=>clipboard.WriteText(text);
 public void Dispose(){server.ConnectionChanged-=ConnectionChanged;server.PairingCodeAvailable-=CodeAvailable;server.Notice-=Notice;}
}
/// <summary>UI-thread-owned facade. Pages never reference SyncServer or the shared history singleton.</summary>
public sealed class AppState : ObservableObject, IDisposable
{
 readonly IAppBackend backend;readonly ClipboardHistory history;readonly Dispatcher dispatcher;readonly SettingsStore? store;long historyRevision;bool disposed;
 public AppState(IAppBackend backend,ClipboardHistory history,Dispatcher dispatcher,SettingsStore? store=null){this.backend=backend;this.history=history;this.dispatcher=dispatcher;this.store=store;Settings=store?.LoadSettings()??new();history.Configure(Settings.Policy);backend.Changed+=OnChanged;history.Changed+=HistoryChanged;Refresh();}
 public UserSettings Settings{get;private set;}
 public IReadOnlyList<DeviceSnapshot> Devices{get;private set;}=Array.Empty<DeviceSnapshot>();
 public IReadOnlyList<ClipboardHistory.Entry> History{get;private set;}=Array.Empty<ClipboardHistory.Entry>();
 public string? HistoryError=>history.Error;
 public PairingSnapshot Pairing{get;private set;}=new(null,null);
 public bool IsPaused{get;private set;}
 public bool StartupEnabled=>backend.StartupEnabled;
 public ConnectionState State=>IsPaused?ConnectionState.Paused:Devices.Any(d=>d.Connected)?ConnectionState.Connected:Devices.Count==0?ConnectionState.NoDevices:ConnectionState.Unreachable;
 public string Version=>typeof(AppState).Assembly.GetName().Version?.ToString(3)??"";
 public event Action<Exception>? ActionFailed;
 void OnChanged(){if(!disposed&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(new Action(Refresh));}
 void HistoryChanged(){if(!disposed&&!dispatcher.HasShutdownStarted)dispatcher.BeginInvoke(new Action(async()=>await ReloadHistoryAsync()));}
 public void Refresh(){dispatcher.VerifyAccess();if(disposed)return;Devices=backend.Devices;IsPaused=backend.Paused;Pairing=backend.Pairing;Raise(nameof(Devices));Raise(nameof(IsPaused));Raise(nameof(Pairing));Raise(nameof(State));}
 public async Task ReloadHistoryAsync(){var revision=Interlocked.Increment(ref historyRevision);var entries=await history.ListAsync();await dispatcher.InvokeAsync(()=>{if(disposed||revision!=historyRevision)return;History=entries;Raise(nameof(History));Raise(nameof(HistoryError));});}
 public void SetGlobalPaused(bool value){dispatcher.VerifyAccess();backend.Paused=value;Refresh();}
 public void BeginPairing()=>backend.BeginPairing();
 public void SetDeviceSync(string id,bool value)=>backend.SetDeviceSync(id,value);
 public void RenameDevice(string id,string name){if(string.IsNullOrWhiteSpace(name)||name.Length>80)throw new ArgumentException("Use a name between 1 and 80 characters.");backend.Rename(id,name.Trim());}
 public void ForgetDevice(string id)=>backend.Forget(id);
 public void CopyPairingCode(){dispatcher.VerifyAccess();if(!Pairing.Active)throw new InvalidOperationException("Pairing code expired.");if(!backend.Copy(Pairing.Code!))throw new InvalidOperationException("Clipboard is busy; try again.");}
 public void CopyClip(Guid id){var clip=History.Single(e=>e.Id==id);if(!backend.Copy(clip.Text))throw new InvalidOperationException("Clipboard is busy; try again.");}
 public async Task PinClip(Guid id,bool pinned){await history.PinAsync(id,pinned);CheckHistoryError();await ReloadHistoryAsync();}
 public async Task DeleteClip(Guid id){await history.DeleteAsync(id);CheckHistoryError();await ReloadHistoryAsync();}
 public async Task ClearHistory(bool includePinned=false){await history.ClearAsync(includePinned);CheckHistoryError();await ReloadHistoryAsync();}
 void CheckHistoryError(){if(history.Error is {} error)throw new InvalidOperationException(error);}
 public void UpdateSettings(UserSettings settings){SettingsStore.Validate(settings);store?.Save("settings.json",settings);Settings=settings;history.Configure(settings.Policy);Raise(nameof(Settings));}
 public void SetStartup(bool enabled){backend.SetStartup(enabled);Raise(nameof(StartupEnabled));}
 public void Report(Exception ex)=>ActionFailed?.Invoke(ex);
 public void Dispose(){disposed=true;Interlocked.Increment(ref historyRevision);backend.Changed-=OnChanged;history.Changed-=HistoryChanged;backend.Dispose();}
}
