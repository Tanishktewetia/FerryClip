using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.ViewModels;

namespace ClipSync.Windows.MainUI.Pages.Dashboard;

/// <summary>Dashboard projection; state changes and mutations stay behind the AppState facade.</summary>
public sealed class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel shell;
    private bool disposed;

    public DashboardViewModel(ShellViewModel shell)
    {
        this.shell = shell;
        Status = new StatusCardViewModel(shell.App);
        Pairing = new PairingViewModel(shell.App);
        ViewHistoryCommand = new RelayCommand(() => shell.Navigate("History"));
        OpenDevicesCommand = new RelayCommand(() => shell.Navigate("Devices"));
        TurnOnHistoryCommand = new RelayCommand(() => { try { shell.App.UpdateSettings(shell.App.Settings with { History = shell.App.Settings.Policy with { Save = true } }); } catch (Exception ex) { shell.App.Report(ex); } });
        OpenSettingsCommand = new RelayCommand(() => shell.Navigate("Settings"));
        shell.App.PropertyChanged += Changed;
        Refresh();
    }

    public StatusCardViewModel Status { get; }
    public PairingViewModel Pairing { get; }
    public IReadOnlyList<DashboardDeviceViewModel> Devices { get; private set; } = [];
    public IReadOnlyList<DashboardClipViewModel> Clips { get; private set; } = [];
    public bool NoDevices => Devices.Count == 0;
    public bool IsPaused => shell.App.IsPaused;
    public double HistoryOpacity => IsPaused ? .48 : 1;
    public bool HasClips => shell.App.Settings.Policy.Save && Clips.Count > 0 && shell.App.HistoryError is null;
    public bool ShowHistoryMessage => !HasClips;
    public string HistoryMessage => shell.App.HistoryError is not null
        ? "Clipboard history could not be read"
        : !shell.App.Settings.Policy.Save ? "Clipboard history is off" : "No recent clipboard items";
    public string HistoryHint => shell.App.HistoryError
        ?? (!shell.App.Settings.Policy.Save ? "Enable Save history in Settings to keep recent clips." : "Copied and received text will appear here.");
    public bool HistoryOff => !shell.App.Settings.Policy.Save;
    public ICommand ViewHistoryCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenDevicesCommand { get; }
    public ICommand TurnOnHistoryCommand { get; }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Devices" or "IsPaused" or "History" or "Settings" or "HistoryError")
            Refresh();

        Raise(nameof(IsPaused));
        Raise(nameof(HistoryOpacity));
        Raise(nameof(HasClips));
        Raise(nameof(ShowHistoryMessage));
        Raise(nameof(HistoryMessage));
        Raise(nameof(HistoryHint));
        Raise(nameof(HistoryOff));
    }

    private void Refresh()
    {
        foreach (var clip in Clips)
            clip.Dispose();

        Devices = shell.App.Devices.Select(device => new DashboardDeviceViewModel(device, shell)).ToArray();
        // The dashboard's recent list is chronological, independent of History's pinned grouping.
        Clips = shell.App.History
            .OrderByDescending(clip => clip.At)
            .Take(3)
            .Select(clip => new DashboardClipViewModel(clip, shell))
            .ToArray();

        Raise(nameof(Devices));
        Raise(nameof(Clips));
        Raise(nameof(NoDevices));
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        shell.App.PropertyChanged -= Changed;
        foreach (var clip in Clips)
            clip.Dispose();
        Clips = [];
        Status.Dispose();
        Pairing.Dispose();
    }
}

public sealed class DashboardDeviceViewModel : ObservableObject
{
    private readonly DeviceSnapshot device;
    private readonly ShellViewModel shell;

    public DashboardDeviceViewModel(DeviceSnapshot device, ShellViewModel shell)
    {
        this.device = device;
        this.shell = shell;
    }

