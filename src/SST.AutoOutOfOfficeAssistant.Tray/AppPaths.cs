namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>Where the app keeps its per-user files: <c>%LOCALAPPDATA%\SST\AOOOA</c>.</summary>
internal static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Gets the data directory (created on first use, taking over the files of older versions).</summary>
    public static string DataDirectory { get; } = Prepare(Path.Combine(LocalAppData, "SST", "AOOOA"));

    /// <summary>Gets the settings and sync state file.</summary>
    public static string DataFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Gets the log file.</summary>
    public static string LogFile => Path.Combine(DataDirectory, "aoooa.log");

    /// <summary>Gets the name of the MSAL token cache file (encrypted with DPAPI for the current Windows user).</summary>
    public const string TokenCacheFileName = "msal.cache";

    // Versions before the rename kept settings, sign-in and log in %LOCALAPPDATA%\FHNW\AOOOA: move them over once.
    private static string Prepare(string directory)
    {
        var legacy = Path.Combine(LocalAppData, "FHNW", "AOOOA");
        if (Directory.Exists(legacy) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
                Directory.Move(legacy, directory);
                var legacyParent = Path.GetDirectoryName(legacy)!;
                if (!Directory.EnumerateFileSystemEntries(legacyParent).Any())
                {
                    Directory.Delete(legacyParent);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Files in use (an old version still running): start with an empty folder instead.
            }
        }

        return Directory.CreateDirectory(directory).FullName;
    }
}

/// <summary>A minimal log file (rotated at 1 MB). Never logs message texts or tokens.</summary>
internal static class FileLog
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message, Exception exception) => Write("ERROR", $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var file = new FileInfo(AppPaths.LogFile);
                if (file.Exists && file.Length > 1_000_000)
                {
                    File.Move(file.FullName, file.FullName + ".1", overwrite: true);
                }

                File.AppendAllText(AppPaths.LogFile, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging must never break the app.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
