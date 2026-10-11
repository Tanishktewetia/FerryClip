#if DEBUG
using ClipSync.Windows.MainUI.Pages.Dashboard;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Debug.Scenarios;
public static class Dashboard
{
    public static void Register(ShellViewModel shell) => shell.Navigation.Register("Dashboard", () => new DashboardPage(shell));
}
#endif
