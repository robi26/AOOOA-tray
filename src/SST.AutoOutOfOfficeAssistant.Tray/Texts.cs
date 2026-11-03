using System.Globalization;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>UI texts in German (Windows display language German) or English (anything else).</summary>
internal static class Texts
{
    private static readonly bool De = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    public static string AppName => De ? "Automatische Abwesenheitsnotiz" : "Automatic out-of-office";

    // Tray menu
    public static string MenuSettings => De ? "Einstellungen…" : "Settings…";

    public static string MenuCheckNow => De ? "Jetzt prüfen" : "Check now";

    public static string MenuSignIn => De ? "Anmelden…" : "Sign in…";

    public static string MenuSignOut(string user) => De ? $"Abmelden ({user})" : $"Sign out ({user})";

    public static string MenuAutostart => De ? "Mit Windows starten" : "Start with Windows";

    public static string MenuExit => De ? "Beenden" : "Exit";

    // Status
    public static string StatusNotConfigured => De ? "Nicht konfiguriert (ClientId in appsettings.json fehlt)" : "Not configured (ClientId missing in appsettings.json)";

    public static string StatusSignInRequired => De ? "Anmeldung erforderlich" : "Sign-in required";

    public static string StatusDisabled => De ? "Ausgeschaltet" : "Switched off";

    public static string StatusNotRun => De ? "Noch nicht geprüft" : "Not checked yet";

    public static string StatusIdle => De ? "Keine Abwesenheit eingetragen" : "No absence in the calendar";

    public static string StatusScheduled => De ? "Abwesenheitsnotiz geplant" : "Automatic reply scheduled";

    public static string StatusManual => De ? "Manuell verwaltet – die App ändert nichts" : "Managed manually – the app changes nothing";

    public static string StatusNotInScope => De ? "Kein Zugriff auf das Postfach" : "No access to the mailbox";

    public static string StatusError(string? error) => De ? $"Fehler: {error}" : $"Error: {error}";

    public static string StatusOffline => De ? "Offline – nächster Versuch automatisch" : "Offline – will retry automatically";

    public static string BlockLine(string start, string end, string back) =>
        De ? $"Abwesend {start} – {end}, Notiz aktiv bis {back}" : $"Away {start} – {end}, reply active until {back}";

    public static string NextAbsence(string range) => De ? $"Nächste Abwesenheit: {range}" : $"Next absence: {range}";

    public static string CurrentAbsence(string until) => De ? $"Abwesend bis {until}" : $"Away until {until}";

    public static string ReplyUntil(string back) => De ? $"Notiz aktiv bis {back}" : $"Reply active until {back}";

    public static string NoAbsence => De ? "Keine Abwesenheit im Kalender" : "No absence in the calendar";

    public static string LastRun(string when) => De ? $"Letzte Prüfung: {when}" : $"Last check: {when}";

    // Balloons
    public static string BalloonScheduledTitle => De ? "Abwesenheitsnotiz geplant" : "Automatic reply scheduled";

    public static string BalloonScheduled(string start, string back) =>
        De ? $"Von {start} bis {back}. Exchange schaltet sie selbst ein und aus – der PC muss dann nicht laufen."
           : $"From {start} until {back}. Exchange switches it on and off itself – the PC does not need to be on.";

    public static string BalloonSignInTitle => De ? "Bitte anmelden" : "Please sign in";

    public static string BalloonSignIn => De ? "Klicken Sie hier, um sich anzumelden. Bis dahin wird Ihr Kalender nicht geprüft." : "Click here to sign in. Until then your calendar is not checked.";

    // Settings window
    public static string SettingsTitle => De ? "Einstellungen" : "Settings";

    public static string GroupAccount => De ? "Konto" : "Account";

    public static string GroupStatus => De ? "Status" : "Status";

    public static string GroupSettings => De ? "Abwesenheitsnotiz" : "Automatic reply";

    public static string GroupPreview => De ? "Vorschau" : "Preview";

    public static string NotSignedIn => De ? "Nicht angemeldet" : "Not signed in";

    public static string SignedInAs(string user) => De ? $"Angemeldet als {user}" : $"Signed in as {user}";

    public static string ButtonSignIn => De ? "Anmelden" : "Sign in";

    public static string ButtonSignOut => De ? "Abmelden" : "Sign out";

    public static string EnableLabel => De
        ? "Abwesenheitsnotiz automatisch aus meinem Kalender verwalten (Termine mit «Anzeigen als: Abwesend»)"
        : "Manage my automatic reply from my calendar (events shown as \"Out of office\")";

    public static string AudienceLabel => De ? "Antwort an externe Absender:" : "Reply to external senders:";

    public static string AudienceAll => De ? "Alle externen Absender" : "All external senders";

    public static string AudienceContacts => De ? "Nur meine Kontakte" : "My contacts only";

    public static string AudienceNone => De ? "Keine Antwort nach aussen" : "No external reply";

    public static string CultureLabel => De ? "Datumsformat:" : "Date format:";

    public static string InternalLabel => De ? "Nachricht innerhalb meiner Organisation:" : "Message inside my organisation:";

    public static string ExternalLabel => De ? "Nachricht an externe Absender:" : "Message to external senders:";

    public static string CopyInternal => De ? "Interne übernehmen" : "Copy internal";

    public static string PlaceholderHelp => De ? "Platzhalter:" : "Placeholders:";

    public static string PreviewCaption(string start, string end) => De ? $"Beispiel: abwesend {start} bis {end}" : $"Example: away {start} to {end}";

    public static string ButtonDefaults => De ? "Standardtexte" : "Default texts";

    public static string ButtonSave => De ? "Speichern" : "Save";

    public static string ButtonClose => De ? "Schliessen" : "Close";

    public static string ButtonCheckNow => De ? "Jetzt prüfen" : "Check now";

    public static string Saved => De ? "Gespeichert. Ihr Kalender wird jetzt geprüft." : "Saved. Your calendar is being checked now.";

    public static string SignOutConfirm => De
        ? "Nach dem Abmelden prüft die App Ihren Kalender nicht mehr. Eine bereits geplante Abwesenheitsnotiz bleibt in Outlook bestehen.\n\nAbmelden?"
        : "After signing out the app no longer checks your calendar. An automatic reply that is already scheduled stays in Outlook.\n\nSign out?";

    public static string SignInFailed(string message) => De ? $"Die Anmeldung ist fehlgeschlagen:\n{message}" : $"Sign-in failed:\n{message}";

    public static string PlaceholderDescription(string placeholder, string germanDescription) => De
        ? germanDescription
        : placeholder switch
        {
            "{StartDate}" => "First day of the absence",
            "{StartDateTime}" => "Start of the absence (date and time)",
            "{EndDate}" => "Last day of the absence",
            "{EndDateTime}" => "End of the absence (date and time)",
            "{ReturnDate}" => "Day you are back at work",
            "{ReturnDateTime}" => "Time you are back at work",
            _ => germanDescription,
        };
}
