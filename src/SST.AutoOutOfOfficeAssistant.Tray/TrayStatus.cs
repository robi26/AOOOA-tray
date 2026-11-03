using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using SST.AutoOutOfOfficeAssistant.Core;
using Microsoft.Win32;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>What the tray icon shows.</summary>
internal enum TrayStatus
{
    NotConfigured,
    SignInRequired,
    Disabled,
    NotRun,
    Idle,
    Scheduled,
    Manual,
    NotInScope,
    Error,
}

/// <summary>Maps the sync state to a tray status, its text and its icon.</summary>
internal static class TrayStatusText
{
    public static TrayStatus From(TrayData data, SyncOutcome? outcome, bool configured, bool signedIn)
    {
        if (!configured)
        {
            return TrayStatus.NotConfigured;
        }

        if (!signedIn || outcome?.SignInRequired == true)
        {
            return TrayStatus.SignInRequired;
        }

        if (!data.Enabled)
        {
            return TrayStatus.Disabled;
        }

        if (outcome?.Error is not null)
        {
            return TrayStatus.Error;
        }

        return data.State.LastResult switch
        {
            SyncResult.Scheduled => TrayStatus.Scheduled,
            SyncResult.Idle => TrayStatus.Idle,
            SyncResult.SkippedManual => TrayStatus.Manual,
            SyncResult.NotInScope => TrayStatus.NotInScope,
            SyncResult.Error => TrayStatus.Error,
            _ => TrayStatus.NotRun,
        };
    }

    public static string Describe(TrayStatus status, TrayData data, SyncOutcome? outcome) => status switch
    {
        TrayStatus.NotConfigured => Texts.StatusNotConfigured,
        TrayStatus.SignInRequired => Texts.StatusSignInRequired,
        TrayStatus.Disabled => Texts.StatusDisabled,
        TrayStatus.Idle => Texts.StatusIdle,
        TrayStatus.Scheduled => Texts.StatusScheduled,
        TrayStatus.Manual => Texts.StatusManual,
        TrayStatus.NotInScope => Texts.StatusNotInScope,
        TrayStatus.Error => Texts.StatusError(outcome?.Error ?? data.State.LastError),
        _ => Texts.StatusNotRun,
    };

    /// <summary>The detected absence as one line, or null.</summary>
    public static string? BlockLine(SyncState state) =>
        state is { BlockStart: { } start, BlockEnd: { } end, ReturnAt: { } back }
            ? Texts.BlockLine(Format(start), Format(end.AddTicks(-1)), Format(back))
            : null;

    /// <summary>
    /// The menu lines about the absence: the next (or current) absence and until when the reply runs.
    /// Empty when the status says nothing about absences (not signed in, disabled, error).
    /// </summary>
    public static IReadOnlyList<string> AbsenceLines(TrayStatus status, SyncState state, DateTimeOffset now)
    {
        if (status is TrayStatus.Idle)
        {
            return [Texts.NoAbsence];
        }

        if (status is not (TrayStatus.Scheduled or TrayStatus.Manual)
            || state is not { BlockStart: { } start, BlockEnd: { } end, ReturnAt: { } back })
        {
            return [];
        }

        var first = start <= now
            ? Texts.CurrentAbsence(FormatEnd(end))
            : Texts.NextAbsence(start.ToLocalTime().Date == LastDay(end) ? FormatStart(start) : $"{FormatStart(start)} – {FormatEnd(end)}");
        return status is TrayStatus.Scheduled ? [first, Texts.ReplyUntil(FormatShort(back))] : [first];
    }

    // Midnight boundaries (all-day events) are shown as dates, other times with the time of day.
    private static string FormatStart(DateTimeOffset start) => FormatShort(start);

    private static string FormatEnd(DateTimeOffset end) =>
        end.ToLocalTime().TimeOfDay == TimeSpan.Zero
            ? LastDay(end).ToString("ddd dd.MM.", CultureInfo.CurrentCulture)
            : FormatShort(end);

    private static DateTime LastDay(DateTimeOffset end)
    {
        var local = end.ToLocalTime();
        return local.TimeOfDay == TimeSpan.Zero ? local.Date.AddDays(-1) : local.Date;
    }

    private static string FormatShort(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        return local.ToString(local.TimeOfDay == TimeSpan.Zero ? "ddd dd.MM." : "ddd dd.MM. HH:mm", CultureInfo.CurrentCulture);
    }

    public static string Format(DateTimeOffset value) =>
        value.ToLocalTime().ToString("ddd dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
}

/// <summary>Status icons drawn at runtime: a coloured disc with a white "A".</summary>
internal static class StatusIcons
{
    private static readonly Dictionary<Color, Icon> Cache = [];

    public static Icon For(TrayStatus status) => Get(status switch
    {
        TrayStatus.Scheduled => Color.FromArgb(0x2E, 0x7D, 0x32),
        TrayStatus.Idle => Color.FromArgb(0x15, 0x65, 0xC0),
        TrayStatus.Manual or TrayStatus.SignInRequired or TrayStatus.NotConfigured => Color.FromArgb(0xEF, 0x6C, 0x00),
        TrayStatus.Error or TrayStatus.NotInScope => Color.FromArgb(0xC6, 0x28, 0x28),
        _ => Color.FromArgb(0x75, 0x75, 0x75),
    });

    public static Icon App => Get(Color.FromArgb(0x15, 0x65, 0xC0));

    public static void DisposeAll()
    {
        foreach (var icon in Cache.Values)
        {
            DestroyIcon(icon.Handle);
            icon.Dispose();
        }

        Cache.Clear();
    }

    private static Icon Get(Color color)
    {
        if (Cache.TryGetValue(color, out var icon))
        {
            return icon;
        }

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("A", font, Brushes.White, new RectangleF(0, 1, 32, 32), format);
        }

        icon = Icon.FromHandle(bitmap.GetHicon());
        Cache[color] = icon;
        return icon;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}

/// <summary>"Start with Windows" through the current user's Run key.</summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SST.AOOOA";
    private const string LegacyValueName = "FHNW.AOOOA";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    /// <summary>Replaces the Run entry of versions before the rename, keeping the user's choice.</summary>
    public static void MigrateLegacy()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(LegacyValueName) is string)
        {
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            Set(enabled: true);
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
