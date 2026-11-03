namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// The bilingual (German/English) reply templates new users start with.
/// </summary>
public static class DefaultTemplates
{
    /// <summary>Gets the default template for senders inside the organisation.</summary>
    public const string Internal =
        "<p>Vielen Dank für Ihre Nachricht. Ich bin bis und mit {EndDate} abwesend und ab {ReturnDate} wieder erreichbar. "
        + "Ihre E-Mail wird nicht weitergeleitet.</p>"
        + "<p>Thank you for your message. I am out of the office until {EndDate} and will be back on {ReturnDate}. "
        + "Your e-mail will not be forwarded.</p>";

    /// <summary>Gets the default template for external senders.</summary>
    public const string External = Internal;
}
