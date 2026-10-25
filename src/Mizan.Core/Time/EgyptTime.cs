namespace Mizan.Core.Time;

public static class EgyptTime
{
    public static TimeZoneInfo Zone { get; }

    static EgyptTime()
    {
        try
        {
            Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                Zone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            }
            catch
            {
                Zone = TimeZoneInfo.CreateCustomTimeZone("Africa/Cairo", TimeSpan.FromHours(2), "Cairo", "Cairo");
            }
        }
    }

    /// <summary>
    /// Returns the local calendar date in Egypt (Kind=Unspecified, 00:00).
    /// </summary>
    public static DateTime TodayLocal(DateTime? utcNow = null)
    {
        var utc = utcNow ?? DateTime.UtcNow;
        if (utc.Kind != DateTimeKind.Utc)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }
        var cairoTime = TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
        return DateTime.SpecifyKind(cairoTime.Date, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Treats storedDueDate (Kind Unspecified or Utc) as UTC, converts to Cairo time, and returns the Date (Kind=Unspecified).
    /// </summary>
    public static DateTime ToLocalDate(DateTime storedDueDate)
    {
        var utc = storedDueDate.Kind switch
        {
            DateTimeKind.Utc => storedDueDate,
            DateTimeKind.Local => storedDueDate.ToUniversalTime(),
            _ => DateTime.SpecifyKind(storedDueDate, DateTimeKind.Utc)
        };
        var cairoTime = TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
        return DateTime.SpecifyKind(cairoTime.Date, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Converts a local date range [fromLocalDate, toLocalDateInclusive] to UTC [StartUtc, EndUtcExclusive).
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtcExclusive) LocalDateRangeToUtc(DateTime fromLocalDate, DateTime toLocalDateInclusive)
    {
        var startUtc = ConvertLocalMidnightToUtc(fromLocalDate);
        var endUtcExclusive = ConvertLocalMidnightToUtc(toLocalDateInclusive.Date.AddDays(1));
        return (startUtc, endUtcExclusive);
    }

    private static DateTime ConvertLocalMidnightToUtc(DateTime localDate)
    {
        var dt = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(dt))
        {
            dt = dt.AddHours(1);
        }
        return TimeZoneInfo.ConvertTimeToUtc(dt, Zone);
    }
}
