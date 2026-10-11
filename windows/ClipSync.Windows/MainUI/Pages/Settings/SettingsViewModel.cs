using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.Services;
using ClipSync.Windows.MainUI.ViewModels;
using ClipSync.Windows.Transport;

namespace ClipSync.Windows.MainUI.Pages.Settings;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    readonly ShellViewModel shell;
    readonly AppState app;
    bool disposed;
    bool firewallHelpExpanded;

    public SettingsViewModel(ShellViewModel shell)
    {
        this.shell = shell;
        app = shell.App;
        app.PropertyChanged += AppChanged;
        ToggleFirewallHelpCommand = new RelayCommand(() => FirewallHelpExpanded = !FirewallHelpExpanded);
        ClearHistoryCommand = new AsyncCommand(ClearHistoryAsync, app.Report);
        OpenTroubleshootingCommand = new RelayCommand(() => OpenUrl("https://ferryclip.app/docs.html#troubleshooting"));
        OpenLatestReleaseCommand = new RelayCommand(() => OpenUrl("https://github.com/Tanishktewetia/FerryClip/releases/latest"));
    }

    public IReadOnlyList<int> HistoryLimits { get; } = [50, 100, 500];
    public IReadOnlyList<string> RetentionOptions { get; } = ["Never", "After 24 hours", "After 7 days"];

    public bool StartWithWindows
    {
        get => app.StartupEnabled;
        set
        {
            if (value == app.StartupEnabled) return;
            try { app.SetStartup(value); }
            catch (Exception ex) { app.Report(ex); }
            Raise();
        }
    }

    public bool CloseToTray
    {
        get => app.Settings.CloseToTray;
        set => Update(app.Settings with { CloseToTray = value });
    }

    public bool AutoCopyReceived{get=>app.Settings.AutoCopyReceived;set=>Update(app.Settings with{AutoCopyReceived=value});}
    public bool PauseAll
    {
        get => app.IsPaused;
        set
        {
            if (value == app.IsPaused) return;
            try { app.SetGlobalPaused(value); }
            catch (Exception ex) { app.Report(ex); }
            Raise();
        }
    }

    public bool SaveHistory
    {
        get => app.Settings.Policy.Save;
        set => UpdatePolicy(app.Settings.Policy with { Save = value });
    }

    public int HistoryLimit
    {
        get => app.Settings.Policy.Limit;
        set => UpdatePolicy(app.Settings.Policy with { Limit = value });
    }

    public string Retention
    {
        get => app.Settings.Policy.RetentionHours switch { 0 => "Never", 168 => "After 7 days", _ => "After 24 hours" };
        set => UpdatePolicy(app.Settings.Policy with { RetentionHours = value switch { "Never" => 0, "After 7 days" => 168, _ => 24 } });
    }

    public bool SkipSensitive
    {
        get => app.Settings.Policy.SkipSensitive;
        set => UpdatePolicy(app.Settings.Policy with { SkipSensitive = value });
    }

    public string Port => SyncServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Version => app.Version;

    public bool FirewallHelpExpanded
    {
        get => firewallHelpExpanded;
        set { if (Set(ref firewallHelpExpanded, value)) Raise(nameof(FirewallHelpLabel)); }
    }

    public string FirewallHelpLabel => FirewallHelpExpanded ? "Hide firewall help" : "Show firewall help";

    public ICommand ToggleFirewallHelpCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand OpenTroubleshootingCommand { get; }
    public ICommand OpenLatestReleaseCommand { get; }

    async Task ClearHistoryAsync()
    {
        if (!await shell.ConfirmAsync("Clear clipboard history?", "Saved clipboard text will be removed. Pinned items are kept.")) return;
        await app.ClearHistory();
        shell.Notify("Clipboard history cleared. Pinned items were kept.");
    }

    void UpdatePolicy(HistoryPolicy policy) => Update(app.Settings with { History = policy });

    void Update(UserSettings settings)
    {
        if (settings == app.Settings) return;
        try { app.UpdateSettings(settings); }
        catch (Exception ex) { app.Report(ex); }
    }

    void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { app.Report(ex); }
    }

    void AppChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.Settings) or nameof(AppState.StartupEnabled) or nameof(AppState.IsPaused)) RaiseAll();
    }

    void RaiseAll()
    {
        Raise(nameof(AutoCopyReceived));Raise(nameof(StartWithWindows)); Raise(nameof(CloseToTray)); Raise(nameof(PauseAll));
        Raise(nameof(SaveHistory)); Raise(nameof(HistoryLimit)); Raise(nameof(Retention)); Raise(nameof(SkipSensitive));
        Raise(nameof(Version));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        app.PropertyChanged -= AppChanged;
    }
}
