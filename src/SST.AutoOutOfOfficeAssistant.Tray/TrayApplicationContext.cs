using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Sync;
using SST.AutoOutOfOfficeAssistant.Graph;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>
/// The tray icon, its menu, notifications and the wiring of sign-in, store and sync.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly TrayOptions options;
    private readonly LocalStore store;
    private readonly NotifyIcon icon;
    private readonly ToolStripMenuItem statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem absenceItem = new() { Enabled = false, Visible = false };
    private readonly ToolStripMenuItem returnItem = new() { Enabled = false, Visible = false };
    private readonly ToolStripMenuItem checkNowItem = new(Texts.MenuCheckNow);
    private readonly ToolStripMenuItem accountItem = new(Texts.MenuSignIn);
    private readonly ToolStripMenuItem autostartItem = new(Texts.MenuAutostart) { CheckOnClick = true };

    // Owner window for the sign-in dialog while the settings window is closed.
    private readonly Form parkingWindow = new() { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Opacity = 0, Size = new Size(1, 1) };

    private MsalAuthenticator? authenticator;
    private SyncRunner? runner;
    private SettingsForm? settingsForm;
    private SyncOutcome? lastOutcome;
    private string? notifiedBlock;
    private bool signInNotified;

    public TrayApplicationContext(TrayOptions options, bool startMinimized)
    {
        this.options = options;
        this.store = new LocalStore(AppPaths.DataFile);
        this.notifiedBlock = BlockKey(this.store.Data.State);
        _ = this.parkingWindow.Handle;

        var menu = new ContextMenuStrip();
        menu.Items.Add(this.statusItem);
        menu.Items.Add(this.absenceItem);
        menu.Items.Add(this.returnItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(this.checkNowItem);
        menu.Items.Add(new ToolStripMenuItem(Texts.MenuSettings, null, (_, _) => this.ShowSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(this.accountItem);
        menu.Items.Add(this.autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Texts.MenuExit, null, (_, _) => this.ExitThread()));
        menu.Opening += (_, _) => this.UpdateUi(); // "next" becomes "current" without waiting for a sync

        this.checkNowItem.Click += async (_, _) => await this.CheckNowAsync();
        this.accountItem.Click += async (_, _) =>
        {
            if (this.IsSignedIn)
            {
                await this.SignOutAsync(null);
            }
            else
            {
                await this.SignInAsync();
            }
        };
        this.autostartItem.Checked = AutoStart.IsEnabled;
        this.autostartItem.CheckedChanged += (_, _) => AutoStart.Set(this.autostartItem.Checked);

        this.icon = new NotifyIcon { ContextMenuStrip = menu, Icon = StatusIcons.For(TrayStatus.NotRun), Text = "AOOOA", Visible = true };
        this.icon.DoubleClick += (_, _) => this.ShowSettings();
        this.icon.BalloonTipClicked += async (_, _) =>
        {
            if (this.Status == TrayStatus.SignInRequired && this.IsConfigured)
            {
                await this.SignInAsync();
            }
            else
            {
                this.ShowSettings();
            }
        };

        _ = this.InitializeAsync(startMinimized);
    }

    public bool IsConfigured => this.options.IsConfigured;

    public bool IsSignedIn => this.authenticator?.UserKey is not null;

    public string? UserName => this.authenticator?.UserName;

    public TrayStatus Status => TrayStatusText.From(this.store.Data, this.lastOutcome, this.IsConfigured, this.IsSignedIn);

    public string StatusText => TrayStatusText.Describe(this.Status, this.store.Data, this.lastOutcome);

    public async Task CheckNowAsync()
    {
        if (this.runner is not null)
        {
            await this.runner.RunNowAsync();
        }
    }

    public async Task SignInAsync()
    {
        if (this.authenticator is null)
        {
            return;
        }

        try
        {
            await this.authenticator.SignInInteractiveAsync(CancellationToken.None);
            this.RememberAccount();
            this.signInNotified = false;
            this.lastOutcome = null;
            this.UpdateUi();
            await this.CheckNowAsync();
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            // The user closed the dialog.
        }
        catch (MsalException ex)
        {
            FileLog.Error("Interactive sign-in failed", ex);
            MessageBox.Show(this.settingsForm, Texts.SignInFailed(ex.Message), "AOOOA", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public async Task SignOutAsync(IWin32Window? owner)
    {
        if (this.authenticator is null
            || MessageBox.Show(owner, Texts.SignOutConfirm, "AOOOA", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        await this.authenticator.SignOutAsync();
        this.store.Update(d =>
        {
            d.AccountId = null;
            d.UserKey = null;
            d.State = SyncState.Empty;
        });
        this.lastOutcome = null;
        this.UpdateUi();
    }

    public void ShowInfo(string title, string text) => this.icon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);

    protected override void ExitThreadCore()
    {
        this.runner?.Dispose();
        this.settingsForm?.Close();
        this.icon.Visible = false;
        this.icon.Dispose();
        this.parkingWindow.Dispose();
        StatusIcons.DisposeAll();
        base.ExitThreadCore();
    }

    private static string? BlockKey(SyncState state) =>
        state.LastResult == SyncResult.Scheduled && state.BlockStart is { } start && state.ReturnAt is { } back ? $"{start:O}|{back:O}" : null;

    private async Task InitializeAsync(bool startMinimized)
    {
        if (!this.options.IsConfigured)
        {
            this.UpdateUi();
            this.ShowSettings();
            return;
        }

        try
        {
            this.authenticator = await MsalAuthenticator.CreateAsync(
                this.options,
                () => this.settingsForm is { IsDisposed: false } form ? form.Handle : this.parkingWindow.Handle);

            var graph = new GraphServiceClient(new MsalTokenCredential(this.authenticator));
            var sync = new MailboxSyncService(new GraphMailboxGateway(graph), this.store, TimeProvider.System);
            this.runner = new SyncRunner(sync, this.store, this.options.SyncInterval);
            this.runner.Completed += this.OnSyncCompleted;

            var signedIn = await this.authenticator.TrySignInSilentlyAsync(this.store.Data.AccountId, CancellationToken.None);
            if (signedIn)
            {
                this.RememberAccount();
            }

            this.UpdateUi();
            this.runner.Start();

            if (!startMinimized && (!signedIn || !this.store.Data.Enabled))
            {
                this.ShowSettings();
            }
        }
#pragma warning disable CA1031 // Show any start-up failure instead of dying silently in the tray.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            FileLog.Error("Start-up failed", ex);
            this.lastOutcome = new SyncOutcome(this.store.Data.State, SignInRequired: false, Error: ex.Message);
            this.UpdateUi();
        }
    }

    private void RememberAccount()
    {
        var userKey = this.authenticator?.UserKey;
        var accountId = this.authenticator?.AccountId;
        this.store.Update(d =>
        {
            if (d.UserKey != userKey)
            {
                // Another mailbox: the fingerprints of the previous one do not apply.
                d.State = SyncState.Empty;
            }

            d.UserKey = userKey;
            d.AccountId = accountId;
        });
    }

    private void OnSyncCompleted(object? sender, SyncOutcome outcome)
    {
        this.lastOutcome = outcome;
        this.UpdateUi();

        if (outcome.SignInRequired)
        {
            if (!this.signInNotified && this.IsConfigured)
            {
                this.signInNotified = true;
                this.icon.ShowBalloonTip(8000, Texts.BalloonSignInTitle, Texts.BalloonSignIn, ToolTipIcon.Warning);
            }

            return;
        }

        this.signInNotified = false;
        var block = BlockKey(outcome.State);
        if (block is not null && block != this.notifiedBlock
            && outcome.State is { BlockStart: { } start, ReturnAt: { } back })
        {
            this.icon.ShowBalloonTip(8000, Texts.BalloonScheduledTitle, Texts.BalloonScheduled(TrayStatusText.Format(start), TrayStatusText.Format(back)), ToolTipIcon.Info);
        }

        this.notifiedBlock = block;
    }

    private void UpdateUi()
    {
        var status = this.Status;
        var text = this.StatusText;
        this.icon.Icon = StatusIcons.For(status);
        this.icon.Text = Truncate($"AOOOA – {text}", 127);
        this.statusItem.Text = text;
        var absence = TrayStatusText.AbsenceLines(status, this.store.Data.State, DateTimeOffset.Now);
        SetInfo(this.absenceItem, absence.Count > 0 ? absence[0] : null);
        SetInfo(this.returnItem, absence.Count > 1 ? absence[1] : null);
        this.checkNowItem.Enabled = this.IsSignedIn;
        this.accountItem.Enabled = this.IsConfigured && this.authenticator is not null;
        this.accountItem.Text = this.UserName is { } user && this.IsSignedIn ? Texts.MenuSignOut(user) : Texts.MenuSignIn;
        if (this.settingsForm is { IsDisposed: false } form)
        {
            form.RefreshStatus();
        }
    }

    private static void SetInfo(ToolStripMenuItem item, string? text)
    {
        item.Text = text ?? string.Empty;
        item.Visible = text is not null;
    }

    private void ShowSettings()
    {
        if (this.settingsForm is null || this.settingsForm.IsDisposed)
        {
            this.settingsForm = new SettingsForm(this.store, this);
            this.settingsForm.FormClosed += (_, _) => this.settingsForm = null;
        }

        this.settingsForm.Show();
        if (this.settingsForm.WindowState == FormWindowState.Minimized)
        {
            this.settingsForm.WindowState = FormWindowState.Normal;
        }

        this.settingsForm.Activate();
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
