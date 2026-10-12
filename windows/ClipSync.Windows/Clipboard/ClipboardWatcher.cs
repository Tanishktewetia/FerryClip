using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ClipSync.Windows.Logging;

namespace ClipSync.Windows.Clipboard;

/// <summary>Tracks clipboard mutations independently of clipboard text content.</summary>
internal sealed class ClipboardSequenceTracker(uint initialSequence)
{
    private uint lastObserved = initialSequence;
    private uint? selfWrite;

    public bool ShouldProcess(uint sequence)
    {
        if (selfWrite == sequence) { lastObserved = sequence; selfWrite = null; return false; }
        return sequence != lastObserved;
    }
    public void MarkObserved(uint sequence) => lastObserved = sequence;
    public void MarkSelfWrite(uint sequence) { lastObserved = sequence; selfWrite = sequence; }
}

/// <summary>STA clipboard access. Detects clipboard mutations by sequence, including repeated identical text.</summary>
public sealed class ClipboardWatcher : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private readonly HwndSource _source;
    private readonly DispatcherTimer _debounce;
    private bool _writing;
    private readonly ClipboardSequenceTracker _sequence;
    public event Action<string>? TextChanged;
    public ClipboardWatcher()
    {
        _source = new HwndSource(new HwndSourceParameters("ClipSyncClipboardWatcher") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(WndProc);
        if (!AddClipboardFormatListener(_source.Handle)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        _sequence = new ClipboardSequenceTracker(GetClipboardSequenceNumber());
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); ReadAndRaise(); };
    }
    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == WmClipboardUpdate && !_writing) {
            var sequence = GetClipboardSequenceNumber();
            if (_sequence.ShouldProcess(sequence)) { _debounce.Stop(); _debounce.Start(); }
            handled = true;
        }
        return IntPtr.Zero;
    }
    private void ReadAndRaise()
    {
        for (var attempt = 0; attempt < 5; attempt++) {
            try {
                var sequence = GetClipboardSequenceNumber();
                if (!_sequence.ShouldProcess(sequence)) return;
                if (!System.Windows.Clipboard.ContainsText()) { _sequence.MarkObserved(sequence); return; }
                var text = System.Windows.Clipboard.GetText();
                _sequence.MarkObserved(GetClipboardSequenceNumber());
                TextChanged?.Invoke(text);
                return;
            } catch (COMException) { Thread.Sleep(10 * (attempt + 1)); }
        }
        FileLogger.Instance.Warn("Clipboard read remained locked after retries");
    }
    public bool WriteText(string text)
    {
        _writing = true;
        try {
            for (var attempt = 0; attempt < 5; attempt++) {
                try {
                    if (text.Length == 0) System.Windows.Clipboard.Clear();
                    else System.Windows.Clipboard.SetText(text);
                    // Track this exact clipboard mutation to suppress its delayed notification,
                    // without suppressing the user's later copy of identical text.
                    _sequence.MarkSelfWrite(GetClipboardSequenceNumber());
                    return true;
                } catch (COMException) { Thread.Sleep(10 * (attempt + 1)); }
            }
            FileLogger.Instance.Warn("Clipboard write remained locked after retries");
            return false;
        } finally { _writing = false; }
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    public void Dispose() { _debounce.Stop(); RemoveClipboardFormatListener(_source.Handle); _source.RemoveHook(WndProc); _source.Dispose(); }
}
