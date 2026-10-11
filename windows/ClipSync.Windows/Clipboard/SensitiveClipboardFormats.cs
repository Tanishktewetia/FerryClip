namespace ClipSync.Windows.Clipboard;
/// <summary>Windows password-manager exclusion formats; used only to classify local history capture.</summary>
public static class SensitiveClipboardFormats
{
 public static bool IsSensitive()=>IsSensitive(System.Windows.Clipboard.ContainsData);
 public static bool IsSensitive(Func<string,bool> contains)
 {
  try{return contains("ExcludeClipboardContentFromMonitorProcessing") || contains("Clipboard Viewer Ignore") || contains("application/x-keepassxc-secret");}
  catch(System.Runtime.InteropServices.COMException){return true;} // Fail closed for history only.
 }
}