    public string Name => device.Name;
    public string Status => !device.Connected
        ? "Not connected"
        : !device.SyncEnabled ? "Sync paused"
        : shell.App.IsPaused ? "Paused"
        : "Connected";
    public string StatusResource => !device.Connected
        ? "Danger"
        : !device.SyncEnabled || shell.App.IsPaused ? "Paused"
        : "Connected";
    public string LastSeen => TimeFormatter.Format(device.Last ?? device.Since);
    public string FullTime => TimeFormatter.Full(device.Last ?? device.Since);
    public string SyncAutomationName => $"Enable or pause sync for {Name}";
    public ICommand OpenCommand => new RelayCommand(() => shell.Navigate("Devices"));
    public System.Windows.Media.Brush StatusBrush => !device.Connected ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,138,138)) : !device.SyncEnabled || shell.App.IsPaused ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245,183,74)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(61,220,151));

    public bool SyncEnabled
    {
        get => device.SyncEnabled;
        set
        {
            if (value == device.SyncEnabled)
                return;

            try
            {
                shell.App.SetDeviceSync(device.Id, value);
                shell.App.Refresh();
            }
            catch (Exception ex)
            {
                shell.App.Report(ex);
                Raise();
            }
        }
    }
}

public sealed class DashboardClipViewModel : ObservableObject, IDisposable
{
    private static readonly string Mask = new('•', 12);
    private readonly ClipSync.Windows.Clipboard.ClipboardHistory.Entry clip;
    private readonly DispatcherTimer revealTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool revealed;
    private bool disposed;
    private bool copied;
    private readonly DispatcherTimer copiedTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private readonly ShellViewModel shell;

    public DashboardClipViewModel(ClipSync.Windows.Clipboard.ClipboardHistory.Entry clip, ShellViewModel shell)
    {
        this.clip = clip;
        this.shell = shell;
        revealTimer.Tick += Remask;
        copiedTimer.Tick += CopiedTimerTick;
        RevealCommand = new RelayCommand(ToggleReveal);
        CopyCommand = new RelayCommand(() =>
        {
            try
            {
                shell.App.CopyClip(clip.Id);
                copied = true; copiedTimer.Stop(); copiedTimer.Start(); Raise(nameof(IsCopied)); Raise(nameof(CopyToolTip));
                shell.Notify("Copied");
            }
            catch (Exception ex)
            {
                shell.App.Report(ex);
            }
        });
    }

    public string Preview => clip.Sensitive && !revealed
        ? Mask
        : clip.Text.Replace('\r', ' ').Replace('\n', ' ');
    public string Direction => clip.Direction == "Received" ? "From phone" : "From PC";
    public string Time => TimeFormatter.Format(clip.At);
    public string FullTime => TimeFormatter.Full(clip.At);
    public bool Sensitive => clip.Sensitive;
    public string RevealLabel => revealed ? "Hide sensitive clip" : "Reveal sensitive clip";
    public ICommand RevealCommand { get; }
    public bool IsCopied => copied;
    public string CopyToolTip => copied ? "Copied" : "Copy clip";
    public ICommand CopyCommand { get; }

    private void CopiedTimerTick(object? sender, EventArgs e) { copiedTimer.Stop(); copied = false; Raise(nameof(IsCopied)); Raise(nameof(CopyToolTip)); }

    private void ToggleReveal()
    {
        if (disposed || !clip.Sensitive)
            return;

        revealed = !revealed;
        if (revealed)
            revealTimer.Start();
        else
            revealTimer.Stop();

        Raise(nameof(Preview));
        Raise(nameof(RevealLabel));
    }

    private void Remask(object? sender, EventArgs e)
    {
        revealTimer.Stop();
        if (!revealed)
            return;

        revealed = false;
        Raise(nameof(Preview));
        Raise(nameof(RevealLabel));
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        revealTimer.Stop();
        copiedTimer.Stop(); copiedTimer.Tick -= CopiedTimerTick;
        revealTimer.Tick -= Remask;
        revealed = false;
    }
}
