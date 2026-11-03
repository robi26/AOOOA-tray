using SST.AutoOutOfOfficeAssistant.Core;
using static SST.AutoOutOfOfficeAssistant.Core.Tests.Zurich;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class WorkingHoursTests
{
    [Test]
    public async Task NextWorkingStart_DuringWorkingHours_ReturnsSameInstant()
    {
        var result = OfficeHours.NextWorkingStart(Nov(3, 10, 30));

        await Assert.That(result).IsEqualTo(Nov(3, 10, 30));
    }

    [Test]
    [Arguments(6, 17, 9)]
    [Arguments(7, 12, 9)]
    [Arguments(3, 6, 3)]
    [Arguments(3, 20, 4)]
    public async Task NextWorkingStart_OutsideWorkingHours_ReturnsNextMorning(int day, int hour, int expectedDay)
    {
        var result = OfficeHours.NextWorkingStart(Nov(day, hour));

        await Assert.That(result).IsEqualTo(Nov(expectedDay, 8));
    }

    [Test]
    public async Task ContainsWorkingTime_Weekend_IsFalse()
    {
        var result = OfficeHours.ContainsWorkingTime(new TimeRange(Nov(6, 17), Nov(9, 8)));

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ContainsWorkingTime_OvernightShift_CoversNextMorning()
    {
        var nightShift = new WorkingHours([DayOfWeek.Monday], new TimeOnly(22, 0), new TimeOnly(6, 0), Zone);

        var result = nightShift.ContainsWorkingTime(new TimeRange(Nov(3, 1), Nov(3, 2)));

        await Assert.That(result).IsTrue();
    }
}
