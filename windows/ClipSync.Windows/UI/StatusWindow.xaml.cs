using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClipSync.Windows.Logging;
using MediaBrush = System.Windows.Media.Brush;
using Forms = System.Windows.Forms;

namespace ClipSync.Windows.UI;

public partial class StatusWindow : Window
{
    public Action<string,bool>? PhoneEnabledRequested { get; set; }
    public Action<string,bool>? PhonePausedRequested { get; set; }
    public Action<string>? PhoneForgetRequested { get; set; }
    public Action<string,string>? PhoneRenameRequested { get; set; }
    private readonly Dictionary<string,System.Windows.Controls.Expander> _phoneTiles = new();
    private readonly Dictionary<string,ClipSync.Windows.Transport.SyncServer.PhoneState> _phoneStates = new();
    public void SetPhones(IReadOnlyList<ClipSync.Windows.Transport.SyncServer.PhoneState> phones) {
        DevicesSection.Visibility=phones.Count>0?Visibility.Visible:Visibility.Collapsed;
        foreach(var id in _phoneTiles.Keys.Except(phones.Select(p=>p.Id)).ToArray()) { var old=_phoneTiles[id]; PhoneTiles.Children.Remove(old.Parent as System.Windows.UIElement ?? old); _phoneTiles.Remove(id); _phoneStates.Remove(id); }
        foreach(var phone in phones) {
            _phoneStates[phone.Id]=phone;
            if(!_phoneTiles.TryGetValue(phone.Id,out var tile)) { tile=CreatePhoneTile(phone.Id); _phoneTiles[phone.Id]=tile; PhoneTiles.Children.Add(new System.Windows.Controls.Border { Child=tile, CornerRadius=new CornerRadius(12), Background=(MediaBrush)FindResource("SurfaceBrush"), BorderBrush=(MediaBrush)FindResource("BorderBrush"), BorderThickness=new Thickness(1), Padding=new Thickness(12), Margin=new Thickness(0,0,0,10) }); }
            var header=(System.Windows.Controls.Grid)tile.Header; var labels=(System.Windows.Controls.StackPanel)header.Children[0];
            ((System.Windows.Controls.TextBlock)labels.Children[0]).Text=phone.Name;
            var mins=phone.Last is {} at ? Math.Max(0,(int)(DateTimeOffset.Now-at).TotalMinutes) : -1;
            ((System.Windows.Controls.TextBlock)labels.Children[1]).Text=mins<0?"Not connected yet":mins==0?"Just now":$"{mins} min ago";
            var status=(System.Windows.Controls.TextBlock)header.Children[1]; status.Text=(phone.Paused?"Paused":phone.Connected?"Connected":"Disconnected")+(tile.IsExpanded?"  ⌃":"  ⌄");
            status.Foreground=(MediaBrush)FindResource(phone.Paused?"PausedBrush":phone.Connected?"ConnectedBrush":"TextSecondaryBrush");
            var body=(System.Windows.Controls.StackPanel)tile.Content;
            ((System.Windows.Controls.TextBlock)body.Children[0]).Text=$"Address: {phone.Address ?? "—"}\nDevice: {phone.Name}\nNetwork: Local network\nConnected since: {phone.Since?.ToString("t") ?? "—"}";
            ((System.Windows.Controls.Button)body.Children[2]).Content=phone.Enabled?"Disconnect":"Connect";
            ((System.Windows.Controls.Button)body.Children[3]).Content=phone.Paused?"▶ Resume":"Ⅱ Pause";
        }
    }
    private System.Windows.Controls.Expander CreatePhoneTile(string id) {
        var tile=new System.Windows.Controls.Expander { Margin=new Thickness(0,0,0,10), Padding=new Thickness(12), Foreground=(MediaBrush)FindResource("TextPrimaryBrush"), Background=(MediaBrush)FindResource("SurfaceBrush") };
        tile.Template=PhoneTile.Template;
        var header=new System.Windows.Controls.Grid { MinWidth=240 }; header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new(){ Width=GridLength.Auto });
        var labels=new System.Windows.Controls.StackPanel(); labels.Children.Add(new System.Windows.Controls.TextBlock{FontSize=16,FontWeight=FontWeights.SemiBold}); labels.Children.Add(new System.Windows.Controls.TextBlock{FontSize=11,Foreground=(MediaBrush)FindResource("TextSecondaryBrush")}); header.Children.Add(labels);
        var status=new System.Windows.Controls.TextBlock{FontSize=11,VerticalAlignment=VerticalAlignment.Center}; System.Windows.Controls.Grid.SetColumn(status,1); header.Children.Add(status); tile.Header=header;
        var body=new System.Windows.Controls.StackPanel{Margin=new Thickness(0,12,0,0)};
        body.Children.Add(new System.Windows.Controls.TextBlock{FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=24,Margin=new Thickness(0,0,0,10)});
        void Button(string text,Action action,string style) { var b=new System.Windows.Controls.Button{Content=text,Style=(Style)FindResource(style),Margin=new Thickness(0,0,0,6)}; b.Click+=(_,_)=>action(); body.Children.Add(b); }
        Button("✎ Rename",()=> { var dialog=new Window{Title="Rename phone",Width=320,SizeToContent=SizeToContent.Height,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner}; var input=new System.Windows.Controls.TextBox{Text=_phoneStates[id].Name,MaxLength=48,Margin=new Thickness(12)}; var stack=new System.Windows.Controls.StackPanel(); stack.Children.Add(input); var save=new System.Windows.Controls.Button{Content="Save",Margin=new Thickness(12)}; save.Click+=(_,_)=>{try{PhoneRenameRequested?.Invoke(id,input.Text);dialog.Close();}catch(Exception){System.Windows.MessageBox.Show(dialog,"Use 1–48 printable characters.");}}; stack.Children.Add(save); dialog.Content=stack; _suppressDeactivation=true; try{dialog.ShowDialog();}finally{_suppressDeactivation=false;} },"SecondaryButtonStyle");
        Button("Disconnect",()=>PhoneEnabledRequested?.Invoke(id,!_phoneStates[id].Enabled),"PrimaryButtonStyle");
        Button("Ⅱ Pause",()=>PhonePausedRequested?.Invoke(id,!_phoneStates[id].Paused),"ActionButtonStyle");
        Button("Forget device",()=> { _suppressDeactivation=true; try{if(System.Windows.MessageBox.Show(this,"Forget this phone? Other phones remain paired.","Forget phone",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes) PhoneForgetRequested?.Invoke(id);}finally{_suppressDeactivation=false;} },"ActionButtonStyle");
        tile.Content=body; tile.Expanded+=(_,_)=> { CollapseOtherTiles(tile,PhoneTiles); SetPhones(_phoneStates.Values.ToList()); }; tile.Collapsed+=(_,_)=>SetPhones(_phoneStates.Values.ToList()); return tile;
    }
    private readonly Action _pair;
    public Action? ConnectionRequested { get; set; }
    public Action<string>? RenameRequested { get; set; }
    private DateTimeOffset? _phoneLastConnected;
    public void SetPhone(bool paired, string name, bool connected, bool enabled, string? address = null, DateTimeOffset? since = null, DateTimeOffset? last = null) {
        DevicesSection.Visibility = paired ? Visibility.Visible : Visibility.Collapsed;
        PhoneNameText.Text = name; PhoneDeviceText.Text = "Device: " + name;
        if (!PhoneNameInput.IsKeyboardFocusWithin) PhoneNameInput.Text = name;
        PhoneStateText.Text = connected ? "Connected" : "Disconnected";
        var brush = (MediaBrush)FindResource(connected ? "ConnectedBrush" : "TextSecondaryBrush"); PhoneStateText.Foreground = brush; PhoneDot.Foreground = brush;
        PhoneAddressText.Text = "Address: " + (address ?? "—"); PhoneSinceText.Text = "Connected since: " + (since?.ToString("t") ?? "—");
        _phoneLastConnected = last; RefreshPhoneTime(); ConnectionButton.Content = enabled ? "Disconnect" : "Connect";
    }
    private void RefreshPhoneTime() { var minutes = _phoneLastConnected is {} at ? Math.Max(0, (int)(DateTimeOffset.Now-at).TotalMinutes) : -1; PhoneTimeText.Text = minutes < 0 ? "Not connected yet" : minutes == 0 ? "Just now" : minutes < 60 ? $"{minutes} min ago" : $"{minutes/60} h ago"; }
    private void EditName_Click(object sender, RoutedEventArgs e) { RenamePanel.Visibility = RenamePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if(RenamePanel.IsVisible) PhoneNameInput.Focus(); }
    internal static void CollapseOtherTiles(System.Windows.Controls.Expander selected, System.Windows.DependencyObject container) {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(container);i++) {
            var child=VisualTreeHelper.GetChild(container,i);
            if(child is System.Windows.Controls.Expander tile && tile != selected) tile.IsExpanded=false;
            CollapseOtherTiles(selected,child);
        }
    }
    private void DeviceTile_Expanded(object sender, RoutedEventArgs e) { if(e.OriginalSource is System.Windows.Controls.Expander tile) CollapseOtherTiles(tile,DevicesSection); }
    private void Connection_Click(object sender, RoutedEventArgs e) => ConnectionRequested?.Invoke();
    private void Rename_Click(object sender, RoutedEventArgs e) { try { RenameRequested?.Invoke(PhoneNameInput.Text); RenamePanel.Visibility = Visibility.Collapsed; } catch (Exception) { System.Windows.MessageBox.Show(this, "Could not save the name. Use 1–48 printable characters and check the log folder if this continues.", "Rename phone"); } }
    private readonly Action? _forget;
    private readonly Func<bool> _pause;
    private readonly Func<bool, bool>? _startupChanged;
    private bool _updatingStartup;
    private readonly PopoverVisibility _visibility = new();
    private bool _suppressDeactivation;
    private bool _closingForExit;
    private bool _positioning;
    private Forms.Screen? _anchorScreen;
    private readonly DispatcherTimer _deactivationGuard;

    public StatusWindow(Action pair, Func<bool> pause, Action? forget = null, bool startWithWindows = false, Func<bool, bool>? startupChanged = null)
    {
        _pair = pair; _pause = pause; _forget = forget; _startupChanged = startupChanged;
        InitializeComponent();
        _updatingStartup = true;
        StartWithWindowsCheckBox.IsChecked = startWithWindows;
        _updatingStartup = false;

        BuildLabel.Text = $"v{typeof(StatusWindow).Assembly.GetName().Version?.ToString(4)} · Text only";
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowActivated = true;
        _deactivationGuard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _deactivationGuard.Tick += (_, _) => { _deactivationGuard.Stop(); _suppressDeactivation = false; };
        var clock = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) }; clock.Tick += (_, _) => RefreshPhoneTime(); clock.Start(); Closed += (_, _) => clock.Stop();
        SizeChanged += (_, _) => { if (IsVisible && _visibility.VisibleRequested && !_positioning) PositionNearTray(); };
    }
    public void SetStatus(string state, string detail)
    {
        StatusText.Text = state; StatusDetail.Text = detail;
        var (icon, brush) = state switch {
            "Connected" => ("●", "ConnectedBrush"), "Paused" => ("Ⅱ", "PausedBrush"),
            "Error" => ("!", "ErrorBrush"), _ => ("◌", "WaitingBrush")
        };
        StatusIcon.Text = icon; StatusIcon.Foreground = (MediaBrush)FindResource(brush);
        PauseButton.Content = state == "Paused" ? "▶ Resume" : "Ⅱ Pause";
    }
    public void SetPairCode(string code) {
        var isCode = code.All(char.IsDigit) && code.Length == 6;
        PairCodeText.Text = isCode ? code : "— — — — — —";
        PairHelpText.Text = isCode ? "Enter this code in FerryClip on your phone."
            : code.Contains("identity rejected", StringComparison.OrdinalIgnoreCase) ? "Only forget the pairing if you intend to replace the saved phone identity."
            : string.IsNullOrWhiteSpace(code) ? "Enter the temporary code on your phone." : code;
    }
    public void TogglePopover() { if (_visibility.VisibleRequested) HidePopover(); else ShowPopover(); }

    public void ShowPopover()
    {
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
            UpdateLayout(); // SizeToContent has resolved ActualHeight on the FIRST opening.
        } finally { _positioning = false; }
        PositionNearTray();
        Activate(); Focus();
        if (SystemParameters.ClientAreaAnimation)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        else Opacity = 1;
        _deactivationGuard.Start();
    }
    public void HidePopover()
    {
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
        MaxHeight = Math.Max(200, (area.Height - 24) / scale.M22);
        Width = Math.Max(240, Math.Min(360, (area.Width - 24) / scale.M11));
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
    private void Window_Deactivated(object sender, EventArgs e) { if (!_suppressDeactivation) HidePopover(); }
    private void Hide_Click(object sender, RoutedEventArgs e) => HidePopover();
    private void Forget_Click(object sender, RoutedEventArgs e)
    {
        if (_forget == null) return;
        _suppressDeactivation = true;
        try {
            if (System.Windows.MessageBox.Show(this,
                "Disconnect and forget the saved phone? You will need to enter a new connection code before it can reconnect. No other phone will be trusted automatically.",
                "Forget paired phone?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                _forget();
        } finally { _suppressDeactivation = false; }
    }
    private void Pair_Click(object sender, RoutedEventArgs e) => _pair();
    private void Pause_Click(object sender, RoutedEventArgs e) => _pause();
    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingStartup || _startupChanged is null) return;
        var requested = StartWithWindowsCheckBox.IsChecked == true;
        if (_startupChanged(requested)) return;
        _updatingStartup = true;
        StartWithWindowsCheckBox.IsChecked = !requested;
        _updatingStartup = false;
        System.Windows.MessageBox.Show(this, "FerryClip could not update the Windows startup setting. Check the log folder for details.", "Startup setting", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void HotspotFirewall_Click(object sender, RoutedEventArgs e) => HotspotFirewall.Request(this);
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start("explorer.exe", FileLogger.Instance.LogDirectory); }
        catch (Exception ex) { FileLogger.Instance.Error("Failed to open log folder", ex); }
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();
}
