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
    private readonly MainUI.Services.AppState _state; private readonly Action _open; private readonly Action _pair; private readonly Action _pause;
    private readonly System.Windows.Controls.ContextMenu _menu;
    private readonly System.Windows.Controls.MenuItem _pauseItem;

    private System.Drawing.Point _openedAt;
    private IntPtr _menuHandle;
    private bool _menuHadForeground;
    private System.Windows.Interop.HwndSource? _menuOwner;
    private readonly System.Windows.Threading.DispatcherTimer _dismissTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public TrayIconManager(MainUI.Services.AppState state,Action open,Action pair,Action pause)
    {
        _state=state;_open=open;_pair=pair;_pause=pause;
        _menu = new System.Windows.Controls.ContextMenu
        {
            Style = (Style)WpfApplication.Current.FindResource("TrayMenuStyle"),
            Placement = PlacementMode.AbsolutePoint
        };
        AddItem("Open FerryClip",_open,"\uE8A7").FontWeight=FontWeights.SemiBold;
        _pauseItem=AddItem("Pause sync",_pause,"\uE769");
        AddItem("Pair new device",_pair,"\uE710");
        _menu.Items.Add(new System.Windows.Controls.Separator());
        var quit=AddItem("Quit FerryClip",QuitApplication,"\uE7E8");
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
        _notifyIcon.MouseUp += OnMouseUp;
        _notifyIcon.MouseDoubleClick += (_,e)=>{if(e.Button==MouseButtons.Left)_open();};
        _state.PropertyChanged += StateChanged;
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

    private void QuitApplication()
    {
        _menu.IsOpen = false;
        _dismissTimer.Stop();
        WpfApplication.Current.Dispatcher.BeginInvoke(new Action(() => WpfApplication.Current.Shutdown()), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        var cursor=Cursor.Position;
        WpfApplication.Current.Dispatcher.BeginInvoke(new Action(()=>{
            if(e.Button==MouseButtons.Left){_menu.IsOpen=false;_open();}
            else if(e.Button==MouseButtons.Right){if(_menu.IsOpen){_menu.IsOpen=false;return;}_openedAt=cursor;_menu.Tag=null;_menu.HorizontalOffset=cursor.X;_menu.VerticalOffset=cursor.Y;OpenMenu();}
        }),System.Windows.Threading.DispatcherPriority.ContextIdle);
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

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
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
                var inset=Math.Max(1,size/8);var extent=size-inset*2;var bounds=new Rectangle(inset,inset,extent,extent);
                graphics.DrawImage(logo,bounds,0,0,logo.Width,logo.Height,GraphicsUnit.Pixel,attributes);
            }
            else {var inset=Math.Max(1,size/8);var extent=size-inset*2;graphics.DrawImage(logo,new Rectangle(inset,inset,extent,extent));}
        }
        if(state=="Paused")
        {
            var badgeSize=Math.Max(6,size*0.4f);var badgeX=size-badgeSize;var badgeY=size-badgeSize;using var badge=new SolidBrush(Color.FromArgb(245,183,74));graphics.FillEllipse(badge,badgeX,badgeY,badgeSize,badgeSize);var barWidth=Math.Max(1,size/16);var barHeight=Math.Max(3,badgeSize*0.48f);var barGap=Math.Max(1,size/16);var barX=badgeX+(badgeSize-(barWidth*2+barGap))/2;var barY=badgeY+(badgeSize-barHeight)/2;using var ink=new SolidBrush(Color.FromArgb(11,42,82));graphics.FillRectangle(ink,barX,barY,barWidth,barHeight);graphics.FillRectangle(ink,barX+barWidth+barGap,barY,barWidth,barHeight);
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
    private string? _iconState;
    private string TrayState=>_state.State switch {MainUI.Models.ConnectionState.Connected=>"Connected",MainUI.Models.ConnectionState.Paused=>"Paused",MainUI.Models.ConnectionState.Unreachable=>"Not connected",_=>"No devices paired"};
    private void StateChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName is "State" or "Devices" or "IsPaused")UpdateState();}
    private void UpdateState()
    {
        var state=TrayState;
        var count=_state.Devices.Count(d=>d.Connected);
        var tip=state=="Connected" ? $"FerryClip - Connected, {count} {(count==1?"device":"devices")}" : $"FerryClip - {state}";_notifyIcon.Text=tip.Length>127?tip[..127]:tip;
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
        _state.PropertyChanged -= StateChanged;
        _dismissTimer.Stop();
        _menu.IsOpen = false;
        _menuHandle = IntPtr.Zero;
        var menuOwner = _menuOwner;
        _menuOwner = null;
        menuOwner?.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.MouseUp -= OnMouseUp;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();

    }
}
