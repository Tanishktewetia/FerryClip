using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClipSync.Windows.MainUI.Infrastructure;
using ClipSync.Windows.MainUI.Models;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.Devices;

public sealed class DevicesViewModel : ObservableObject, IDisposable
{
    readonly ShellViewModel shell;
    readonly DispatcherTimer timer;
    bool pairingOpen, disposed;
    public DevicesViewModel(ShellViewModel shell)
    {
        this.shell = shell;
        Pairing = new(shell.App);
        AddCommand = new RelayCommand(() => PairingOpen = true);
        ClosePairingCommand = new RelayCommand(() => PairingOpen = false);
        shell.App.PropertyChanged += Changed;
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += Tick;
        timer.Start();
        Refresh();
    }
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];
    public PairingViewModel Pairing { get; }
    public bool HasDevices => Devices.Count != 0;
    public bool IsEmpty => !HasDevices;
    public bool PairingOpen { get => pairingOpen; set {Set(ref pairingOpen, value);Raise(nameof(AddPrimary));} }
    public bool AddPrimary=>!PairingOpen;
    public ICommand AddCommand { get; }
    public ICommand ClosePairingCommand { get; }
    void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(shell.App.Devices) or nameof(shell.App.IsPaused)) Refresh();
    }
    void Tick(object? sender, EventArgs e) { foreach (var device in Devices) device.RefreshTime(); }
    void Refresh()
    {
        var snapshots = shell.App.Devices;
        foreach (var old in Devices.Where(d => !snapshots.Any(s => s.Id == d.Id)).ToArray()) Devices.Remove(old);
        foreach (var snapshot in snapshots)
        {
            var card = Devices.FirstOrDefault(d => d.Id == snapshot.Id);
            if (card is null) Devices.Add(new(snapshot, shell)); else card.Update(snapshot);
        }
        Raise(nameof(HasDevices)); Raise(nameof(IsEmpty));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop(); timer.Tick -= Tick;
        shell.App.PropertyChanged -= Changed;
        Pairing.Dispose();
    }
}

public sealed class DeviceCardViewModel : ObservableObject
{
    DeviceSnapshot snapshot;
    readonly ShellViewModel shell;
    bool editing;
    string draft = "", error = "";
    public DeviceCardViewModel(DeviceSnapshot snapshot, ShellViewModel shell)
    {
        this.snapshot = snapshot; this.shell = shell;
        RenameCommand = new RelayCommand(() => { DraftName = Name; Error = ""; IsEditing = true; });
        SaveRenameCommand = new RelayCommand(SaveRename);
        CancelRenameCommand = new RelayCommand(() => { IsEditing = false; Error = ""; });
        ForgetCommand = new AsyncCommand(async () => {
            if (await shell.ConfirmAsync("Forget device?", $"Forget {Name}? To sync with it again, you will need to pair it with a new code."))
            { shell.App.ForgetDevice(Id); shell.App.Refresh(); shell.Notify("Device forgotten"); }
        }, shell.App.Report);
    }
    public string Id => snapshot.Id;
    public string Name => snapshot.Name;
    public bool SyncEnabled
    {
        get => snapshot.SyncEnabled;
        set { if (value == snapshot.SyncEnabled) return; try { shell.App.SetDeviceSync(Id, value); shell.App.Refresh(); } catch (Exception ex) { shell.App.Report(ex); Raise(); } }
    }
    public string Status => !snapshot.Connected ? "Unreachable" : shell.App.IsPaused || !snapshot.SyncEnabled ? "Paused" : "Connected";
    public System.Windows.Media.Brush StatusBrush => (System.Windows.Media.Brush)(System.Windows.Application.Current.MainWindow?.TryFindResource(Status == "Connected" ? "Connected" : Status == "Paused" ? "Paused" : "Danger") ?? System.Windows.Media.Brushes.Gray);
    public string Subtitle => $"{Status} · {Relative(snapshot.Connected ? snapshot.Since : snapshot.Last)}";
    public string Address => PlainIPv4(snapshot.Address);
    public string TimeLabel => snapshot.Connected ? "Connected since" : "Last seen";
    public string TimeValue => (snapshot.Connected ? snapshot.Since : snapshot.Last)?.LocalDateTime.ToString("g") ?? "Not available";
    public string Network => snapshot.Connected ? "Local network" : "Not connected";
    public bool IsEditing { get => editing; private set { Set(ref editing, value); Raise(nameof(IsNotEditing)); } }
    public bool IsNotEditing => !IsEditing;
    public string DraftName { get => draft; set => Set(ref draft, value); }
    public string Error { get => error; private set { Set(ref error, value); Raise(nameof(HasError)); } }
    public bool HasError => Error.Length != 0;
    public ICommand RenameCommand { get; }
    public ICommand SaveRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand ForgetCommand { get; }
    void SaveRename()
    {
        if (string.IsNullOrWhiteSpace(DraftName) || DraftName.Trim().Length > 80) { Error = "Use a name between 1 and 80 characters."; return; }
        try { shell.App.RenameDevice(Id, DraftName.Trim()); shell.App.Refresh(); IsEditing = false; Error = ""; shell.Notify("Device renamed"); }
        catch (Exception ex) { Error = ex.Message; shell.App.Report(ex); }
    }
    internal void Update(DeviceSnapshot value) { snapshot = value; foreach (var property in new[] { nameof(Name), nameof(SyncEnabled), nameof(Status), nameof(StatusBrush), nameof(Subtitle), nameof(Address), nameof(TimeLabel), nameof(TimeValue), nameof(Network) }) Raise(property); }
    internal void RefreshTime() => Raise(nameof(Subtitle));
    static string Relative(DateTimeOffset? time)
    {
        if (time is null) return "Time not available";
        var elapsed = DateTimeOffset.UtcNow - time.Value;
        return elapsed.TotalMinutes < 1 ? "Just now" : elapsed.TotalHours < 1 ? $"{(int)elapsed.TotalMinutes} min ago" : elapsed.TotalDays < 1 ? $"{(int)elapsed.TotalHours} hr ago" : $"{(int)elapsed.TotalDays} days ago";
    }
    public static string PlainIPv4(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "Not available";
        var host = address.Trim();
        if (host.StartsWith('[')) { var end = host.IndexOf(']'); if (end > 0) host = host[1..end]; }
        if (!IPAddress.TryParse(host, out var ip)) { var colon = host.LastIndexOf(':'); if (colon > 0) IPAddress.TryParse(host[..colon], out ip); }
        if (ip is null) return "Not available";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? ip.ToString() : "IPv4 not available";
    }
}
