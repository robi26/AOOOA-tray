using SST.AutoOutOfOfficeAssistant.Tray;

// One instance per Windows user.
using var singleInstance = new Mutex(initiallyOwned: true, @"Local\SST.AOOOA.Tray", out var isFirstInstance);
if (!isFirstInstance)
{
    return;
}

ApplicationConfiguration.Initialize();
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
Application.ThreadException += (_, e) => FileLog.Error("Unhandled UI exception", e.Exception);
AppDomain.CurrentDomain.UnhandledException += (_, e) => FileLog.Error("Unhandled exception", (Exception)e.ExceptionObject);

AutoStart.MigrateLegacy();

var options = TrayOptions.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
var startMinimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
FileLog.Info($"Started (configured: {options.IsConfigured}, minimized: {startMinimized}).");

Application.Run(new TrayApplicationContext(options, startMinimized));
