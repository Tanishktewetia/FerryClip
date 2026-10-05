using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
namespace ClipSync.Windows.Transport;
// Discovery hints only; never establishes trust or carries clipboard text.
internal sealed class LanAnnouncement : IDisposable {
 readonly CancellationTokenSource _stop = new(); readonly int _port;
 public LanAnnouncement(int port) { _port=port; _=Task.Run(Run); }
 async Task Run() {

  while(!_stop.IsCancellationRequested) {
   foreach(var link in Links()) {
    try { using var udp = new UdpClient(new IPEndPoint(link.Address, 0)); udp.EnableBroadcast=true; var bytes=Encoding.ASCII.GetBytes($"FERRYCLIP-LAN/1|{link.Address}|{_port}"); var ip=link.Address.GetAddressBytes(); var mask=link.IPv4Mask.GetAddressBytes(); var target=new IPAddress(ip.Select((b,i)=>(byte)(b | ~mask[i])).ToArray()); await udp.SendAsync(bytes,new IPEndPoint(target,48653),_stop.Token); }
    catch(Exception e) when(e is SocketException or OperationCanceledException or ObjectDisposedException) { }
   }
   try { await Task.Delay(1500,_stop.Token); } catch(OperationCanceledException) { break; }
  }
 }
 internal static IEnumerable<UnicastIPAddressInformation> Links() => NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork && IsPrivate(a.Address));
 internal static IEnumerable<IPAddress> Addresses() => NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).Where(a=>a.AddressFamily==AddressFamily.InterNetwork && IsPrivate(a));
 internal static bool IsPrivate(IPAddress a) { var b=a.GetAddressBytes(); return b.Length==4 && (b[0]==10 || b[0]==172 && b[1]>=16 && b[1]<=31 || b[0]==192 && b[1]==168); }
 public void Dispose(){_stop.Cancel();}
}
