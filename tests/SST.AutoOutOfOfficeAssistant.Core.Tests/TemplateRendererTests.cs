using System.Globalization;
using SST.AutoOutOfOfficeAssistant.Core;
using static SST.AutoOutOfOfficeAssistant.Core.Tests.Zurich;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class TemplateRendererTests
{
    private static readonly AbsenceBlock FridayAllDay = new(Nov(6), Nov(7), Nov(9, 8));
    private static readonly CultureInfo SwissGerman = CultureInfo.GetCultureInfo("de-CH");

    [Test]
    [Arguments("{StartDate}", "06.11.2026")]
    [Arguments("{EndDate}", "06.11.2026")]
    [Arguments("{ReturnDate}", "09.11.2026")]
    [Arguments("{ReturnDateTime}", "09.11.2026 08:00")]
    [Arguments("{enddate}", "06.11.2026")]
    public async Task Render_Placeholder_IsReplacedInLocalTime(string template, string expected)
    {
        var result = TemplateRenderer.Render(template, FridayAllDay, Zone, SwissGerman);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Render_EndDateOfAllDayEvent_IsTheLastAbsentDay()
    {
        // The event ends at midnight on Saturday, but Friday is the last day of absence.
        var result = TemplateRenderer.Render("bis {EndDate}", FridayAllDay, Zone, SwissGerman);

        await Assert.That(result).IsEqualTo("bis 06.11.2026");
    }

    [Test]
    public async Task Render_UnknownPlaceholder_IsLeftUnchanged()
    {
        var result = TemplateRenderer.Render("<p>{Unknown} {ReturnDate}</p>", FridayAllDay, Zone, SwissGerman);

        await Assert.That(result).IsEqualTo("<p>{Unknown} 09.11.2026</p>");
    }

    [Test]
    public async Task Render_EnglishCulture_UsesEnglishDateFormat()
    {
        var result = TemplateRenderer.Render("{ReturnDate}", FridayAllDay, Zone, CultureInfo.GetCultureInfo("en-GB"));

        await Assert.That(result).IsEqualTo("09/11/2026");
    }
}
