using ClipSync.Windows.MainUI.Infrastructure;
namespace ClipSync.Windows.MainUI.Infrastructure;
/// <summary>Factory routes release their previous page on navigation. Phase 2 registers real page factories.</summary>
public sealed class NavigationService : ObservableObject, IDisposable
{
 readonly Dictionary<string,Func<object?>> routes=new(StringComparer.Ordinal);object? content;
 public object? Content{get=>content;private set=>Set(ref content,value);}
 public void Register(string name,Func<object?> factory)=>routes[name]=factory;
 public void Navigate(string name){if(!routes.TryGetValue(name,out var factory))throw new ArgumentException("Unknown route: "+name);var next=factory();if(Content is IDisposable old)old.Dispose();Content=next;}
 public void Dispose(){if(Content is IDisposable old)old.Dispose();Content=null;}
}
