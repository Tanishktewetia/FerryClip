using System.IO;
using System.Text.Json;
using ClipSync.Windows.MainUI.Models;
namespace ClipSync.Windows.MainUI.Services;
public sealed class SettingsStore
{
 readonly string directory;
 public SettingsStore(string? directory=null)=>this.directory=directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FerryClip");
 public T Load<T>(string name,T fallback){var p=Path.Combine(directory,name);if(!File.Exists(p))return fallback;return JsonSerializer.Deserialize<T>(File.ReadAllText(p))??throw new InvalidDataException("Invalid "+name);}
 public void Save<T>(string name,T value){Directory.CreateDirectory(directory);var p=Path.Combine(directory,name);File.WriteAllText(p+".tmp",JsonSerializer.Serialize(value));File.Move(p+".tmp",p,true);}
 public UserSettings LoadSettings()
 {
  try{var s=Load("settings.json",new UserSettings());Validate(s);return s;}
  catch(Exception ex) when(ex is JsonException or IOException or ArgumentException or UnauthorizedAccessException)
  {
   Logging.FileLogger.Instance.Error("Settings could not be loaded; using safe defaults",ex);
   var path=Path.Combine(directory,"settings.json");
   if(File.Exists(path)){try{File.Copy(path,path+".invalid-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"),false);}catch(Exception backup){Logging.FileLogger.Instance.Error("Could not preserve invalid settings",backup);}}
   return new UserSettings();
  }
 }
 public static void Validate(UserSettings s){if(s.Theme!="Dark"||s.Policy.Limit is not (50 or 100 or 500)||s.Policy.RetentionHours is not (0 or 24 or 168))throw new ArgumentException("Unsupported settings policy");}
}
