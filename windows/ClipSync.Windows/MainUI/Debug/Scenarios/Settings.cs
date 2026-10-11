#if DEBUG
using ClipSync.Windows.MainUI.Pages.Settings;
using ClipSync.Windows.MainUI.ViewModels;

namespace ClipSync.Windows.MainUI.Debug.Scenarios;

public static class Settings
{
    public static void Register(ShellViewModel shell) =>
        shell.Navigation.Register("Settings", () => new SettingsPage(shell));
}
#endif
