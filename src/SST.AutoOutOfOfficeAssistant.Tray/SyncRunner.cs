using System.Net.NetworkInformation;
using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Sync;
using Microsoft.Win32;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>The result of one sync, as the UI needs it.</summary>
/// <param name="State">The sync state after the run.</param>
/// <param name="SignInRequired">True when the user must sign in again.</param>
/// <param name="Error">An unexpected error, or null.</param>
internal sealed record SyncOutcome(SyncState State, bool SignInRequired, string? Error);

/// <summary>
/// Runs the sync every few minutes, after the PC wakes up and when the network comes back. Runs never
/// overlap; a request during a run starts one more run afterwards. <see cref="Completed"/> is raised on the UI thread.
/// </summary>
internal sealed class SyncRunner : IDisposable
{
    private readonly MailboxSyncService sync;
    private readonly LocalStore store;
    private readonly TimeSpan interval;
    private readonly SynchronizationContext ui;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stopping = new();
    private volatile bool rerunRequested;

    public SyncRunner(MailboxSyncService sync, LocalStore store, TimeSpan interval)
    {
        this.sync = sync;
        this.store = store;
        this.interval = interval;
        this.ui = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    /// <summary>Raised on the UI thread after each run.</summary>
    public event EventHandler<SyncOutcome>? Completed;

    /// <summary>Starts the periodic runs (the first one immediately).</summary>
    public void Start()
    {
        SystemEvents.PowerModeChanged += this.OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged += this.OnNetworkAvailabilityChanged;
        _ = this.LoopAsync();
    }

    /// <summary>Runs a sync now, or right after the one in progress.</summary>
    /// <returns>A task.</returns>
    public async Task RunNowAsync()
    {
        if (!await this.gate.WaitAsync(0).ConfigureAwait(true))
        {
            this.rerunRequested = true;
            return;
        }

        try
        {
            do
            {
                this.rerunRequested = false;
                var outcome = await this.RunOnceAsync().ConfigureAwait(true);
                if (outcome is not null)
                {
                    this.ui.Post(_ => this.Completed?.Invoke(this, outcome), null);
                }
            }
            while (this.rerunRequested && !this.stopping.IsCancellationRequested);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= this.OnPowerModeChanged;
        NetworkChange.NetworkAvailabilityChanged -= this.OnNetworkAvailabilityChanged;
        this.stopping.Cancel();
        this.stopping.Dispose();
        this.gate.Dispose();
    }

    private async Task<SyncOutcome?> RunOnceAsync()
    {
        var userKey = this.store.Data.UserKey;
        if (userKey is null)
        {
            return new SyncOutcome(this.store.Data.State, SignInRequired: true, Error: null);
        }

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return null;
        }

        try
        {
            var state = await Task.Run(() => this.sync.SyncAsync(userKey, this.stopping.Token)).ConfigureAwait(true);
            return new SyncOutcome(state, SignInRequired: false, Error: null);
        }
        catch (OperationCanceledException) when (this.stopping.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (SignInRequiredException.IsCause(ex))
        {
            return new SyncOutcome(this.store.Data.State, SignInRequired: true, Error: null);
        }
#pragma warning disable CA1031 // Any failure is shown in the tray and retried on the next run.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            FileLog.Error("Sync failed", ex);
            return new SyncOutcome(this.store.Data.State, SignInRequired: false, Error: ex.Message);
        }
    }

    private async Task LoopAsync()
    {
        try
        {
            await this.RunNowAsync().ConfigureAwait(true);
            using var timer = new PeriodicTimer(this.interval);
            while (await timer.WaitForNextTickAsync(this.stopping.Token).ConfigureAwait(true))
            {
                await this.RunNowAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
        catch (ObjectDisposedException)
        {
            // Stopped.
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            // Give the network a moment after wake-up.
            this.RunSoon(TimeSpan.FromSeconds(30));
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            this.RunSoon(TimeSpan.FromSeconds(5));
        }
    }

    private void RunSoon(TimeSpan delay) => this.ui.Post(
        async _ =>
        {
            try
            {
                await Task.Delay(delay, this.stopping.Token).ConfigureAwait(true);
                await this.RunNowAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // The app is closing.
            }
            catch (ObjectDisposedException)
            {
                // The app is closing.
            }
        },
        null);
}
