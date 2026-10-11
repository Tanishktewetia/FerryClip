using System.Windows;
using ClipSync.Windows.Logging;
namespace ClipSync.Windows.MainUI.Services;
public static class ExceptionBoundary
{
#if DEBUG
 static bool showing;
#endif
 public static void Install(System.Windows.Application app)
 {
  app.DispatcherUnhandledException+=(_,e)=>{
   FileLogger.Instance.Error("Fatal UI exception",e.Exception);
#if DEBUG
   if(!showing){showing=true;try{System.Windows.MessageBox.Show("An unhandled UI error occurred. FerryClip cannot safely continue.\n"+e.Exception,"FerryClip Debug error",MessageBoxButton.OK,MessageBoxImage.Error);}catch(Exception nested){FileLogger.Instance.Error("Debug error dialog failed",nested);}finally{showing=false;}}
#endif
   // Unknown failures remain unhandled; never falsely claim continuation is safe.
  };
  AppDomain.CurrentDomain.UnhandledException+=(_,e)=>FileLogger.Instance.Error("Fatal process exception: "+e.ExceptionObject);
  TaskScheduler.UnobservedTaskException+=(_,e)=>FileLogger.Instance.Error("Unobserved task exception",e.Exception);
 }
}
