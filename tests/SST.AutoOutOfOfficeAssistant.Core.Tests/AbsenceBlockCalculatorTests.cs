using SST.AutoOutOfOfficeAssistant.Core;
using static SST.AutoOutOfOfficeAssistant.Core.Tests.Zurich;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class AbsenceBlockCalculatorTests
{
    private static readonly DateTimeOffset MondayMorning = Nov(2, 7);

    [Test]
    public async Task Calculate_NoEvents_ReturnsNull()
    {
        var block = AbsenceBlockCalculator.Calculate([], OfficeHours, MondayMorning);

        await Assert.That(block).IsNull();
    }

    [Test]
    public async Task Calculate_EventEndingDuringWorkingHours_ReturnsAtEventEnd()
    {
        var block = AbsenceBlockCalculator.Calculate([new TimeRange(Nov(3, 9), Nov(3, 12))], OfficeHours, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(3, 9), Nov(3, 12), Nov(3, 12)));
    }

    [Test]
    public async Task Calculate_AllDayFriday_ReturnsOnMondayMorning()
    {
        var block = AbsenceBlockCalculator.Calculate([AllDayNov(6)], OfficeHours, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(6), Nov(7), Nov(9, 8)));
    }

    [Test]
    [Arguments(11, 15, "overlapping")]
    [Arguments(12, 15, "touching")]
    public async Task Calculate_OverlappingOrTouchingEvents_MergesIntoOneBlock(int secondStartHour, int secondEndHour, string _)
    {
        TimeRange[] events = [new(Nov(3, 9), Nov(3, 12)), new(Nov(3, secondStartHour), Nov(3, secondEndHour))];

        var block = AbsenceBlockCalculator.Calculate(events, OfficeHours, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(3, 9), Nov(3, 15), Nov(3, 15)));
    }

    [Test]
    public async Task Calculate_FridayAndMonday_BridgesTheWeekend()
    {
        var block = AbsenceBlockCalculator.Calculate([AllDayNov(9), AllDayNov(6)], OfficeHours, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(6), Nov(10), Nov(10, 8)));
    }

    [Test]
    public async Task Calculate_EveningGap_IsBridged()
    {
        TimeRange[] events = [new(Nov(3, 13), Nov(3, 17)), new(Nov(4, 8), Nov(4, 17))];

        var block = AbsenceBlockCalculator.Calculate(events, OfficeHours, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(3, 13), Nov(4, 17), Nov(5, 8)));
    }

    [Test]
    public async Task MergeBlocks_WorkingDayInBetween_SplitsIntoTwoBlocks()
    {
        var blocks = AbsenceBlockCalculator.MergeBlocks([AllDayNov(3), AllDayNov(5)], OfficeHours);

        await Assert.That(blocks).IsEquivalentTo(new[]
        {
            new AbsenceBlock(Nov(3), Nov(4), Nov(4, 8)),
            new AbsenceBlock(Nov(5), Nov(6), Nov(6, 8)),
        });
    }

    [Test]
    public async Task Calculate_ShortWorkingGap_SplitsBlocks()
    {
        // One working hour between the events breaks the block.
        TimeRange[] events = [new(Nov(3, 8), Nov(3, 12)), new(Nov(3, 13), Nov(3, 17))];

        var block = AbsenceBlockCalculator.Calculate(events, OfficeHours, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(3, 8), Nov(3, 12), Nov(3, 12)));
    }

    [Test]
    public async Task Calculate_FirstBlockOver_ReturnsNextUpcomingBlock()
    {
        var block = AbsenceBlockCalculator.Calculate([AllDayNov(3), AllDayNov(9)], OfficeHours, Nov(5, 10));

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(9), Nov(10), Nov(10, 8)));
    }

    [Test]
    public async Task Calculate_AfterLastEventButBeforeReturn_StillReturnsBlock()
    {
        var block = AbsenceBlockCalculator.Calculate([AllDayNov(6)], OfficeHours, Nov(7, 10));

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(6), Nov(7), Nov(9, 8)));
    }

    [Test]
    public async Task Calculate_AcrossDstChange_UsesLocalWorkingHours()
    {
        // Fri 23 Oct (CEST, UTC+2) and Mon 26 Oct (CET, UTC+1) 2026; DST ends Sun 25 Oct.
        TimeRange[] events =
        [
            new(At(2026, 10, 23), At(2026, 10, 24)),
            new(At(2026, 10, 26), At(2026, 10, 27)),
        ];

        var block = AbsenceBlockCalculator.Calculate(events, OfficeHours, At(2026, 10, 22));

        await Assert.That(block!.Start).IsEqualTo(new DateTimeOffset(2026, 10, 22, 22, 0, 0, TimeSpan.Zero));
        await Assert.That(block.ReturnAt).IsEqualTo(new DateTimeOffset(2026, 10, 27, 7, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task Calculate_NoWorkingDays_BridgesEverythingAndReturnsAtEnd()
    {
        var noWorkingDays = new WorkingHours([], new TimeOnly(8, 0), new TimeOnly(17, 0), Zone);

        var block = AbsenceBlockCalculator.Calculate([AllDayNov(3), AllDayNov(5)], noWorkingDays, MondayMorning);

        await Assert.That(block).IsEqualTo(new AbsenceBlock(Nov(3), Nov(6), Nov(6)));
    }

    [Test]
    public async Task Calculate_EmptyEvent_IsIgnored()
    {
        var block = AbsenceBlockCalculator.Calculate([new TimeRange(Nov(3, 9), Nov(3, 9))], OfficeHours, MondayMorning);

        await Assert.That(block).IsNull();
    }
}
