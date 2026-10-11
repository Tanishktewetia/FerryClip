using System.Globalization;
namespace ClipSync.Windows.MainUI.Infrastructure;
/// <summary>One local-calendar display policy for all main-window timestamps.</summary>
public static class TimeFormatter
{
 public static string Format(DateTimeOffset? value, DateTimeOffset? now = null)
 {
  if(value is null)return "Not available";
  var current=(now ?? DateTimeOffset.Now).LocalDateTime; var date=value.Value.LocalDateTime;
  var elapsed=current-date;
  if(elapsed.TotalMinutes<1)return "Just now";
  if(elapsed.TotalHours<1)return $"{(int)elapsed.TotalMinutes} min ago";
  if(date.Date==current.Date)return "Today, "+date.ToString("HH:mm",CultureInfo.InvariantCulture);
  if(date.Date==current.Date.AddDays(-1))return "Yesterday, "+date.ToString("HH:mm",CultureInfo.InvariantCulture);
  return date.ToString(date.Year==current.Year?"d MMM, HH:mm":"d MMM yyyy",CultureInfo.InvariantCulture);
 }
 public static string Full(DateTimeOffset? value)=>value?.ToLocalTime().ToString("d MMMM yyyy, HH:mm:ss zzz",CultureInfo.InvariantCulture) ?? "Not available";
}
