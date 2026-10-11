#if DEBUG
using ClipSync.Windows.MainUI.Pages.Devices;
using ClipSync.Windows.MainUI.ViewModels;

namespace ClipSync.Windows.MainUI.Debug.Scenarios;

public static class Devices
{
    public static void Register(ShellViewModel shell)
    {
        shell.Navigation.Register("Devices", () =>
        {
            var page = new DevicesPage(shell);
            if (shell.App.Devices.Count > 0 && (shell.App.Pairing.Active || shell.App.Pairing.Deadline is not null))
            {
                ((DevicesViewModel)page.DataContext).PairingOpen = true;
            }

            return page;
        });
    }
}
#endif
