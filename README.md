# AOOOA – Automatic Out-of-Office Assistant (tray app)

A small Windows tray app (`AOOOA.exe`) that turns the user's Outlook automatic replies on and off based on
their calendar. It works with the signed-in user's **delegated** permissions and runs for work or school
accounts of any Microsoft 365 organisation.

- An event with **Show as: Out of office** (Graph `showAs = oof`) marks an absence.
- Consecutive or overlapping OOO events are merged into one **absence block**. A gap between two events only
  breaks the block if it contains working time according to the user's Outlook **working hours**, so nights and
  weekends are bridged (OOO Friday + OOO Monday = one block).
- For the current or next block the app sets a **scheduled** automatic reply from the block start until the
  user's first working time after the block (the "return"). Exchange itself switches the reply on and off at
  those times, so **the PC does not have to run during the absence**.
- The reply text comes from the user's templates, with placeholders such as `{EndDate}` and `{ReturnDate}`.
- **Replies the user sets up by hand are never touched** (see [Ownership](#ownership-manual-replies-are-never-touched)).

**Use only one tool to manage a mailbox's automatic replies.** The app remembers only what it wrote itself,
so a reply set by another tool counts as manual and is left alone.

## Download and install

Requirements: Windows 10/11 (64-bit) and a work or school Microsoft 365 account with an Exchange Online
mailbox. Personal Microsoft accounts (outlook.com, hotmail) and on-premises Exchange are not supported.
No .NET installation and no admin rights are needed.

1. Download `AOOOA-<version>-win-x64.zip` from the latest release:
   **https://github.com/robi26/AOOOA-tray/releases/latest**
2. Right-click the zip → **Properties** → tick **Unblock** → **OK**. Otherwise Windows shows a SmartScreen
   warning on every start. If it still appears, click **More info** → **Run anyway**.
3. Unpack it to **`%LOCALAPPDATA%\Programs\AOOOA`** (paste the path into the Explorer address bar). Keep
   `AOOOA.exe` and `appsettings.json` together in that folder. Don't run it from the Downloads folder: the
   autostart entry remembers the exe's location.
4. Start `AOOOA.exe`. The app appears as an icon in the taskbar notification area (behind the `^` arrow).

Or in PowerShell, from the folder with the zip:

```powershell
$target = "$env:LOCALAPPDATA\Programs\AOOOA"
Unblock-File .\AOOOA-*-win-x64.zip
Expand-Archive .\AOOOA-*-win-x64.zip -DestinationPath $target -Force
Start-Process "$target\AOOOA.exe"
```

### First start
1. **Sign in** with your work or school account. On a PC joined to your organisation this happens without
   asking. The first time, Microsoft asks you to allow the app to read your calendar and manage your mailbox
   settings. If your organisation blocks this, your IT administrator has to approve the app (see
   [Users from other organisations](#users-from-other-organisations)).
2. In the settings window, tick **"Abwesenheitsnotiz automatisch aus meinem Kalender verwalten"**
   (*Manage my automatic reply from my calendar*), adjust the texts if you like, and click **Speichern** (*Save*).
3. In the tray menu, tick **"Mit Windows starten"** (*Start with Windows*).
4. Mark an absence in your Outlook calendar with **Show as: Out of office**. The tray menu then shows the next
   absence and until when the automatic reply will be active.

### Update and uninstall
- **Update:** quit the app (tray menu → **Beenden** / *Exit*), replace both files with the new version and
  start it again. Settings and sign-in are kept in `%LOCALAPPDATA%\SST\AOOOA`.
- **Uninstall:** switch off **"Mit Windows starten"**, quit the app, then delete
  `%LOCALAPPDATA%\Programs\AOOOA` and `%LOCALAPPDATA%\SST\AOOOA`. A reply the app has already scheduled stays
  in Outlook until its end time; switch it off there if needed.

## Projects

| Project | Purpose |
|---|---|
| `src/SST.AutoOutOfOfficeAssistant.Core` | Pure logic: block calculation, working hours, template rendering, ownership, sync planner, `MailboxSyncService` |
| `src/SST.AutoOutOfOfficeAssistant.Graph` | `GraphMailboxGateway`: calendar and automatic replies via Microsoft Graph |
| `src/SST.AutoOutOfOfficeAssistant.Tray` | WinForms tray app: sign-in (MSAL + Windows broker), local settings, sync loop, UI |
| `tests/*.Tests` | TUnit tests |

## How it works

| | |
|---|---|
| Permissions | Delegated Microsoft Graph: `User.Read`, `Calendars.ReadBasic`, `MailboxSettings.ReadWrite` (plus `offline_access`) |
| App registration | **app-ext-aoooatray-p**, client ID `6f328276-c32c-4b39-93f1-81f495ce5cc6`, multi-tenant public client |
| Sign-in | Windows broker (WAM): silent with the Windows account on Entra-joined PCs, otherwise an account picker once |
| Checks | At start, every 15 minutes (`SyncIntervalMinutes`, minimum 5), after saving the settings, on "Jetzt prüfen", after wake-up and when the network returns |
| Settings | `%LOCALAPPDATA%\SST\AOOOA\settings.json`; tokens in `msal.cache` (DPAPI-encrypted for the Windows user); log in `aoooa.log` |
| Templates | Plain text; a blank line starts a new paragraph |
| Language | German when the Windows display language is German, otherwise English |

Menu: status, the next (or current) absence and until when the reply is active, "Jetzt prüfen", settings,
sign in/out, "Mit Windows starten" (current user's Run key), exit. Notifications appear when a reply is
scheduled and when sign-in is needed.

### Absence blocks
1. Events from 31 days ago to 90 days ahead are read with `calendarView` (`Prefer: outlook.timezone="UTC"`).
   Cancelled events are ignored. A block that reaches the end of that window is followed for up to a year.
   All-day events are read as midnight to midnight in the mailbox time zone.
2. Events are sorted and merged when they overlap, touch, or the gap between them has no working time.
3. The block whose return is still in the future is used: the running absence, or else the next upcoming one.
4. **Return** = first working instant at or after the block end. If the mailbox has no working hours,
   Mon–Fri 08:00–17:00 in the mailbox time zone is assumed.

### The reply
`status = scheduled`, `scheduledStartDateTime` = block start, `scheduledEndDateTime` = return.
Placeholders (formatted in the mailbox time zone and the chosen culture, de-CH / fr-CH / en-GB):

| Placeholder | Meaning |
|---|---|
| `{StartDate}`, `{StartDateTime}` | Start of the absence |
| `{EndDate}` | Last day of the absence (an all-day event ending at midnight counts for the day before) |
| `{EndDateTime}` | End of the last event |
| `{ReturnDate}`, `{ReturnDateTime}` | First working time after the absence |

### Ownership: manual replies are never touched
After each write the app reads the setting back and stores a **fingerprint**: status, schedule (minute
precision), audience and the messages as normalised text, because Exchange rewrites the HTML. On every run:

| Current reply in the mailbox | Classification | What the app does |
|---|---|---|
| Disabled, or scheduled and already over | Free | Sets the reply if an absence is coming up |
| Equal to the app's last write | Owned | Updates it when the block or templates changed; disables it when the absence was deleted or the user opted out |
| Anything else (switched on by hand, schedule or text edited in Outlook) | Manual | Leaves it alone. The app takes over again once the user switches the reply off |

## One-time setup

### 1. App registration
Already done: **app-ext-aoooatray-p** (`6f328276-c32c-4b39-93f1-81f495ce5cc6`), set in
`src/SST.AutoOutOfOfficeAssistant.Tray/appsettings.json`. A public client ID is not a secret. To recreate it:

```powershell
$desktopAppId = az ad app create --display-name "app-ext-aoooatray-p" --sign-in-audience AzureADMultipleOrgs `
  --is-fallback-public-client true --query appId -o tsv
az ad app update --id $desktopAppId --public-client-redirect-uris `
  "ms-appx-web://microsoft.aad.brokerplugin/$desktopAppId" "http://localhost"
az ad sp create --id $desktopAppId | Out-Null

# Delegated Microsoft Graph permissions; IDs are looked up instead of hard-coded.
$graphScopes = (az ad sp show --id 00000003-0000-0000-c000-000000000000 --query oauth2PermissionScopes | ConvertFrom-Json)
$permissions = "User.Read", "Calendars.ReadBasic", "MailboxSettings.ReadWrite", "offline_access" | ForEach-Object {
    $name = $_; "$(($graphScopes | Where-Object value -eq $name).id)=Scope"
}
az ad app permission add --id $desktopAppId --api 00000003-0000-0000-c000-000000000000 --api-permissions $permissions

# Optional: consent for all users of your own tenant, so they see no consent prompt
az ad app permission admin-consent --id $desktopAppId
```

Required settings: supported account types **multiple organizations**, the two redirect URIs above under
*Mobile and desktop applications*, **Allow public client flows = Yes**, no client secret.

### 2. Build and distribute
1. The GitHub Actions workflow `.github/workflows/build.yml` builds and tests every push and pull request and
   uploads `AOOOA.exe` (self-contained, about 65 MB, no .NET install needed) and `appsettings.json` as a
   build artifact. Both files must stay in the same folder.
2. To publish a release, push a version tag: `git tag v1.0.0 && git push origin v1.0.0`. The workflow then
   creates a GitHub release with `AOOOA-1.0.0-win-x64.zip`.
3. **Sign** `AOOOA.exe` with a code-signing certificate before distributing it widely. Unsigned downloads
   trigger SmartScreen warnings.
4. Users unpack the zip anywhere, e.g. `%LOCALAPPDATA%\Programs\AOOOA`; no admin rights are needed. They
   enable "Mit Windows starten" in the tray menu themselves. Managed PCs can get it via Intune.

### Users from other organisations
On first sign-in, Entra asks for consent to read their calendar and to manage their mailbox settings. Many
tenants only allow user consent for apps from **verified publishers**. Consider
[publisher verification](https://learn.microsoft.com/entra/identity-platform/publisher-verification-overview)
for app-ext-aoooatray-p. If consent is blocked, the user's administrator can grant it with
`https://login.microsoftonline.com/<their-tenant>/adminconsent?client_id=6f328276-c32c-4b39-93f1-81f495ce5cc6`.

Requirements: Windows 10/11 x64 and an **Exchange Online** mailbox. Personal Microsoft accounts and
on-premises Exchange are not supported.

## Local development

```powershell
dotnet run --project src\SST.AutoOutOfOfficeAssistant.Tray
dotnet test --solution SST.AutoOutOfOfficeAssistant.slnx
```

Only one instance runs at a time; close the tray app before rebuilding, otherwise its DLLs are locked.

## Verification
1. Start the app, sign in, tick "Abwesenheitsnotiz automatisch aus meinem Kalender verwalten" and save.
2. Create an all-day "Out of office" event tomorrow and one on the following Monday. After "Jetzt prüfen" the
   menu shows one merged absence, and Outlook (File → Automatic Replies) shows a scheduled reply with the
   correct `{EndDate}` and `{ReturnDate}`.
3. Extend an event: the schedule updates at the next check.
4. Delete the events: the reply is switched off.
5. Switch on a reply by hand in Outlook: the status shows "Manuell verwaltet" and the reply stays unchanged.
6. If the status shows "Kein Zugriff", the calendar permission may be too narrow: change `Calendars.ReadBasic`
   to `Calendars.Read` in `MsalAuthenticator.cs` and in the app registration.

## Known limitations
- Calendar changes are picked up only while the PC is running (at the next check).
- When the app sets a reply, it overwrites the internal and external texts stored in the mailbox with the
  rendered templates.
- Only the user's own default calendar is read. Very short OOO events (for example a doctor's appointment
  shown as OOO) also trigger a reply.
