using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using ClipSync.Windows.Logging;
using WpfApplication = System.Windows.Application;

namespace ClipSync.Windows.UI;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly StatusWindow _statusWindow;
    private readonly System.Windows.Controls.ContextMenu _menu;
    private readonly System.Windows.Controls.MenuItem _pauseItem;
    private bool _hideOnClick;
    private System.Drawing.Point _openedAt;
    private IntPtr _menuHandle;
    private bool _menuHadForeground;
    private System.Windows.Interop.HwndSource? _menuOwner;
    private readonly System.Windows.Threading.DispatcherTimer _dismissTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public TrayIconManager(StatusWindow statusWindow)
    {
        _statusWindow = statusWindow;
        _menu = new System.Windows.Controls.ContextMenu
        {
            Style = (Style)WpfApplication.Current.FindResource("TrayMenuStyle"),
            Placement = PlacementMode.AbsolutePoint
        };
        AddItem("Open FerryClip", DesktopActions.OpenFerryClip, "\uE8A7").FontWeight = FontWeights.SemiBold;
        AddItem("Show status", () => _statusWindow.ShowPopover(), "\uE946");
        _pauseItem = AddItem("Pause sync", () => _statusWindow.PauseRequested?.Invoke(), "\uE769", startsGroup: true);
        AddItem("Pair new device", () => _statusWindow.ShowPairing(), "\uE710");
#if DEBUG
        AddItem("Open log folder", DesktopActions.OpenLogFolder, "\uE838", startsGroup: true);
        var quit = AddItem("Quit FerryClip", () => WpfApplication.Current.Shutdown(), "\uE7E8");
#else
        var quit = AddItem("Quit FerryClip", () => WpfApplication.Current.Shutdown(), "\uE7E8", startsGroup: true);
#endif
        quit.Tag = "Destructive";
        _menu.StaysOpen = false;
        _menu.Opened += (_, _) => PositionAndActivateMenu();
        _menu.Closed += (_, _) => { _dismissTimer.Stop(); _menuHandle = IntPtr.Zero; _menuOwner?.Dispose(); _menuOwner = null; };
        _menu.PreviewMouseMove += (_, _) => { if (Cursor.Position != _openedAt) _menu.Tag = "Pointer"; };
        _menu.PreviewKeyDown += (_, e) =>
        {
            _menu.Tag = "Keyboard";
            if (e.Key == System.Windows.Input.Key.Escape) _menu.IsOpen = false;
        };
        _dismissTimer.Tick += (_, _) =>
        {
            var foreground = GetForegroundWindow();
            var ownsForeground = foreground == _menuHandle || foreground == _menuOwner?.Handle;
            if (ownsForeground) _menuHadForeground = true;
            var outsideClick = (GetAsyncKeyState(1) < 0 || GetAsyncKeyState(2) < 0) && !_menu.IsMouseOver;
            if (_menuHandle != IntPtr.Zero && (outsideClick || (_menuHadForeground && !ownsForeground && !IsChild(_menuHandle, foreground)))) _menu.IsOpen = false;
        };
        _notifyIcon = new NotifyIcon { Text = "FerryClip", Visible = true, Icon = CreateTrayIcon("No devices paired") };
        _notifyIcon.MouseDown += OnMouseDown;
        _notifyIcon.MouseUp += OnMouseUp;
        _statusWindow.StatusChanged += UpdateState;
        UpdateState();
        FileLogger.Instance.Info("Tray icon created");
    }

    private System.Windows.Controls.MenuItem AddItem(string label, Action action, string glyph, bool startsGroup = false)
    {
        var item = new System.Windows.Controls.MenuItem { Header = label, Margin = new Thickness(0, startsGroup ? 6 : 0, 0, 0), Icon = new System.Windows.Controls.TextBlock { Text=glyph, FontFamily=new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize=14 }, Style = (Style)WpfApplication.Current.FindResource("TrayMenuItemStyle") };
        item.Click += (_, _) => { _menu.IsOpen = false; action(); };
        _menu.Items.Add(item);
        return item;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _hideOnClick = _statusWindow.IsPopoverRequested || _statusWindow.WasJustDeactivated;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        var cursor = Cursor.Position;
        _statusWindow.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _menu.IsOpen = false;
                if (_hideOnClick) _statusWindow.HidePopover(); else _statusWindow.ShowPopover();
            }
            else if (e.Button == MouseButtons.Right)
            {
                _statusWindow.HidePopover();
                if (_menu.IsOpen) { _menu.IsOpen = false; return; }
                _openedAt = cursor;
                _menu.Tag = null;
                // Seed WPF placement in DIPs; final positioning below uses the popup's own DPI.
                _menu.HorizontalOffset = cursor.X / (_statusWindow.IsLoaded ? System.Windows.Media.VisualTreeHelper.GetDpi(_statusWindow).DpiScaleX : 1);
                _menu.VerticalOffset = cursor.Y / (_statusWindow.IsLoaded ? System.Windows.Media.VisualTreeHelper.GetDpi(_statusWindow).DpiScaleY : 1);                OpenMenu();
            }
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private void OpenMenu()
    {
        _menuOwner ??= new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("FerryClip tray menu owner")
        {
            Width = 1, Height = 1, PositionX = -32000, PositionY = -32000,
            WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x80
        });
        ShowWindow(_menuOwner.Handle, 5);
        SetForegroundWindow(_menuOwner.Handle);
        _menu.IsOpen = true;
    }
    private void PositionAndActivateMenu()
    {
        _menuHadForeground = false;
        _menu.UpdateLayout();
        var source = System.Windows.PresentationSource.FromVisual(_menu) as System.Windows.Interop.HwndSource;
        if (source is null) return;
        _menuHandle = source.Handle;
        var dpi = GetDpiForWindow(_menuHandle) / 96.0;
        var area = Screen.FromPoint(_openedAt).WorkingArea;
        var width = (int)Math.Ceiling(_menu.ActualWidth * dpi);
        var height = (int)Math.Ceiling(_menu.ActualHeight * dpi);
        var x = Math.Clamp(_openedAt.X - width / 2, area.Left, Math.Max(area.Left, area.Right - width));
        var y = Math.Clamp(_openedAt.Y - height - 6, area.Top, Math.Max(area.Top, area.Bottom - height));
        SetWindowPos(_menuHandle, new IntPtr(-1), x, y, 0, 0, 0x0001 | 0x0010);
        // Required by Windows for tray menus to dismiss when another app is clicked.
        // Keep the foreground owner stable while WPF holds menu mouse capture.
        _dismissTimer.Start();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    private static Icon CreateTrayIcon(string state, int size = 32)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.ScaleTransform(size / 32f, size / 32f);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.Clear(Color.Transparent);
        var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/FerryClip;component/Assets/ferryclip_logo.png"))
            ?? throw new FileNotFoundException("FerryClip tray logo resource is missing.");
        using (resource.Stream)
        using (var logo = new Bitmap(resource.Stream))
        {
            if(state is "Not connected" or "No devices paired")
            {
                using var attributes=new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new[] {
                    new float[] {.299f,.299f,.299f,0,0},new float[] {.587f,.587f,.587f,0,0},new float[] {.114f,.114f,.114f,0,0},new float[] {0,0,0,1,0},new float[] {0,0,0,0,1} }));
                graphics.DrawImage(logo,new Rectangle(1,6,30,20),0,0,logo.Width,logo.Height,GraphicsUnit.Pixel,attributes);
            }
            else graphics.DrawImage(logo,new Rectangle(1,6,30,20));
        }
        if(state=="Paused")
        {
            using var badge=new SolidBrush(Color.FromArgb(245,183,74));graphics.FillEllipse(badge,19,19,13,13);
            using var ink=new SolidBrush(Color.FromArgb(11,42,82));graphics.FillRectangle(ink,23,22,2,7);graphics.FillRectangle(ink,27,22,2,7);
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
    private string? _iconState;
    private void UpdateState()
    {
        var state=_statusWindow.State;
        var count=_statusWindow.ConnectedCount;
        _notifyIcon.Text=state=="Connected" ? $"FerryClip · Connected · {count} {(count==1?"device":"devices")}" : $"FerryClip · {state}";
        var pause=_pauseItem;pause.Header=state=="Paused"?"Resume sync":"Pause sync";
        ((System.Windows.Controls.TextBlock)pause.Icon).Text=state=="Paused"?"\uE768":"\uE769";
        if(_iconState!=state) { var old=_notifyIcon.Icon;
            using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
            {
                var sizes=new[]{16,20,24,32};var images=new List<byte[]>();
                foreach(var size in sizes){using var icon=CreateTrayIcon(state,size);using var bytes=new MemoryStream();icon.Save(bytes);images.Add(bytes.ToArray());}
                writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)4);var offset=6+16*4;
                for(var i=0;i<4;i++){var data=images[i];writer.Write((byte)sizes[i]);writer.Write((byte)sizes[i]);writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(data.Length-22);writer.Write(offset);offset+=data.Length-22;}
                foreach(var data in images)writer.Write(data,22,data.Length-22);
            }
            stream.Position=0;using var variants=new Icon(stream);_notifyIcon.Icon=(Icon)variants.Clone();old?.Dispose();_iconState=state; }
    }
    public void ShowNotice(string message) => _notifyIcon.ShowBalloonTip(5000, "FerryClip", message, ToolTipIcon.Info);
    public void Dispose()
    {
        _statusWindow.StatusChanged -= UpdateState;
        _dismissTimer.Stop();
        _menu.IsOpen = false;
        _notifyIcon.Visible = false;
        _notifyIcon.MouseDown -= OnMouseDown;
        _notifyIcon.MouseUp -= OnMouseUp;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
        _statusWindow.CloseForExit();
    }
}
