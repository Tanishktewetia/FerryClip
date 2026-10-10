using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using ClipSync.Core;
using ClipSync.Windows.Logging;
using ClipSync.Windows.Security;

namespace ClipSync.Windows.Transport;

public sealed class SyncServer : IDisposable
{
    public const int Port = 48653;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly CertificateStore _store;
    private readonly List<SslStream> _clients = [];
    private readonly Dictionary<SslStream,string> _clientPeers = [];
    private readonly Dictionary<string,long> _peerEpochs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,(string? Address,DateTimeOffset? Since,DateTimeOffset? Last)> _peerInfo = new(StringComparer.OrdinalIgnoreCase);
    public sealed record PhoneState(string Id,string Name,bool Connected,bool Enabled,bool Paused,string? Address,DateTimeOffset? Since,DateTimeOffset? Last);
    public IReadOnlyList<PhoneState> Phones { get { lock(_gate) return _store.Peers.Select(p=> { var info=_peerInfo.GetValueOrDefault(p.Fingerprint); return new PhoneState(p.Fingerprint,p.Name,_clientPeers.Any(c=>c.Value==p.Fingerprint && _clients.Contains(c.Key)),p.Enabled,p.Paused,info.Address,info.Since,info.Last); }).ToList(); } }
    public void RenamePhone(string id,string name) { lock(_gate) _store.RenamePeer(id,name); ConnectionChanged?.Invoke(_clients.Count>0); }
    public void PausePhone(string id,bool paused) { lock(_gate) { _store.UpdatePeer(id,p=>p with { Paused=paused }); if(paused) JournalFor(id).Clear(); } ConnectionChanged?.Invoke(_clients.Count>0); }
    public void EnablePhone(string id,bool enabled) { lock(_gate) { _store.UpdatePeer(id,p=>p with { Enabled=enabled }); if(!enabled) RevokePhone(id); } ConnectionChanged?.Invoke(_clients.Count>0); }
    public void ForgetPhone(string id) { lock(_gate) { _store.ForgetPeer(id); RevokePhone(id); } ConnectionChanged?.Invoke(_clients.Count>0); }
    private void RevokePhone(string id) { JournalFor(id).Clear(); if(_peerInfo.TryGetValue(id,out var info)) _peerInfo[id]=(info.Address,null,info.Last); _peerEpochs[id]=_peerEpochs.GetValueOrDefault(id)+1; foreach(var client in _clientPeers.Where(c=>c.Value==id).Select(c=>c.Key).ToArray()) { client.Dispose(); _clients.Remove(client); _clientPeers.Remove(client); _modern.Remove(client); _pong.Remove(client); } Status=_clients.Count>0?"Connected":"Waiting"; }
    private bool PeerPaused(string peer) => _paused || _store.FindPeer(peer)?.Paused != false;
    private readonly object _gate = new();
    private readonly Channel<ClipMessage> _outgoing = Channel.CreateBounded<ClipMessage>(new BoundedChannelOptions(1) { SingleReader = false, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly SemaphoreSlim _pairGate = new(1, 1);
    private readonly bool _advertise;
    private readonly Action<string> _info;
    private readonly Action<string> _warn;
    private readonly TimeSpan _pairingDuration;
    private readonly TimeSpan _heartbeatInterval;
    private DateTime _pairUntil;
    private string? _pairCode;
    private string? _replacementPeer;
    private int _pairAttempts;
    private long _pairGeneration;
    private bool _pairRestartRequired;
    private long _trustGeneration;
    private readonly HashSet<SslStream> _modern = [];
    private readonly Dictionary<SslStream, long> _pong = [];
    private readonly ReconnectJournal _journal;
    private readonly Dictionary<string,ReconnectJournal> _peerJournals = new(StringComparer.OrdinalIgnoreCase);
    private ReconnectJournal JournalFor(string peer) { if(!_peerJournals.TryGetValue(peer,out var journal)) _peerJournals[peer]=journal=new ReconnectJournal(_store.Fingerprint); return journal; }
    private MdnsAdvertiser? _mdns;
    private LanAnnouncement? _lanAnnouncement;
    public string? RemoteAddress { get; private set; }
    public DateTimeOffset? ConnectedSince { get; private set; }
    public DateTimeOffset? LastConnectedAt { get; private set; }
    public bool ReplayOnConnect { get; set; } = true;
    private bool _connectionsEnabled = true;
    public bool ConnectionsEnabled { get { lock (_gate) return _connectionsEnabled; } }
    public void SetConnectionsEnabled(bool enabled) { lock (_gate) { _connectionsEnabled = enabled; _trustGeneration++; if (!enabled) { foreach (var client in _clients) client.Dispose(); _clients.Clear(); _clientPeers.Clear(); _modern.Clear(); _pong.Clear(); _journal.Clear(); _peerJournals.Clear(); while (_outgoing.Reader.TryRead(out _)) { } Status = "Waiting"; } } ConnectionChanged?.Invoke(Status == "Connected"); }
    private bool _paused;
    public bool Paused { get { lock (_gate) return _paused; } set { lock (_gate) { _paused = value; if(value) _journal.Clear(); } } }

    public event Action<string>? IncomingText;
    public Func<string, bool>? ClipboardSink { get; set; }
    public event Action<bool>? ConnectionChanged;
    public event Action<string>? PairingCodeAvailable;
    public event Action<string>? Notice;
    public bool PairingOpen { get { lock (_gate) return _pairCode is not null && DateTime.UtcNow < _pairUntil; } }
    public string Status { get; private set; } = "Waiting";
    public string Fingerprint => _store.Fingerprint;
    public int ListeningPort => ((IPEndPoint)_listener.LocalEndpoint).Port;

    // Optional ephemeral port/isolated identity let integration tests avoid the user's app.
    public SyncServer(CertificateStore store, int port = Port, bool advertise = true, TimeSpan? pairingDuration = null, Action<string>? log = null, TimeSpan? heartbeatInterval = null)
    {
        _store = store;
        _journal = new ReconnectJournal(store.Fingerprint);
        _info = log ?? (message => FileLogger.Instance.Info(message));
        _warn = log ?? (message => FileLogger.Instance.Warn(message));
        _listener = new TcpListener(IPAddress.IPv6Any, port);
        _listener.Server.DualMode = true;
        _advertise = advertise;
        _pairingDuration = pairingDuration ?? TimeSpan.FromMinutes(2);
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(15);
        if (_heartbeatInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
    }
    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoop);
        _ = Task.Run(BroadcastLoop);
        if (_advertise) { Advertise(); _lanAnnouncement = new LanAnnouncement(ListeningPort); }
        _info($"mTLS sync server listening on Wi-Fi/LAN port {ListeningPort}");
    }
    public void BeginPairing(string? replacementPeer = null)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        long generation;
        lock (_gate) { if (replacementPeer is not null && _store.FindPeer(replacementPeer) is null) throw new InvalidOperationException("Phone is no longer paired"); _replacementPeer = replacementPeer; _pairUntil = DateTime.UtcNow.Add(_pairingDuration); _pairCode = code; _pairAttempts = 0; generation = ++_pairGeneration; _pairRestartRequired = false; }
        _info("Explicit one-time pairing code opened");
        PairingCodeAvailable?.Invoke(code);
        _ = Task.Run(async () => { try { await Task.Delay(_pairingDuration, _stop.Token); lock (_gate) { if (generation != _pairGeneration || _pairUntil == DateTime.MinValue) return; _pairUntil = DateTime.MinValue; _pairCode = null; } Notice?.Invoke("Pairing timed out. Generate code to connect for a new code."); } catch (OperationCanceledException) { } });
    }
    public bool HasPairedPhone { get { lock (_gate) return _store.PinnedPeer != null; } }
    /// <summary>Only called after local user confirmation. Never triggered by a network request.</summary>
    public void ForgetPairedPhone()
    {
        lock (_gate) {
            // Persist revocation first. If storage fails, retain the live session and report the error.
            _store.ForgetPeer();
            _trustGeneration++; _pairGeneration++; _pairUntil = DateTime.MinValue; _pairCode = null; _pairRestartRequired = false;
            foreach (var client in _clients) client.Dispose();
            _clients.Clear(); _modern.Clear(); _pong.Clear(); _journal.Clear();
            while (_outgoing.Reader.TryRead(out _)) { }
            Status = "Waiting";
        }
        _info("Paired phone forgotten by local user; all previous sessions revoked");
        ConnectionChanged?.Invoke(false);
        Notice?.Invoke("Phone forgotten. Generate code to connect, then search again on your phone.");
    }
    public string PairCodeFor(string peerFingerprint) => CertificateStore.PairCode(_store.Fingerprint, peerFingerprint);
    private static bool IsLocalAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true; // Local integration and self-test clients.
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10 ||
                (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                (b[0] == 192 && b[1] == 168) ||
                (b[0] == 100 && b[1] is >= 64 and <= 127) ||
                (b[0] == 169 && b[1] == 254);
        }
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return address.IsIPv6LinkLocal || (b[0] & 0xFE) == 0xFC;
        }
        return false;
    }
    private bool ValidatePeer(X509Certificate? certificate)
    {
        if (certificate is null) return false;
        var fp = Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));
        lock (_gate) {
            return (_connectionsEnabled && _store.FindPeer(fp)?.Enabled == true) || DateTime.UtcNow < _pairUntil;
        }
    }
    private async Task AcceptLoop()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
                _ = Handle(await _listener.AcceptTcpClientAsync(_stop.Token));
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { _warn("Listener stopped: " + ex.GetType().Name); }
    }
    private async Task Handle(TcpClient tcp)
    {
        using (tcp)
        using (var ssl = new SslStream(tcp.GetStream(), false, (_, certificate, _, _) => ValidatePeer(certificate)))
        {
            long trustGeneration; lock (_gate) trustGeneration = _trustGeneration;
            long peerEpoch=0; string? peerId=null;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            try
            {
                var remoteAddress = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address;
                if (remoteAddress is null || !IsLocalAddress(remoteAddress))
                {
                    _warn("Remote peer rejected: outside local private/VPN address space");
                    return;
                }
                tcp.NoDelay = true;
                tcp.SendTimeout = 3000;
                ssl.WriteTimeout = 3000;
                using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                {
                    handshake.CancelAfter(TimeSpan.FromSeconds(10));
                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _store.Certificate,
                        ApplicationProtocols = [new SslApplicationProtocol("clipsync-pair/1"), new SslApplicationProtocol("clipsync-probe/1")],
                        ClientCertificateRequired = true,
                        EnabledSslProtocols = SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                    }, handshake.Token);
                }
                lock (_gate) { if (trustGeneration != _trustGeneration) return; }
                var peer = Convert.ToHexString(SHA256.HashData(ssl.RemoteCertificate!.GetRawCertData()));
                peerId=peer; lock(_gate) peerEpoch=_peerEpochs.GetValueOrDefault(peer);
                var control = ssl.NegotiatedApplicationProtocol.ToString();
                if (control is "clipsync-pair/1" or "clipsync-probe/1") {
                    await HandleControl(ssl, peer, control == "clipsync-probe/1", trustGeneration);
                    return;
                }
                bool needsPairing;
                lock (_gate) needsPairing = _store.FindPeer(peer) == null;
                if (needsPairing)
                {
                    // Legacy sync sockets must never establish trust: only the mobile code-entry ALPN flow
                    // can prove the user saw the short-lived random code displayed on this PC.
                    _warn("Unpaired legacy sync handshake rejected; explicit mobile code entry is required");
                    await WriteLineAsync(ssl, "PAIR_REJECTED", _stop.Token);
                    return;
                }
                lock (_gate)
                {
                    // Forget revokes handshakes already in flight as well as live clients.
                    if (trustGeneration != _trustGeneration || !_connectionsEnabled) return;
                    if (_store.FindPeer(peer)?.Enabled != true || peerEpoch!=_peerEpochs.GetValueOrDefault(peer)) return;
                    foreach(var old in _clientPeers.Where(c=>c.Value==peer).Select(c=>c.Key).ToArray()) { old.Dispose(); _clients.Remove(old); _clientPeers.Remove(old); _modern.Remove(old); _pong.Remove(old); }
                    var ready = Encoding.ASCII.GetBytes("READY\n");
                    ssl.Write(ready);
                    _clients.Add(ssl); _clientPeers[ssl]=peer;
                    RemoteAddress = tcp.Client.RemoteEndPoint?.ToString(); ConnectedSince = DateTimeOffset.Now; LastConnectedAt = ConnectedSince;
                    _peerInfo[peer]=(RemoteAddress,ConnectedSince,LastConnectedAt);
                    Status = "Connected";
                }
                ConnectionChanged?.Invoke(true);
                _ = Heartbeat(ssl, lifetime.Token);
                ReconnectJournal journal; lock(_gate) journal=JournalFor(peer);
                var reader = new FrameReader();
                var buffer = new byte[8192];
                while (!_stop.IsCancellationRequested)
                {
                    var count = await ssl.ReadAsync(buffer, _stop.Token);
                    if (count == 0) break;
                    foreach (var message in reader.Push(buffer[..count]))
                    {
                        string? incoming = null;
                        ClipMessage? accepted = null;
                        lock (_gate)
                        {
                            if (trustGeneration != _trustGeneration || !_clients.Contains(ssl) || peerEpoch!=_peerEpochs.GetValueOrDefault(peer) || _store.FindPeer(peer)?.Enabled!=true) return;
                            if (message.Type == MessageType.Hello && SessionProtocol.Origin(message) is { } origin && _modern.Add(ssl))
                            {
                                journal.Observe(message.Lamport); _journal.Observe(message.Lamport);

                                _pong[ssl] = Environment.TickCount64;
                                ssl.Write(FrameCodec.Encode(SessionProtocol.Hello(journal.Latest, ReplayOnConnect && !PeerPaused(peer))));
                                if (!PeerPaused(peer) && SessionProtocol.Replay(message) && journal.Latest is { } newest && SessionProtocol.Newer(newest, message.Lamport, origin)) ssl.Write(FrameCodec.Encode(newest));
                            }
                            else if (message.Type == MessageType.Ping && _modern.Contains(ssl)) ssl.Write(FrameCodec.Encode(new ClipMessage(MessageType.Pong)));
                            else if (message.Type == MessageType.Pong) _pong[ssl] = Environment.TickCount64;
                            else if (!PeerPaused(peer) && message.Type == MessageType.State && _modern.Contains(ssl)) { _journal.Observe(message.Lamport); if(journal.Accept(message)) { accepted = message; incoming = message.Text ?? ""; } }
                            else if (!PeerPaused(peer) && message.Type == MessageType.Text) { incoming = message.Text ?? ""; accepted = journal.Local(incoming); _journal.Observe(accepted.Lamport); }
                        }
                        if (incoming is not null) {
                            if (ClipboardSink?.Invoke(incoming) == false) {
                                lock (_gate) { if (journal.Latest == accepted) journal.Clear(); }
                                throw new IOException("Clipboard apply failed; reconnect for latest replay");
                            }
                            IncomingText?.Invoke(incoming);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or AuthenticationException or SocketException or OperationCanceledException or ObjectDisposedException or ArgumentException)
            {
                _warn("Client connection ended: " + ex.GetType().Name);
            }
            finally
            {
                lifetime.Cancel();
                bool removed; bool connected;
                lock (_gate)
                {
                    _modern.Remove(ssl); _pong.Remove(ssl); _clientPeers.Remove(ssl);
                    if(peerId != null && !_clientPeers.Values.Contains(peerId) && _peerInfo.TryGetValue(peerId,out var info)) _peerInfo[peerId]=(info.Address,null,info.Last);
                    removed = _clients.Remove(ssl); connected = _clients.Count != 0;
                    Status = connected ? "Connected" : "Waiting";
                }
                if (removed) { if(!connected) ConnectedSince = null; ConnectionChanged?.Invoke(connected); }
            }
        }
    }
    // Control sockets never enter the clipboard client list, even for an already paired phone.
    private async Task HandleControl(SslStream ssl, string peer, bool probe, long trustGeneration)
    {
        long epoch; lock(_gate) epoch=_peerEpochs.GetValueOrDefault(peer);
        var host = Convert.ToBase64String(Encoding.UTF8.GetBytes(Environment.MachineName));
        if (probe) { await WriteLineAsync(ssl, $"INFO|{host}|{(PairingOpen ? 1 : 0)}", _stop.Token); return; }
        if (!await _pairGate.WaitAsync(0, _stop.Token)) { Notice?.Invoke("Pairing rejected: another request is already being confirmed."); await WriteLineAsync(ssl, "PAIR_BUSY", _stop.Token); return; }
        try {
            DateTime deadline; long generation; string? code;
            lock (_gate) { deadline = _pairUntil; generation = _pairGeneration; code = _pairCode; }
            if (code is null || _pairRestartRequired || DateTime.UtcNow >= deadline) { await WriteLineAsync(ssl, "PAIR_CLOSED", _stop.Token); return; }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(deadline - DateTime.UtcNow);
            await WriteLineAsync(ssl, $"PAIR|{_store.Fingerprint}", timeout.Token);
            await WriteLineAsync(ssl, $"META|{host}|{Math.Max(1, (long)(deadline - DateTime.UtcNow).TotalMilliseconds)}", timeout.Token);
            for (var attempt = 1; attempt <= 3; attempt++) {
                var line = await ReadLineAsync(ssl, timeout.Token);
                if (line == "CANCEL") return;
                if (line != "CONFIRM|" + code) {
                    int remaining;
                    lock (_gate) {
                        if (generation != _pairGeneration || _pairCode is null) return;
                        remaining = 3 - ++_pairAttempts;
                        if (remaining <= 0) { _pairUntil = DateTime.MinValue; _pairCode = null; _pairRestartRequired = true; }
                    }
                    if (remaining <= 0) {
                        await WriteLineAsync(ssl, "PAIR_REJECTED", timeout.Token);
                        Notice?.Invoke("Three incorrect codes. Generate code to connect to generate another code."); return;
                    }
                    await WriteLineAsync(ssl, $"WRONG|{remaining}", timeout.Token); continue;
                }
                lock (_gate) {
                    if (epoch!=_peerEpochs.GetValueOrDefault(peer) || trustGeneration != _trustGeneration || DateTime.UtcNow >= deadline || generation != _pairGeneration || !string.Equals(_pairCode, code, StringComparison.Ordinal)) return;

                    var replacement = _replacementPeer;
                    _store.PinReplacing(peer, replacement);
                    if (replacement is not null && !replacement.Equals(peer, StringComparison.OrdinalIgnoreCase)) RevokePhone(replacement);
                    _replacementPeer = null; _pairUntil = DateTime.MinValue; _pairCode = null; _pairRestartRequired = false;
                }
                await WriteLineAsync(ssl, $"PAIRED|{host}", timeout.Token);
                try {
                    using var nameDeadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); nameDeadline.CancelAfter(700);
                    var nameLine = await ReadLineAsync(ssl, nameDeadline.Token);
                    if (nameLine.StartsWith("NAME|")) { var name = Encoding.UTF8.GetString(Convert.FromBase64String(nameLine[5..])); lock(_gate) { if(epoch==_peerEpochs.GetValueOrDefault(peer) && trustGeneration==_trustGeneration && _store.FindPeer(peer)?.Name=="Paired phone") _store.RenamePeer(peer,name); } }
                } catch(Exception e) when(e is IOException or OperationCanceledException or FormatException or ArgumentException) { }
                PairingCodeAvailable?.Invoke(""); ConnectionChanged?.Invoke(Status == "Connected"); return;
            }
        } catch (OperationCanceledException) when (!_stop.IsCancellationRequested) {
            Notice?.Invoke("Pairing timed out. Generate code to connect for a new code.");
            using var final = new CancellationTokenSource(500);
            try { await WriteLineAsync(ssl, "PAIR_TIMEOUT", final.Token); } catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        } finally { _pairGate.Release(); }
    }
    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellation)
    {
        var bytes = new List<byte>(); var one = new byte[1];
        while (await stream.ReadAsync(one, cancellation) != 0)
        {
            if (one[0] == (byte)'\n') return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            if (bytes.Count >= 256 || (one[0] < 32 && one[0] != 13) || one[0] > 126) throw new InvalidDataException("Invalid pairing response");
            bytes.Add(one[0]);
        }
        throw new EndOfStreamException();
    }
    private static Task WriteLineAsync(Stream stream, string value, CancellationToken cancellation) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(value + "\n"), cancellation).AsTask();
    public void Broadcast(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > SessionProtocol.MaxText)
        {
            _warn("Clipboard skipped: exceeds current CSP1 text limit"); return;
        }
        lock (_gate) { if (!_paused) { var message=_journal.Local(text); foreach(var peer in _store.Peers.Where(p=>p.Enabled && !p.Paused)) JournalFor(peer.Fingerprint).Accept(message); _outgoing.Writer.TryWrite(message); } }
    }
    private async Task BroadcastLoop()
    {
        try {
            await foreach (var text in _outgoing.Reader.ReadAllAsync(_stop.Token)) BroadcastNow(text);
        } catch (OperationCanceledException) { }
    }
    private void BroadcastNow(ClipMessage text)
    {

        bool removed = false; bool connected;
        lock (_gate)
        {
            if (_paused || _journal.Latest != text) return;
            foreach (var client in _clients.ToArray())
            {
                if(!_clientPeers.TryGetValue(client,out var peer) || PeerPaused(peer) || _store.FindPeer(peer)?.Enabled!=true) continue;
                JournalFor(peer).Accept(text);
                try { client.Write(FrameCodec.Encode(_modern.Contains(client) ? text : new ClipMessage(MessageType.Text, text.Text))); }
                catch { client.Dispose(); _clients.Remove(client); removed = true; }
            }
            connected = _clients.Count != 0;
            Status = connected ? "Connected" : "Waiting";
        }
        if (removed) ConnectionChanged?.Invoke(connected);
    }
    private async Task Heartbeat(SslStream ssl, CancellationToken stop)
    {
        try {
            while (!stop.IsCancellationRequested) {
                await Task.Delay(_heartbeatInterval, stop);
                lock (_gate) {
                    if (!_clients.Contains(ssl)) return;
                    if (!_modern.Contains(ssl)) continue;
                    if (Environment.TickCount64 - _pong.GetValueOrDefault(ssl) >= _heartbeatInterval.TotalMilliseconds * 2) { ssl.Dispose(); return; }
                    ssl.Write(FrameCodec.Encode(new ClipMessage(MessageType.Ping)));
                }
            }
        } catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { ssl.Dispose(); }
    }
    private void Advertise()
    {
        try { _mdns = new MdnsAdvertiser(ListeningPort, _info); }
        catch (Exception ex) { _warn("DNS-SD unavailable: " + ex.GetType().Name); }
    }
    public void Dispose()
    {
        _mdns?.Dispose(); _lanAnnouncement?.Dispose();
        _outgoing.Writer.TryComplete(); _stop.Cancel(); _listener.Stop();
        lock (_gate) { foreach (var client in _clients) client.Dispose(); _clients.Clear(); }
    }
}
