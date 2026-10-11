using ClipSync.Windows.Clipboard;
namespace ClipSync.Windows.UI;
public partial class StatusWindow
{
    public void RecordHistory(string text, string direction, bool sensitive = false) => _ = Task.Run(() => ClipboardHistory.Shared.RecordAsync(text, direction, sensitive));
}
