#if DEBUG
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace ClipSync.Windows.MainUI.Debug;
/// <summary>Real SendInput on isolated preview HWND only. Saves/restores cursor position.</summary>
internal static class NativeInput
{
 [StructLayout(LayoutKind.Sequential)] struct Input {public uint Type;public Union Data;}
 [StructLayout(LayoutKind.Explicit)] struct Union {[FieldOffset(0)] public Mouse Mouse;[FieldOffset(0)] public Keyboard Keyboard;}
 [StructLayout(LayoutKind.Sequential)] struct Mouse {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)] struct Keyboard {public ushort Key,Scan;public uint Flags,Time;public UIntPtr Extra;}
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] input,int size);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out PointNative point);
 [StructLayout(LayoutKind.Sequential)] struct PointNative {public int X,Y;}
 static void Send(Input input){if(SendInput(1,[input],Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("SendInput unavailable");}
 static void Key(ushort key,bool up)=>Send(new(){Type=1,Data=new(){Keyboard=new(){Key=key,Flags=up?2u:0u}}});
 static void SendMouse(uint flag)=>Send(new(){Type=0,Data=new(){Mouse=new(){Flags=flag}}});
 public static async Task Run(MainWindow window,Action<string> capture)
 {
  GetCursorPos(out var old);bool down=false;
  try{
   window.Activate();var button=MainWindow.Descendants<System.Windows.Controls.Button>(window).First(b=>Equals(b.ToolTip,"Dashboard"));if(new System.Windows.Interop.WindowInteropHelper(window).Handle!=GetForegroundWindow())throw new Exception("Preview not foreground; refusing physical input");var point=button.PointToScreen(new System.Windows.Point(button.ActualWidth/2,button.ActualHeight/2));SetCursorPos((int)point.X,(int)point.Y);await Task.Delay(200);if(!button.IsMouseOver)throw new Exception("Physical hover failed");capture("hover");SendMouse(2);down=true;await Task.Delay(100);if(!button.IsPressed)throw new Exception("Physical press failed");capture("pressed");SendMouse(4);down=false;await Task.Delay(100);
   Key(0x10,false);Key(0x09,false);Key(0x09,true);Key(0x10,true);await Task.Delay(100);if(System.Windows.Input.Keyboard.FocusedElement is null)throw new Exception("Physical reverse Tab failed");capture("reverse-tab");
   var vm=(ViewModels.ShellViewModel)window.DataContext;var confirmation=vm.ConfirmAsync("Keyboard trap test","Isolated preview");await Task.Delay(180);
   for(int i=0;i<5;i++){Key(0x10,false);Key(0x09,false);Key(0x09,true);Key(0x10,true);await Task.Delay(50);if(!MainWindow.Descendants<StackPanel>(window).Any(p=>p.Name=="DialogHost"&&p.IsKeyboardFocusWithin))throw new Exception("Physical reverse-tab escaped dialog");}
   capture("dialog-reverse-tab");Key(0x1B,false);Key(0x1B,true);await Task.Delay(100);if(await confirmation)throw new Exception("Escape did not cancel dialog");
   if(MainWindow.Descendants<System.Windows.Controls.Button>(window).FirstOrDefault(b=>Equals(b.Content,"Primary")) is {} primary){
    foreach(var sample in new[]{primary,MainWindow.Descendants<System.Windows.Controls.Button>(window).First(b=>Equals(b.Content,"Secondary"))}){var pos=sample.PointToScreen(new System.Windows.Point(sample.ActualWidth/2,sample.ActualHeight/2));SetCursorPos((int)pos.X,(int)pos.Y);await Task.Delay(100);capture(sample.Content+"-hover");SendMouse(2);down=true;await Task.Delay(80);if(!sample.IsPressed)throw new Exception("Gallery press failed");capture(sample.Content+"-pressed");SendMouse(4);down=false;await Task.Delay(80);Key(0x09,false);Key(0x09,true);await Task.Delay(80);capture(sample.Content+"-keyboard");}
   }
  }finally{if(down)SendMouse(4);Key(0x10,true);SetCursorPos(old.X,old.Y);}
 }
}
#endif




