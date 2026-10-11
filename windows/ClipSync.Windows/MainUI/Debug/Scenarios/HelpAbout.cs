#if DEBUG
using ClipSync.Windows.MainUI.Pages.HelpAbout;
using ClipSync.Windows.MainUI.ViewModels;
namespace ClipSync.Windows.MainUI.Debug.Scenarios;
public static class HelpAbout
{
 public static void Register(ShellViewModel shell)
 {
  shell.Navigation.Register("Help & Docs",()=>new HelpPage(shell));
  shell.Navigation.Register("About",()=>new AboutPage(shell));
 }
}
#endif