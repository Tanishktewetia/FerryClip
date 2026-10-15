using System.Threading;
using System.Windows;
using ClipSync.Windows.UI;
using ClipSync.Windows.Logging;
using ClipSync.Windows.Theme;
using ClipSync.Windows.Transport;
using ClipSync.Windows.Security;
using ClipSync.Windows.Clipboard;
using ClipSync.Core;
namespace ClipSync.Windows;
public partial class App : System.Windows.Application
{
 Mutex? mutex;TrayIconManager? tray;CertificateStore? store;SyncServer? server;ClipboardWatcher? clipboard;bool ownsMutex;MainUI.Services.AppState? state;MainUI.Services.MainWindowService? windows;
 protected override void OnStartup(StartupEventArgs e){base.OnStartup(e);
#if DEBUG
 if(MainUI.Debug.PreviewRunner.TryStart(this,e.Args))return;
#endif
 MainUI.Services.ExceptionBoundary.Install(this);mutex=new Mutex(true,"ClipSync-SingleInstance-48653",out var created);ownsMutex=created;if(!created){Shutdown();return;}FileLogger.Instance.Info($"FerryClip v{typeof(App).Assembly.GetName().Version?.ToString(3)} starting");ThemeManager.Initialize();store=new CertificateStore();server=new SyncServer(store);clipboard=new ClipboardWatcher();var settings=new MainUI.Services.SettingsStore();state=new MainUI.Services.AppState(new MainUI.Services.LiveBackend(server,clipboard),ClipboardHistory.Shared,Dispatcher,settings);windows=new MainUI.Services.MainWindowService(state,settings);DesktopActions.OpenRequested=page=>windows.Open(page);state.WelcomeGuideRequested=()=>windows.Open(onboarding:true);server.ReplayOnConnect=true;tray=new TrayIconManager(state,()=>windows.Open(),()=>{windows.Open("Dashboard");state.BeginPairing();},()=>state.SetGlobalPaused(!state.IsPaused));windows.Notice=tray.ShowNotice;
 server.Notice+=message=>Dispatcher.BeginInvoke(new Action(()=>tray.ShowNotice(message)));
 server.ConnectionChanged+=_=>Dispatcher.BeginInvoke(new Action(()=>state.Refresh()));
 server.ClipboardSink=text=>Dispatcher.Invoke(()=>{var applied=!state.Settings.AutoCopyReceived||clipboard.WriteText(text);if(applied){_ = ClipboardHistory.Shared.RecordAsync(text,"Received",false);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));FileLogger.Instance.Info($"Phone clipboard applied: text {text.Length} chars hash {hash[..6]}");}return applied;});
 clipboard.TextChanged+=text=>{_ = ClipboardHistory.Shared.RecordAsync(text,"Copied",SensitiveClipboardFormats.IsSensitive());var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));server.Broadcast(text);FileLogger.Instance.Info($"Clipboard sent: text {text.Length} chars hash {hash[..6]}");};
 server.Start();_ = state.ReloadHistoryAsync();var startup=e.Args.Any(a=>string.Equals(a,"--startup",StringComparison.OrdinalIgnoreCase));var show=e.Args.Any(a=>string.Equals(a,"--show",StringComparison.OrdinalIgnoreCase));var paired=store.Peers.Count>0;if(paired&&!state.Settings.OnboardingCompleted)state.CompleteOnboarding();if(!startup&&!paired&&!state.Settings.OnboardingCompleted)windows.Open(onboarding:true);else if(!startup&&show)windows.Open("Dashboard");
 }
 protected override void OnExit(ExitEventArgs e){if(!ownsMutex){mutex?.Dispose();base.OnExit(e);return;}FileLogger.Instance.Info("FerryClip shutting down");DesktopActions.OpenRequested=null;tray?.Dispose();windows?.Dispose();state?.Dispose();clipboard?.Dispose();server?.Dispose();store?.Dispose();ThemeManager.Shutdown();mutex?.ReleaseMutex();mutex?.Dispose();FileLogger.Instance.Dispose();base.OnExit(e);}
}
