using SST.AutoOutOfOfficeAssistant.Core;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

/// <summary>
/// Test helpers for local times in Europe/Zurich. In November 2026: Mon 2, Fri 6, Sat 7, Sun 8, Mon 9.
/// DST ends on Sunday 25 October 2026.
/// </summary>
internal static class Zurich
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static WorkingHours OfficeHours { get; } = WorkingHours.Default(Zone);

    /// <summary>Converts a Zurich local time to a UTC instant.</summary>
    public static DateTimeOffset At(int year, int month, int day, int hour = 0, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>A November 2026 instant.</summary>
    public static DateTimeOffset Nov(int day, int hour = 0, int minute = 0) => At(2026, 11, day, hour, minute);

    /// <summary>An all-day event on the given November 2026 day (midnight to midnight).</summary>
    public static TimeRange AllDayNov(int day) => new(Nov(day), Nov(day + 1));
}
