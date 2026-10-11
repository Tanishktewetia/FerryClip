#if DEBUG
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ClipSync.Windows.Clipboard;
using ClipSync.Windows.MainUI.Models;
namespace ClipSync.Windows.MainUI.Debug;
public static class HistoryPolicyTests
{
 static void Assert(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 static async Task WriteLegacy(string path,object entries){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var bytes=JsonSerializer.SerializeToUtf8Bytes(entries);try{await File.WriteAllBytesAsync(path,ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser));}finally{CryptographicOperations.ZeroMemory(bytes);}}
 public static async Task Run(string root)
 {
    var areas=new[]{new System.Windows.Rect(0,0,1920,1040),new System.Windows.Rect(-1280,0,1280,720)};
  var recovered=Services.PlacementRecovery.Recover(new(1100,720,5000,5000),areas);Assert(recovered.Left>=0&&recovered.Top>=0&&recovered.Left+recovered.Width<=1920,"Removed monitor recovery");
  var negative=Services.PlacementRecovery.Recover(new(900,600,-1200,20),areas);Assert(negative.Left<0&&negative.Left+negative.Width<=0,"Negative monitor recovery");
  var invalid=Services.PlacementRecovery.Recover(new(double.NaN,double.PositiveInfinity,double.NaN,0),areas);Assert(double.IsFinite(invalid.Width)&&double.IsFinite(invalid.Left!.Value),"Invalid placement recovery");
  foreach(var format in new[]{"ExcludeClipboardContentFromMonitorProcessing","Clipboard Viewer Ignore","application/x-keepassxc-secret"})Assert(SensitiveClipboardFormats.IsSensitive(s=>s==format),"Sensitive format: "+format);
  Assert(!SensitiveClipboardFormats.IsSensitive(_=>false),"Normal format");Assert(SensitiveClipboardFormats.IsSensitive(_=>throw new System.Runtime.InteropServices.COMException()),"Locked clipboard fail closed");
  foreach(var count in new[]{0,14,100,500}){var p=Path.Combine(root,$"legacy-{count}.dat");await WriteLegacy(p,Enumerable.Range(0,count).Select(i=>new{Id=Guid.NewGuid(),Text="Unicode 🌊 中文 "+i,Direction="Copied",At=DateTimeOffset.UtcNow}).ToArray());var h=new ClipboardHistory(p);h.Configure(new(true,500,0,false));var rows=await h.ListAsync();Assert(rows.Count==count,"Legacy count migration");Assert(rows.All(e=>!e.Pinned&&!e.Sensitive),"Legacy defaults");}
  foreach(var cap in new[]{50,100,500}){var h=new ClipboardHistory(Path.Combine(root,$"cap-{cap}.dat"));h.Configure(new(true,cap,0,false));await Task.WhenAll(Enumerable.Range(0,cap+14).Select(i=>h.RecordAsync("item "+i,"Copied")));Assert((await h.ListAsync()).Count==cap,"Concurrent cap");}
  var corrupt=Path.Combine(root,"corrupt.dat");var sentinel=new byte[]{1,2,3,4,5};await File.WriteAllBytesAsync(corrupt,sentinel);var bad=new ClipboardHistory(corrupt, _=>{});await bad.RecordAsync("must not overwrite","Copied");Assert((await File.ReadAllBytesAsync(corrupt)).SequenceEqual(sentinel)&&bad.Error is not null,"Unreadable preservation");
  var policy=new ClipboardHistory(Path.Combine(root,"policy.dat"));policy.Configure(new(false,100,24,true));await policy.RecordAsync("not recorded","Copied");Assert((await policy.ListAsync()).Count==0,"History disabled");policy.Configure(new(true,100,24,true));await policy.RecordAsync("sensitive","Copied",true);Assert((await policy.ListAsync()).Count==0,"Sensitive skip");policy.Configure(new(true,100,24,false));await policy.RecordAsync(new string('界',32000),"Received",true);var entry=(await policy.ListAsync()).Single();Assert(entry.Sensitive&&entry.Text.Length==32000,"Long Unicode and sensitive metadata");await policy.PinAsync(entry.Id,true);await policy.ClearAsync();Assert((await policy.ListAsync()).Single().Pinned,"Pinned safe clear");await policy.ClearAsync(true);Assert((await policy.ListAsync()).Count==0,"Explicit pinned clear");
  var retentionPath=Path.Combine(root,"retention.dat");await WriteLegacy(retentionPath,new[]{new ClipboardHistory.Entry(Guid.NewGuid(),"old","Copied",DateTimeOffset.UtcNow.AddDays(-8)),new ClipboardHistory.Entry(Guid.NewGuid(),"pinned","Copied",DateTimeOffset.UtcNow.AddDays(-8),true)});var retention=new ClipboardHistory(retentionPath);Assert((await retention.ListAsync()).Single().Pinned,"Retention preserves pinned");
 }
}
#endif


