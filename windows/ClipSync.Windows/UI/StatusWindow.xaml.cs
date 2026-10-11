using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClipSync.Windows.Transport;
using Button = System.Windows.Controls.Button;
using Forms = System.Windows.Forms;
namespace ClipSync.Windows.UI;
public partial class StatusWindow : Window
{
    private readonly Action _pair;
    private readonly PopoverVisibility _visibility = new();
    private bool _suppressDeactivation, _closingForExit, _positioning, _pairExpanded, _paused;
    private Forms.Screen? _anchorScreen;
    private readonly DispatcherTimer _deactivationGuard;
    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromSeconds(1) };
    private IReadOnlyList<SyncServer.PhoneState> _phones = Array.Empty<SyncServer.PhoneState>();
    private bool _lastCodeExpired;
    private string? _code;
    private DateTimeOffset? _expires;
    public Action? PauseRequested { get; set; }
    public string State { get; private set; } = "No devices paired";
    public int ConnectedCount => _phones.Count(p => p.Connected);
    public event Action? StatusChanged;
    public StatusWindow(Action pair)
    {
        _pair = pair;
        InitializeComponent();
        BuildLabel.Text = $"v{typeof(StatusWindow).Assembly.GetName().Version?.ToString(4)}";
        _deactivationGuard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _deactivationGuard.Tick += (_, _) => { _deactivationGuard.Stop(); _suppressDeactivation = false; if (!IsActive) HidePopover(); };
        _countdown.Tick += (_, _) => UpdateCountdown();
        Closed += (_, _) => _countdown.Stop();
        SizeChanged += (_, _) => { if (IsVisible && _visibility.VisibleRequested && !_positioning) PositionNearTray(); };
        RefreshState();
    }
    public void SetPhones(IReadOnlyList<SyncServer.PhoneState> phones) { var wasPaired=_phones.Count>0;_phones = phones; RefreshState();if(wasPaired && phones.Count==0)ExpandPairing(true); }
    public void SetPaused(bool paused) { _paused = paused; RefreshState(); }
    public void SetStatus(string state, string detail) { RefreshState(); }
    private void RefreshState()
    {
        State = _paused ? "Paused" : _phones.Count == 0 ? "No devices paired" : ConnectedCount > 0 ? "Connected" : "Not connected";
        StatusText.Text = State;
        StatusDetail.Text = State switch
        {
            "Paused" => "Clipboard sync is paused",
            "Connected" => $"{ConnectedCount} {(ConnectedCount == 1 ? "device" : "devices")} on your local network",
            "No devices paired" => "Pair your phone to start syncing",
            _ => "Reconnecting on the local network..."
        };
        StatusDot.Fill = Brush(State == "Connected" ? "ConnectedBrush" : State == "Paused" ? "PausedBrush" : State == "Not connected" ? "ErrorBrush" : "TextSecondaryBrush");
        var tint = ((SolidColorBrush)StatusDot.Fill).Color;
        var surface = ((SolidColorBrush)Brush("SurfaceBrush")).Color;
        StatusCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(surface.R*.92+tint.R*.08),(byte)(surface.G*.92+tint.G*.08),(byte)(surface.B*.92+tint.B*.08)));
        SyncButton.Visibility = State is "Connected" or "Paused" ? Visibility.Visible : Visibility.Collapsed;
        SyncButton.Content = ActionContent(_paused ? "Resume" : "Pause", _paused ? "\uE768" : "\uE769");
        PhoneTiles.Children.Clear();
        DevicesSection.Visibility = _phones.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var phone in _phones.Take(3))
        {
            // Device navigation opens the full main window.
            var row = new Border { Background=Brush("SurfaceBrush"), CornerRadius=new CornerRadius(8), Padding=new Thickness(8,4,8,4), Margin=new Thickness(0,0,0,4) };
            var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(28) }); grid.ColumnDefinitions.Add(new());
            grid.Children.Add(MakeIcon("\uE8EA"));
            var labels = new StackPanel(); Grid.SetColumn(labels,1);
            labels.Children.Add(new TextBlock { Text=phone.Name, Foreground=Brush("TextPrimaryBrush"), FontWeight=FontWeights.SemiBold, FontSize=14, TextTrimming=TextTrimming.CharacterEllipsis });
            var status = _paused || phone.Paused ? "Paused" : phone.Connected ? "Connected" : "Unreachable";
            var minutes = phone.Last is {} last ? Math.Max(0,(int)(DateTimeOffset.Now-last).TotalMinutes) : -1;
            var subtitle = new TextBlock { FontSize=13, Margin=new Thickness(0,4,0,0), Foreground=Brush("TextSecondaryBrush") };
            subtitle.Inlines.Add(new System.Windows.Documents.Run(status) { Foreground=Brush(status=="Connected"?"ConnectedBrush":status=="Paused"?"PausedBrush":"ErrorBrush") });
            subtitle.Inlines.Add(" · " + (minutes < 0 ? "Not connected yet" : minutes == 0 ? "Just now" : $"{minutes} min ago"));
            labels.Children.Add(subtitle); grid.Children.Add(labels);
            row.Cursor=System.Windows.Input.Cursors.Hand; row.ToolTip="Open Devices"; row.MouseLeftButtonUp+=(_,_)=>DesktopActions.OpenDevices(); row.Child=grid; PhoneTiles.Children.Add(row);
        }
        if (_phones.Count > 3) PhoneTiles.Children.Add(new TextBlock { Text=$"+{_phones.Count-3} more", FontSize=13, Foreground=Brush("TextSecondaryBrush"), Margin=new Thickness(8,4,0,4) });
        if (_phones.Count == 0 && !IsVisible) ExpandPairing(true, false);
        UpdateButtons(); StatusChanged?.Invoke();
    }
    private System.Windows.Media.Brush Brush(string name) => (System.Windows.Media.Brush)FindResource(name);
    private TextBlock MakeIcon(string glyph) => new() { Text=glyph, Foreground=Brush("TextPrimaryBrush"), Style=(Style)FindResource("PopoverIcon") };
    private StackPanel ActionContent(string text,string glyph) { var p=new StackPanel { Orientation=System.Windows.Controls.Orientation.Horizontal }; var icon=MakeIcon(glyph);icon.Margin=new Thickness(0,0,6,0);p.Children.Add(icon);p.Children.Add(new TextBlock { Text=text, VerticalAlignment=VerticalAlignment.Center });return p; }
    private void UpdateButtons()
    {
        var activeCode = _code is not null && _expires > DateTimeOffset.UtcNow;
        var generatePrimary = _pairExpanded && !activeCode;
        GenerateButton.Style = (Style)FindResource(generatePrimary ? "PopoverPrimaryButton" : "PopoverButton");
        GenerateButton.Content = activeCode ? "Regenerate" : "Generate code";
        SyncButton.Style = (Style)FindResource(_paused && !generatePrimary ? "PopoverPrimaryButton" : "PopoverButton");
        OpenButton.Style = (Style)FindResource(_paused || generatePrimary ? "PopoverButton" : "PopoverPrimaryButton");
    }
    public void SetPairCode(string code, DateTimeOffset? expires = null)
    {
        _code = code.Length == 6 && code.All(char.IsDigit) ? code : null;
        _expires = _code is null ? null : expires;
        if (_code is not null) { _lastCodeExpired=false; _countdown.Start(); }
        else { _countdown.Stop(); _lastCodeExpired=code.Contains("timed out",StringComparison.OrdinalIgnoreCase); }
        UpdateCountdown();
        if(_code is not null) ExpandPairing(true);
    }
    private void UpdateCountdown()
    {
        if(_code is not null && _expires <= DateTimeOffset.UtcNow)
        {
            _code=null;_expires=null;_lastCodeExpired=true;_countdown.Stop();
        }
        var active=_code is not null && _expires is not null;
        ActiveCodeRow.Visibility=active ? Visibility.Visible : Visibility.Collapsed;
        PairCodeText.Text=active ? _code![..3]+" "+_code[3..] : "";
        PairHelpText.Text=active ? "Enter this code on your phone" : "Creates a six-digit code to pair your phone";
        PairExpiryText.Visibility=active || _lastCodeExpired ? Visibility.Visible : Visibility.Collapsed;
        PairExpiryText.Foreground=Brush(!active && _lastCodeExpired ? "PausedBrush" : "TextSecondaryBrush");
        if(active)
        {
            var seconds=Math.Max(0,(int)Math.Ceiling((_expires!.Value-DateTimeOffset.UtcNow).TotalSeconds));
            PairExpiryText.Text=$"Expires in {seconds/60:00}:{seconds%60:00}";
        }
        else PairExpiryText.Text=_lastCodeExpired ? "Last code expired" : "";
        UpdateButtons();
        if(_pairExpanded) ResizePairing(true);
    }
    public void ShowPairing() { ShowPopover(); ExpandPairing(true); }
    private void ExpandPairing(bool expanded, bool animate = true)
    {
        _pairExpanded=expanded;
        PairChevron.Text=expanded ? "\uE70E" : "\uE70D";
        System.Windows.Automation.AutomationProperties.SetName(PairToggle,expanded?"Collapse pairing panel":"Expand pairing panel");
        ResizePairing(animate);UpdateButtons();
    }
    private void ResizePairing(bool animate)
    {
        PairContent.Measure(new System.Windows.Size(Math.Max(1,Width-42),double.PositiveInfinity));
        var target=_pairExpanded ? PairContent.DesiredSize.Height : 0;
        var current=PairPanel.ActualHeight;
        if(Math.Abs(current-target)<.5)return;
        PairPanel.BeginAnimation(HeightProperty,null);
        PairPanel.Height=target;
        if(animate && IsVisible && SystemParameters.ClientAreaAnimation)
            PairPanel.BeginAnimation(HeightProperty,new DoubleAnimation(current,target,TimeSpan.FromMilliseconds(150)) { FillBehavior=FillBehavior.Stop });
    }    private void PairToggle_Click(object sender,RoutedEventArgs e) => ExpandPairing(!_pairExpanded);
    private void Sync_Click(object sender,RoutedEventArgs e) => PauseRequested?.Invoke();
    private void CopyCode_Click(object sender,RoutedEventArgs e) { if(_code is null)return;try { System.Windows.Clipboard.SetText(_code);CopyCodeButton.ToolTip="Code copied"; }catch(System.Runtime.InteropServices.COMException){CopyCodeButton.ToolTip="Clipboard busy. Try again.";} }
    private void Window_KeyDown(object sender,System.Windows.Input.KeyEventArgs e) { if(e.Key!=Key.Escape)return;if(_pairExpanded)ExpandPairing(false);else HidePopover();e.Handled=true; }
    public void TogglePopover() { if (_visibility.VisibleRequested) HidePopover(); else ShowPopover(); }

    public void ShowPopover()
    {
        if (_visibility.VisibleRequested && IsVisible) { Activate(); return; }
        if (_phones.Count == 0) ExpandPairing(true, false);
        _visibility.Show();
        BeginAnimation(OpacityProperty, null); // Cancels any fade-out and its outdated completion.
        _deactivationGuard.Stop(); _suppressDeactivation = true;
        _anchorScreen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        _positioning = true;
        try {
            Opacity = 0;
            _ = new WindowInteropHelper(this).EnsureHandle();
            ConstrainToScreen();
            if (!IsVisible) Show();
            Keyboard.ClearFocus(); FocusManager.SetFocusedElement(this,null);
            UpdateLayout(); // SizeToContent has resolved ActualHeight on the FIRST opening.
        } finally { _positioning = false; }
        PositionNearTray();
        Activate();
        if (SystemParameters.ClientAreaAnimation)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        else Opacity = 1;
        _deactivationGuard.Start();
    }
    public void HidePopover()
    {
        ExpandPairing(false, false);
        var revision = _visibility.Hide();
        _deactivationGuard.Stop(); _suppressDeactivation = false;
        if (!IsVisible) return;
        BeginAnimation(OpacityProperty, null);
        if (!SystemParameters.ClientAreaAnimation) { Hide(); return; }
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(90));
        fade.Completed += (_, _) => { if (_visibility.CanFinishHide(revision)) Hide(); };
        BeginAnimation(OpacityProperty, fade);
    }
    private void ConstrainToScreen()
    {
        var area = (_anchorScreen ?? Forms.Screen.PrimaryScreen!).WorkingArea;
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        MaxHeight = Math.Min(620, Math.Max(200, (area.Height - 24) / scale.M22));
        Width = Math.Max(240, Math.Min(400, (area.Width - 24) / scale.M11));
    }
    private void PositionNearTray()
    {
        if (_positioning) return;
        _positioning = true;
        try {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            // Repeat once after crossing a DPI boundary so layout/device scaling agrees.
            for (var pass = 0; pass < 2; pass++) {
                ConstrainToScreen(); UpdateLayout();
                var area = (_anchorScreen ?? Forms.Screen.PrimaryScreen!).WorkingArea;
                var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
                var target = PopoverPlacement.BottomRight(new(area.Left, area.Top, area.Width, area.Height), ActualWidth * transform.M11, ActualHeight * transform.M22);
                SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(target.Left), (int)Math.Round(target.Top), 0, 0, 0x0001 | 0x0004 | 0x0010);
            }
        } finally { _positioning = false; }
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closingForExit) { e.Cancel = true; HidePopover(); }
        base.OnClosing(e);
    }
    public void CloseForExit() { _closingForExit = true; _deactivationGuard.Stop(); Close(); }
    private long _deactivatedAt;
    public bool IsPopoverRequested => _visibility.VisibleRequested;
    public bool WasJustDeactivated => Environment.TickCount64 - _deactivatedAt < 250;
    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_suppressDeactivation) { _deactivatedAt = Environment.TickCount64; HidePopover(); }
    }
    private void Hide_Click(object sender, RoutedEventArgs e) => HidePopover();
    private void Pair_Click(object sender, RoutedEventArgs e) => _pair();
    private void OpenFerryClip_Click(object sender, RoutedEventArgs e) => DesktopActions.OpenFerryClip();
}
