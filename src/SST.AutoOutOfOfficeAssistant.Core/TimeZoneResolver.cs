namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// Resolves the time zone names Exchange returns (Windows IDs such as "W. Europe Standard Time",
/// IANA IDs, or "UTC") to a <see cref="TimeZoneInfo"/>. .NET resolves Windows and IANA IDs on all platforms.
/// </summary>
public static class TimeZoneResolver
{
    /// <summary>The time zone used when none of the candidates can be resolved.</summary>
    public const string FallbackId = "Europe/Zurich";

    /// <summary>
    /// Returns the first candidate that resolves to a known time zone, or <see cref="FallbackId"/>.
    /// </summary>
    /// <param name="candidates">Time zone names in order of preference. Null and empty entries are skipped.</param>
    /// <returns>The resolved time zone.</returns>
    public static TimeZoneInfo Resolve(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (TryResolve(candidate, out var tz))
            {
                return tz;
            }
        }

        return TryResolve(FallbackId, out var fallback) ? fallback : TimeZoneInfo.Utc;
    }

    private static bool TryResolve(string? name, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Equals("UTC", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("tzone://Microsoft/Utc", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(trimmed);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
