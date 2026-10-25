using Mizan.Core.Time;
using Xunit;

namespace Mizan.UnitTests.Core;

public class EgyptTimeTests
{
    [Fact]
    public void Zone_ShouldBeCairoOrEgyptStandardTime()
    {
        Assert.NotNull(EgyptTime.Zone);
        Assert.True(EgyptTime.Zone.Id is "Africa/Cairo" or "Egypt Standard Time");
    }

    [Fact]
    public void ToLocalDate_ConvertsUtcToCairoDateCorrectly()
    {
        // 2026-10-01 21:00:00 UTC -> in Cairo (UTC+3 in October) is 2026-10-02 00:00:00
        var storedDueDate1 = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc);
        var localDate1 = EgyptTime.ToLocalDate(storedDueDate1);
        Assert.Equal(new DateTime(2026, 10, 2), localDate1);

        // 2026-10-02 00:00:00 UTC -> 2026-10-02 03:00:00 Cairo
        var storedDueDate2 = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
        var localDate2 = EgyptTime.ToLocalDate(storedDueDate2);
        Assert.Equal(new DateTime(2026, 10, 2), localDate2);
    }

    [Fact]
    public void ToLocalDate_WithUnspecifiedKind_TreatsAsUtc()
    {
        var storedDueDate = new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Unspecified);
        var localDate = EgyptTime.ToLocalDate(storedDueDate);
        Assert.Equal(new DateTime(2026, 10, 2), localDate);
    }

    [Fact]
    public void TodayLocal_ReturnsCairoDateAtMidnight()
    {
        var utc = new DateTime(2026, 10, 1, 22, 0, 0, DateTimeKind.Utc);
        var today = EgyptTime.TodayLocal(utc);
        Assert.Equal(new DateTime(2026, 10, 2), today);
        Assert.Equal(DateTimeKind.Unspecified, today.Kind);
    }

    [Fact]
    public void LocalDateRangeToUtc_HandlesNormalDatesAndDstTransition()
    {
        // On a normal date
        var (startUtc, endUtcExclusive) = EgyptTime.LocalDateRangeToUtc(new DateTime(2026, 10, 2), new DateTime(2026, 10, 2));
        Assert.True(startUtc < endUtcExclusive);

        // Egypt DST spring-forward (last Friday of April): e.g., April 24, 2026
        // At 00:00 it jumps to 01:00, making 00:00 invalid in local time.
        // ConvertLocalMidnightToUtc should safely handle it and not throw.
        var exception = Record.Exception(() =>
        {
            var range = EgyptTime.LocalDateRangeToUtc(new DateTime(2026, 4, 24), new DateTime(2026, 4, 25));
            Assert.True(range.StartUtc < range.EndUtcExclusive);
        });

        Assert.Null(exception);
    }
}
