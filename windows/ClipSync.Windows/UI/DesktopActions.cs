using System.Diagnostics;
using ClipSync.Windows.Logging;
namespace ClipSync.Windows.UI;
internal static class DesktopActions
{
    public static Action<string?>? OpenRequested {get;set;}
    public static void OpenFerryClip() => OpenRequested?.Invoke(null);
    public static void OpenDevices() => OpenRequested?.Invoke("Devices");
#if DEBUG
    public static void OpenLogFolder()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", FileLogger.Instance.LogDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { FileLogger.Instance.Error("Failed to open log folder", ex); }
    }
#endif
}
