using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Graph;
using Microsoft.Graph.Models;
using TimeRange = SST.AutoOutOfOfficeAssistant.Core.TimeRange;

namespace SST.AutoOutOfOfficeAssistant.Graph.Tests;

public class GraphMailboxGatewayTests
{
    private static readonly TimeZoneInfo Zurich = TimeZoneResolver.Resolve("W. Europe Standard Time");

    private static DateTimeTimeZone Utc(string value) => new() { DateTime = value, TimeZone = "UTC" };

    [Test]
    public async Task ToRange_AllDayEvent_RunsFromMidnightToMidnightInMailboxTimeZone()
    {
        // As Graph returns an all-day event Fri 6 – Mon 9 Nov with Prefer: outlook.timezone="UTC".
        var e = new Event { IsAllDay = true, Start = Utc("2026-11-06T00:00:00.0000000"), End = Utc("2026-11-10T00:00:00.0000000") };

        var range = GraphMailboxGateway.ToRange(e, Zurich);

        await Assert.That(range).IsEqualTo(new TimeRange(
            new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.FromHours(1)),
            new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.FromHours(1))));
    }

    [Test]
    public async Task ToRange_AllDayEventAcrossDstChange_UsesEachDaysOffset()
    {
        var e = new Event { IsAllDay = true, Start = Utc("2026-10-23T00:00:00"), End = Utc("2026-10-27T00:00:00") };

        var range = GraphMailboxGateway.ToRange(e, Zurich);

        await Assert.That(range).IsEqualTo(new TimeRange(
            new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.FromHours(1))));
    }

    [Test]
    public async Task ToRange_TimedEvent_KeepsUtcInstants()
    {
        var e = new Event { IsAllDay = false, Start = Utc("2026-11-06T13:00:00.0000000"), End = Utc("2026-11-06T17:00:00.0000000") };

        var range = GraphMailboxGateway.ToRange(e, Zurich);

        await Assert.That(range).IsEqualTo(new TimeRange(
            new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 6, 17, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public async Task ToRange_MissingEnd_ReturnsNull()
    {
        var e = new Event { IsAllDay = true, Start = Utc("2026-11-06T00:00:00") };

        await Assert.That(GraphMailboxGateway.ToRange(e, Zurich)).IsNull();
    }
}
