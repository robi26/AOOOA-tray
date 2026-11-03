using SST.AutoOutOfOfficeAssistant.Core;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class PlainTextTemplateTests
{
    [Test]
    public async Task ToHtml_BlankLines_SeparateParagraphs()
    {
        var html = PlainTextTemplate.ToHtml("Erster Absatz\r\n\r\nZweiter Absatz");

        await Assert.That(html).IsEqualTo("<p>Erster Absatz</p><p>Zweiter Absatz</p>");
    }

    [Test]
    public async Task ToHtml_SingleLineBreak_BecomesBr()
    {
        var html = PlainTextTemplate.ToHtml("Freundliche Grüsse\nAnna Muster");

        await Assert.That(html).IsEqualTo("<p>Freundliche Grüsse<br>Anna Muster</p>");
    }

    [Test]
    public async Task ToHtml_MarkupCharacters_AreEncodedButPlaceholdersKept()
    {
        var html = PlainTextTemplate.ToHtml("<b>bis</b> {EndDate} & mehr");

        await Assert.That(html).IsEqualTo("<p>&lt;b&gt;bis&lt;/b&gt; {EndDate} &amp; mehr</p>");
    }

    [Test]
    [Arguments(null)]
    [Arguments("  \n ")]
    public async Task ToHtml_Empty_ReturnsEmpty(string? text)
    {
        await Assert.That(PlainTextTemplate.ToHtml(text)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ToHtml_Default_HasTwoParagraphsWithPlaceholders()
    {
        var html = PlainTextTemplate.ToHtml(PlainTextTemplate.Default);

        await Assert.That(html).StartsWith("<p>Vielen Dank").And.Contains("</p><p>Thank you").And.Contains("{ReturnDate}");
    }
}
