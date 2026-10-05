using System.Diagnostics;
using System.Text;
using System.Windows;
namespace ClipSync.Windows.UI;
internal static class HotspotFirewall {
 public static void Request(Window owner) {
  if(System.Windows.MessageBox.Show(owner,"Phone hotspots may use Windows' Public network profile. Allow this FerryClip executable to receive TCP connections on port 48653 from the local subnet on all profiles? Windows will request administrator approval. Existing firewall block rules are not removed.","Allow local hotspot connections",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes) return;
  var exe=Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable");
  var escaped=exe.Replace("'","''");
  var command=$"$ErrorActionPreference='Stop'; New-NetFirewallRule -DisplayName 'FerryClip local hotspot TCP' -Direction Inbound -Action Allow -Program '{escaped}' -Protocol TCP -LocalPort 48653 -RemoteAddress LocalSubnet -Profile Any | Out-Null; New-NetFirewallRule -DisplayName 'FerryClip local discovery UDP' -Direction Inbound -Action Allow -Program '{escaped}' -Protocol UDP -LocalPort 5353 -RemoteAddress LocalSubnet -Profile Any | Out-Null";
  try { Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(command))) { UseShellExecute=true, Verb="runas", WindowStyle=ProcessWindowStyle.Hidden }); }
  catch(System.ComponentModel.Win32Exception) { System.Windows.MessageBox.Show(owner,"Firewall approval was cancelled or unavailable. No firewall setting was changed by FerryClip.","Hotspot access"); }
 }
}
