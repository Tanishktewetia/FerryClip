using System.Windows;
using System.Windows.Input;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Pages.Devices;
public partial class DevicesPage : System.Windows.Controls.UserControl, IDisposable
{
    readonly DevicesViewModel model;
    IInputElement? previousFocus;
    public DevicesPage(ShellViewModel shell)
    {
        InitializeComponent(); DataContext = model = new(shell);
        model.PropertyChanged += ModelChanged;
        PreviewKeyDown += KeyPressed;
    }
    void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DevicesViewModel.PairingOpen)) return;
        if (model.PairingOpen) { previousFocus = Keyboard.FocusedElement; Dispatcher.BeginInvoke(new Action(() => ClosePairing.Focus())); }
        else if (previousFocus is not null) Keyboard.Focus(previousFocus);
    }
    void KeyPressed(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (model.PairingOpen && e.Key == Key.Escape) { model.ClosePairingCommand.Execute(null); e.Handled = true; }
        else if (model.PairingOpen && e.Key == Key.Tab && !PairingDialog.IsKeyboardFocusWithin) { ClosePairing.Focus(); e.Handled = true; }
    }
    void RenameLoaded(object sender, RoutedEventArgs e)
    {
        var box = (System.Windows.Controls.TextBox)sender;
        box.IsVisibleChanged += RenameVisible;
    }
    void RenameLostFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox box && box.DataContext is DeviceCardViewModel card && card.IsEditing)
            card.SaveRenameCommand.Execute(null);
    }
    void RenameVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) { var box = (System.Windows.Controls.TextBox)sender; box.Dispatcher.BeginInvoke(new Action(() => { box.Focus(); box.SelectAll(); })); }
    }
    void OpenOverflow(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
    public void Dispose() { model.PropertyChanged -= ModelChanged; PreviewKeyDown -= KeyPressed; model.Dispose(); }
}
