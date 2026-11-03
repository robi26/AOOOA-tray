namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// The weekly working hours of a mailbox (Outlook "Working hours"), in the mailbox's time zone.
/// </summary>
public sealed class WorkingHours
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkingHours"/> class.
    /// </summary>
    /// <param name="days">The working days.</param>
    /// <param name="startTime">Daily start (local time).</param>
    /// <param name="endTime">Daily end (local time). A value at or before <paramref name="startTime"/> means the period ends on the next day.</param>
    /// <param name="timeZone">The time zone the times are expressed in.</param>
    public WorkingHours(IEnumerable<DayOfWeek> days, TimeOnly startTime, TimeOnly endTime, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(timeZone);

        this.Days = days.ToHashSet();
        this.StartTime = startTime;
        this.EndTime = endTime;
        this.TimeZone = timeZone;
    }

    /// <summary>Gets the working days.</summary>
    public IReadOnlySet<DayOfWeek> Days { get; }

    /// <summary>Gets the daily start time.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Gets the daily end time.</summary>
    public TimeOnly EndTime { get; }

    /// <summary>Gets the time zone of <see cref="StartTime"/> and <see cref="EndTime"/>.</summary>
    public TimeZoneInfo TimeZone { get; }

    /// <summary>
    /// Monday to Friday, 08:00 to 17:00. Used when a mailbox has no working hours configured.
    /// </summary>
    /// <param name="timeZone">The time zone.</param>
    /// <returns>The default working hours.</returns>
    public static WorkingHours Default(TimeZoneInfo timeZone) => new(
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        new TimeOnly(8, 0),
        new TimeOnly(17, 0),
        timeZone);

    /// <summary>
    /// Returns all working periods (as UTC ranges) that overlap <paramref name="range"/>, ordered by start.
    /// </summary>
    /// <param name="range">The range to inspect.</param>
    /// <returns>The overlapping working periods.</returns>
    public IEnumerable<TimeRange> PeriodsOverlapping(TimeRange range)
    {
        if (this.Days.Count == 0 || range.IsEmpty)
        {
            yield break;
        }

        // Start one day early so a period that began the day before (overnight hours) is not missed.
        var firstDay = this.LocalDate(range.Start).AddDays(-1);
        var lastDay = this.LocalDate(range.End);

        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            if (!this.Days.Contains(day.DayOfWeek))
            {
                continue;
            }

            var period = this.PeriodOn(day);
            if (period.Overlaps(range))
            {
                yield return period;
            }
        }
    }

    /// <summary>
    /// Returns true when any working time falls inside <paramref name="range"/>.
    /// </summary>
    /// <param name="range">The range to inspect.</param>
    /// <returns>True when the range contains working time.</returns>
    public bool ContainsWorkingTime(TimeRange range) => this.PeriodsOverlapping(range).Any();

    /// <summary>
    /// Returns the first working instant at or after <paramref name="instant"/>: the instant itself when it falls
    /// inside working hours, otherwise the start of the next working period.
    /// </summary>
    /// <param name="instant">The instant.</param>
    /// <returns>The first working instant.</returns>
    public DateTimeOffset NextWorkingStart(DateTimeOffset instant)
    {
        // Any non-empty set of working days has a period within the next 8 days.
        foreach (var period in this.PeriodsOverlapping(new TimeRange(instant, instant.AddDays(8))))
        {
            return period.Start > instant ? period.Start : instant;
        }

        return instant;
    }

    private DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, this.TimeZone).DateTime);

    private TimeRange PeriodOn(DateOnly day)
    {
        var start = this.ToInstant(day.ToDateTime(this.StartTime));
        var endDay = this.EndTime > this.StartTime ? day : day.AddDays(1);
        var end = this.ToInstant(endDay.ToDateTime(this.EndTime));
        return new TimeRange(start, end);
    }

    private DateTimeOffset ToInstant(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A local time inside a DST gap does not exist; move forward to the first valid time.
        while (this.TimeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddMinutes(15);
        }

        return new DateTimeOffset(unspecified, this.TimeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }
}
