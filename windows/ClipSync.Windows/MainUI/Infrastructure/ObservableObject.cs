using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace ClipSync.Windows.MainUI.Infrastructure;
public abstract class ObservableObject : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 protected bool Set<T>(ref T field,T value,[CallerMemberName] string? name=null){if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;Raise(name);return true;}
 protected void Raise([CallerMemberName] string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed class RelayCommand(Action execute,Func<bool>? canExecute=null):ICommand
{
 public bool CanExecute(object? p)=>canExecute?.Invoke()??true;
 public void Execute(object? p)=>execute();
 public event EventHandler? CanExecuteChanged;
 public void Refresh()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
}
public sealed class AsyncCommand(Func<Task> execute,Action<Exception> failed):ICommand
{
 bool busy;
 public bool CanExecute(object? p)=>!busy;
 public event EventHandler? CanExecuteChanged;
 public async void Execute(object? p){if(busy)return;busy=true;CanExecuteChanged?.Invoke(this,EventArgs.Empty);try{await execute();}catch(Exception ex){failed(ex);}finally{busy=false;CanExecuteChanged?.Invoke(this,EventArgs.Empty);}}
}
