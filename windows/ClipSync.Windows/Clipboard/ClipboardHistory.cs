using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClipSync.Windows.Logging;

namespace ClipSync.Windows.Clipboard;
/// <summary>DPAPI encrypted history; optional fields migrate the original JSON records safely.</summary>
public sealed class ClipboardHistory
{
    public sealed record Entry(Guid Id, string Text, string Direction, DateTimeOffset At, bool Pinned = false, bool Sensitive = false);
    public static ClipboardHistory Shared { get; } = new();
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FerryClip", "history.dat");
    ClipSync.Windows.MainUI.Models.HistoryPolicy _policy = new();
    readonly Action<Exception>? _reportError;
    public ClipboardHistory(string? path = null, Action<Exception>? reportError = null) { if(path is not null) _path = path; _reportError = reportError; }
    public void Configure(ClipSync.Windows.MainUI.Models.HistoryPolicy policy) => _policy = policy;
    List<Entry> _entries = new();
    bool _loaded;
    public event Action? Changed;
    public string? Error { get; private set; }
    public static bool LooksSensitive(string text) => Regex.IsMatch(text.Trim(), @"^\d{4,8}$|\b(?:\d[ -]?){13,19}\b");
    public async Task<IReadOnlyList<Entry>> ListAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await LoadAsync(); if (Prune()) await SaveAsync(); return _entries.ToArray(); }
        catch (Exception ex) { Fail(ex); return Array.Empty<Entry>(); }
        finally { _gate.Release(); }
    }
    public Task RecordAsync(string text, string direction, bool flaggedSensitive = false) => MutateAsync(() =>
    {
        var sensitive = flaggedSensitive || LooksSensitive(text);
        if (!_policy.Save || (_policy.SkipSensitive && sensitive) || string.IsNullOrEmpty(text) || text.Length > 65536) return;
        if (_entries.FirstOrDefault() is { } latest && latest.Text == text && latest.Direction == direction) return;
        _entries.Insert(0, new Entry(Guid.NewGuid(), text, direction, DateTimeOffset.UtcNow, false, sensitive));
    });    public Task DeleteAsync(Guid id) => MutateAsync(() => _entries.RemoveAll(e => e.Id == id));
    public Task ClearAsync(bool includePinned = false) => MutateAsync(() => _entries.RemoveAll(e => includePinned || !e.Pinned));
    public Task PinAsync(Guid id, bool pinned) => MutateAsync(() => { var i = _entries.FindIndex(e => e.Id == id); if(i >= 0) _entries[i] = _entries[i] with { Pinned = pinned }; });
    async Task MutateAsync(Action change)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await LoadAsync(); change(); Prune(); await SaveAsync(); Error = null; }
        catch (Exception ex) { Fail(ex); }
        finally { _gate.Release(); }
        Changed?.Invoke();
    }
    async Task LoadAsync()
    {
        if (_loaded) return;
        if (File.Exists(_path))
        {
            var plain = ProtectedData.Unprotect(await File.ReadAllBytesAsync(_path).ConfigureAwait(false), null, DataProtectionScope.CurrentUser);
            try { _entries = JsonSerializer.Deserialize<List<Entry>>(plain) ?? new(); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        _loaded = true;
    }
    bool Prune()
    {
        var removed = _policy.RetentionHours == 0 ? 0 : _entries.RemoveAll(e => !e.Pinned && e.At < DateTimeOffset.UtcNow.AddHours(-_policy.RetentionHours));
        var excess = _entries.Where(e => !e.Pinned).Skip(_policy.Limit).Select(e => e.Id).ToHashSet();
        removed += _entries.RemoveAll(e => excess.Contains(e.Id));
        return removed > 0;
    }    async Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(_entries);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        await File.WriteAllBytesAsync(_path + ".tmp", encrypted).ConfigureAwait(false);
        File.Move(_path + ".tmp", _path, true);
    }
    void Fail(Exception ex) { Error = "History could not be opened or saved. Clipboard sync is unaffected."; if(_reportError is not null) _reportError(ex); else FileLogger.Instance.Warn("History storage failed: " + ex); }
}





