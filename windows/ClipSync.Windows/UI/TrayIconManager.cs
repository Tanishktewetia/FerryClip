using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using ClipSync.Windows.Logging;

namespace ClipSync.Windows.UI;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly StatusWindow _statusWindow;

    public TrayIconManager(StatusWindow statusWindow, bool replayOnConnect = true, Action<bool>? replayChanged = null)
    {
        _statusWindow = statusWindow;
        _notifyIcon = new NotifyIcon
        {
            Text = "FerryClip — Waiting",
            Visible = true,
            Icon = CreateTrayIcon()
        };
        _notifyIcon.MouseUp += OnMouseUp;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Status", null, (_, _) => ScheduleShowStatus());
        var replay = new ToolStripMenuItem("Apply latest text on reconnect") { CheckOnClick = true, Checked = replayOnConnect };
        replay.CheckedChanged += (_, _) => replayChanged?.Invoke(replay.Checked);
        menu.Items.Add(replay);
        menu.Items.Add("Open Log Folder", null, OnOpenLogFolder);
        menu.Items.Add("Quit FerryClip (stop sharing)", null, OnExit);
        _notifyIcon.ContextMenuStrip = menu;
        FileLogger.Instance.Info("Tray icon created");
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ScheduleShowFromTray();
    }

    private void ScheduleShowFromTray()
    {
        _statusWindow.Dispatcher.BeginInvoke(
            new Action(() => _statusWindow.ShowPopover()),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ScheduleShowStatus()
    {
        _statusWindow.Dispatcher.BeginInvoke(
            new Action(() => _statusWindow.ShowPopover()),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnOpenLogFolder(object? sender, EventArgs e)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", FileLogger.Instance.LogDirectory); }
        catch (Exception ex) { FileLogger.Instance.Error("Failed to open log folder", ex); }
    }

    private void OnExit(object? sender, EventArgs e) => System.Windows.Application.Current.Shutdown();

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.Clear(Color.Transparent);
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ferryclip_logo.png"))
            ?? throw new FileNotFoundException("FerryClip tray logo resource is missing.");
        using (resource.Stream)
        using (var logo = new Bitmap(resource.Stream))
            graphics.DrawImage(logo, new Rectangle(1, 6, 30, 20));
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void ShowNotice(string message) => _notifyIcon.ShowBalloonTip(5000, "FerryClip", message, ToolTipIcon.Info);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _statusWindow.CloseForExit();
    }
}
