#if DEBUG
using ClipSync.Windows.MainUI.Infrastructure;
namespace ClipSync.Windows.MainUI.Debug;
public static class TimeFormatterTests
{
 public static void Run()
 {
  var now=new DateTimeOffset(new DateTime(2026,10,11,12,0,0,DateTimeKind.Local));
  void Check(DateTimeOffset? date,string expected){var actual=TimeFormatter.Format(date,now);if(actual!=expected)throw new Exception($"Time formatter: expected {expected}, got {actual}");}
  Check(null,"Not available");Check(now.AddSeconds(-59),"Just now");Check(now.AddMinutes(-1),"1 min ago");Check(now.AddMinutes(-59),"59 min ago");Check(now.AddHours(-1),"Today, 11:00");Check(now.AddDays(-1),"Yesterday, 12:00");Check(now.AddDays(-2),"9 Oct, 12:00");Check(now.AddYears(-1),"11 Oct 2025");
  if(!TimeFormatter.Full(now).Contains("11 October 2026, 12:00:00"))throw new Exception("Full timestamp tooltip policy");
 }
}
#endif
