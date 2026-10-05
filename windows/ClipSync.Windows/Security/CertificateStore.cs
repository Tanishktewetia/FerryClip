using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
namespace ClipSync.Windows.Security;
public sealed class CertificateStore : IDisposable
{
    private readonly string _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipSync");
    public X509Certificate2 Certificate { get; }
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Certificate.RawData));
    public string PinnedPeerPath => Path.Combine(_dir, "pinned-peer.txt");
    public sealed record Peer(string Fingerprint, string Name, bool Enabled = true, bool Paused = false);
    private readonly object _peerGate = new();
    private string PeersPath => Path.Combine(_dir,"paired-phones.json");
    public IReadOnlyList<Peer> Peers { get { lock(_peerGate) {
        if(File.Exists(PeersPath)) { var peers=System.Text.Json.JsonSerializer.Deserialize<List<Peer>>(File.ReadAllText(PeersPath)) ?? throw new InvalidDataException("Invalid paired phone store"); if(peers.Any(p=>p==null || p.Fingerprint==null || p.Fingerprint.Length!=64 || !p.Fingerprint.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(p.Name)) || peers.Select(p=>p.Fingerprint.ToUpperInvariant()).Distinct().Count()!=peers.Count) throw new InvalidDataException("Invalid paired phone identities"); return peers; }
        if(!File.Exists(PinnedPeerPath)) return Array.Empty<Peer>();
        var fp=File.ReadAllText(PinnedPeerPath).Trim();
        return new[]{new Peer(fp,File.Exists(Path.Combine(_dir,"phone-name.txt")) ? File.ReadAllText(Path.Combine(_dir,"phone-name.txt")) : "Paired phone")};
    } } }
    public string? PinnedPeer => Peers.FirstOrDefault()?.Fingerprint;
    public Peer? FindPeer(string fp) => Peers.FirstOrDefault(p=>p.Fingerprint.Equals(fp,StringComparison.OrdinalIgnoreCase));
    private void SavePeers(IEnumerable<Peer> peers) {
        var temp=PeersPath+".tmp"; File.WriteAllText(temp,System.Text.Json.JsonSerializer.Serialize(peers)); File.Move(temp,PeersPath,true);
    }
    public void UpdatePeer(string fp, Func<Peer,Peer> change) { lock(_peerGate) {
        var peers=Peers.ToList(); var i=peers.FindIndex(p=>p.Fingerprint.Equals(fp,StringComparison.OrdinalIgnoreCase));
        if(i<0) throw new InvalidOperationException("Phone is not paired"); peers[i]=change(peers[i]); SavePeers(peers);
    } }
    public void RenamePeer(string fp,string name) { name=name.Trim(); if(name.Length is <1 or >48 || name.Any(char.IsControl)) throw new ArgumentException("Use 1–48 printable characters."); UpdatePeer(fp,p=>p with { Name=name }); }
    public void ForgetPeer(string fp) { lock(_peerGate) SavePeers(Peers.Where(p=>!p.Fingerprint.Equals(fp,StringComparison.OrdinalIgnoreCase))); }

    public CertificateStore(string profile = "identity", string? directory = null){ if(directory is not null) _dir = directory; Directory.CreateDirectory(_dir); var p=Path.Combine(_dir,profile+".pfx"); const string pw="clipsync-dpapi"; if(File.Exists(p)){ var raw=ProtectedData.Unprotect(File.ReadAllBytes(p),null,DataProtectionScope.CurrentUser); Certificate=X509CertificateLoader.LoadPkcs12(raw,pw,X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.PersistKeySet); } else { using var rsa=RSA.Create(2048); var req=new CertificateRequest("CN=ClipSync",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1); req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,false)); req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,false)); using var c=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddYears(5)); File.WriteAllBytes(p,ProtectedData.Protect(c.Export(X509ContentType.Pfx,pw),null,DataProtectionScope.CurrentUser)); Certificate=X509CertificateLoader.LoadPkcs12(ProtectedData.Unprotect(File.ReadAllBytes(p),null,DataProtectionScope.CurrentUser),pw,X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.PersistKeySet); }}
    public string PhoneName { get => Peers.FirstOrDefault()?.Name ?? "Paired phone"; set { if(PinnedPeer is {} fp) RenamePeer(fp,value); else throw new InvalidOperationException("No paired phone"); } }
    public void ForgetPeer() { lock(_peerGate) { SavePeers(Array.Empty<Peer>()); File.Delete(PinnedPeerPath); File.Delete(Path.Combine(_dir,"phone-name.txt")); } }
    public void Pin(string fp) { if(fp.Length!=64 || !fp.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid certificate fingerprint"); lock(_peerGate) { var peers=Peers.ToList(); if(!peers.Any(p=>p.Fingerprint.Equals(fp,StringComparison.OrdinalIgnoreCase))) peers.Add(new Peer(fp.ToUpperInvariant(),"Paired phone")); SavePeers(peers); } }
    public static string PairCode(string a,string b)=> (BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(string.CompareOrdinal(a,b)<0?a+b:b+a)),0)%1000000).ToString("D6");
    public void Dispose()=>Certificate.Dispose();
}


