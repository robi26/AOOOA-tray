using System.Globalization;
using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Contracts;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>
/// The settings window: account, status, templates (plain text) and a live preview.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly LocalStore store;
    private readonly TrayApplicationContext tray;

    private readonly Label accountLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 0) };
    private readonly Button accountButton = new() { AutoSize = true };
    private readonly Label statusLabel = new() { AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 6, 12, 6) };
    private readonly Button checkNowButton = new() { AutoSize = true, Text = Texts.ButtonCheckNow };
    private readonly CheckBox enabledBox = new() { AutoSize = true, Text = Texts.EnableLabel, MaximumSize = new Size(600, 0) };
    private readonly ComboBox audienceBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly ComboBox cultureBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly TextBox internalBox = CreateTextBox(readOnly: false);
    private readonly TextBox externalBox = CreateTextBox(readOnly: false);
    private readonly Label externalLabel = new() { AutoSize = true, Text = Texts.ExternalLabel, Margin = new Padding(0, 10, 0, 2) };
    private readonly Button copyButton = new() { AutoSize = true, Text = Texts.CopyInternal };
    private readonly Label previewCaption = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox previewBox = CreateTextBox(readOnly: true);

    public SettingsForm(LocalStore store, TrayApplicationContext tray)
    {
        this.store = store;
        this.tray = tray;

        this.Text = $"AOOOA – {Texts.AppName}";
        this.Icon = StatusIcons.App;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.Font = SystemFonts.MessageBoxFont;
        this.ClientSize = new Size(680, 820);
        this.MinimumSize = new Size(560, 600);

        this.audienceBox.Items.AddRange(
        [
            new Choice<ExternalAudience>(ExternalAudience.All, Texts.AudienceAll),
            new Choice<ExternalAudience>(ExternalAudience.ContactsOnly, Texts.AudienceContacts),
            new Choice<ExternalAudience>(ExternalAudience.None, Texts.AudienceNone),
        ]);
        this.cultureBox.Items.AddRange(SupportedCultures.All.Select(c => (object)new Choice<string>(c.Name, c.DisplayName)).ToArray());

        this.BuildLayout();
        this.LoadFromStore();
        this.RefreshStatus();

        this.accountButton.Click += async (_, _) =>
        {
            if (this.tray.IsSignedIn)
            {
                await this.tray.SignOutAsync(this);
            }
            else
            {
                await this.tray.SignInAsync();
            }
        };
        this.checkNowButton.Click += async (_, _) => await this.tray.CheckNowAsync();
        this.copyButton.Click += (_, _) => this.externalBox.Text = this.internalBox.Text;
        this.internalBox.TextChanged += (_, _) => this.UpdatePreview();
        this.externalBox.TextChanged += (_, _) => this.UpdatePreview();
        this.cultureBox.SelectedIndexChanged += (_, _) => this.UpdatePreview();
        this.audienceBox.SelectedIndexChanged += (_, _) => this.UpdateExternalVisibility();
    }

    /// <summary>Shows the current account and sync status.</summary>
    public void RefreshStatus()
    {
        this.accountLabel.Text = this.tray.UserName is { } user ? Texts.SignedInAs(user) : Texts.NotSignedIn;
        this.accountButton.Text = this.tray.IsSignedIn ? Texts.ButtonSignOut : Texts.ButtonSignIn;
        this.accountButton.Enabled = this.tray.IsConfigured;
        this.checkNowButton.Enabled = this.tray.IsSignedIn;

        var state = this.store.Data.State;
        var lines = new List<string> { this.tray.StatusText };
        if (this.tray.Status is TrayStatus.Scheduled or TrayStatus.Manual && TrayStatusText.BlockLine(state) is { } block)
        {
            lines.Add(block);
        }

        if (state.LastRunUtc is { } lastRun)
        {
            lines.Add(Texts.LastRun(TrayStatusText.Format(lastRun)));
        }

        this.statusLabel.Text = string.Join(Environment.NewLine, lines);
    }

    private static TextBox CreateTextBox(bool readOnly) => new()
    {
        Multiline = true,
        ReadOnly = readOnly,
        ScrollBars = ScrollBars.Vertical,
        AcceptsReturn = true,
        WordWrap = true,
        Height = 110,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 2, 0, 4),
    };

    private static Label FieldLabel(string text) => new()
    {
        AutoSize = false,
        Text = text,
        Size = new Size(210, 26),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 0, 8, 0),
    };

    private static GroupBox Group(string title, Control content)
    {
        content.Dock = DockStyle.Fill;
        var group = new GroupBox { Text = title, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 10) };
        group.Controls.Add(content);
        return group;
    }

    private static TableLayoutPanel Column() => new()
    {
        ColumnCount = 1,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) },
    };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.AddRange(controls);
        return row;
    }

    private void BuildLayout()
    {
        var account = Row(this.accountLabel, this.accountButton);
        var status = Row(this.statusLabel, this.checkNowButton);

        var settings = Column();
        settings.Controls.Add(this.enabledBox);
        settings.Controls.Add(Row(FieldLabel(Texts.AudienceLabel), this.audienceBox));
        settings.Controls.Add(Row(FieldLabel(Texts.CultureLabel), this.cultureBox));
        settings.Controls.Add(new Label { AutoSize = true, Text = Texts.InternalLabel, Margin = new Padding(0, 10, 0, 2) });
        settings.Controls.Add(this.internalBox);
        settings.Controls.Add(Row(this.externalLabel, this.copyButton));
        settings.Controls.Add(this.externalBox);
        settings.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(600, 0),
            ForeColor = SystemColors.GrayText,
            Text = Texts.PlaceholderHelp + Environment.NewLine + string.Join(
                Environment.NewLine,
                TemplateRenderer.Placeholders.Select(p => $"  {p.Placeholder}  –  {Texts.PlaceholderDescription(p.Placeholder, p.Description)}")),
        });

        var preview = Column();
        preview.Controls.Add(this.previewCaption);
        preview.Controls.Add(this.previewBox);

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        foreach (var group in new[]
        {
            Group(Texts.GroupAccount, account),
            Group(Texts.GroupStatus, status),
            Group(Texts.GroupSettings, settings),
            Group(Texts.GroupPreview, preview),
        })
        {
            group.Width = 630;
            content.Controls.Add(group);
        }

        var defaultsButton = new Button { AutoSize = true, Text = Texts.ButtonDefaults };
        var saveButton = new Button { AutoSize = true, Text = Texts.ButtonSave };
        var closeButton = new Button { AutoSize = true, Text = Texts.ButtonClose, DialogResult = DialogResult.Cancel };
        defaultsButton.Click += (_, _) => this.internalBox.Text = this.externalBox.Text = ForTextBox(PlainTextTemplate.Default);
        saveButton.Click += async (_, _) => await this.SaveAsync();
        closeButton.Click += (_, _) => this.Close();
        this.AcceptButton = saveButton;
        this.CancelButton = closeButton;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 12),
        };
        buttons.Controls.AddRange([closeButton, saveButton, defaultsButton]);

        this.Controls.Add(content);
        this.Controls.Add(buttons);
    }

    private void LoadFromStore()
    {
        var data = this.store.Data;
        this.enabledBox.Checked = data.Enabled;
        this.internalBox.Text = ForTextBox(data.InternalText);
        this.externalBox.Text = ForTextBox(data.ExternalText);
        Select(this.audienceBox, data.ExternalAudience);
        Select(this.cultureBox, data.Culture);
        this.UpdateExternalVisibility();
        this.UpdatePreview();
    }

    private async Task SaveAsync()
    {
        var audience = Selected<ExternalAudience>(this.audienceBox);
        var culture = Selected<string>(this.cultureBox) ?? SupportedCultures.Default;
        var errors = new SettingsDto
        {
            Enabled = this.enabledBox.Checked,
            InternalTemplate = PlainTextTemplate.ToHtml(this.internalBox.Text),
            ExternalTemplate = PlainTextTemplate.ToHtml(this.externalBox.Text),
            ExternalAudience = audience,
            Culture = culture,
        }.Validate();
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        this.store.Update(d =>
        {
            d.Enabled = this.enabledBox.Checked;
            d.InternalText = this.internalBox.Text;
            d.ExternalText = this.externalBox.Text;
            d.ExternalAudience = audience;
            d.Culture = culture;
        });

        this.tray.ShowInfo(Texts.ButtonSave, Texts.Saved);
        await this.tray.CheckNowAsync();
    }

    private void UpdateExternalVisibility()
    {
        var visible = Selected<ExternalAudience>(this.audienceBox) != ExternalAudience.None;
        this.externalLabel.Visible = this.copyButton.Visible = this.externalBox.Visible = visible;
        this.UpdatePreview();
    }

    private void UpdatePreview()
    {
        var block = SampleBlock();
        var culture = CultureInfo.GetCultureInfo(Selected<string>(this.cultureBox) ?? SupportedCultures.Default);
        string Render(string text) => ForTextBox(TemplateRenderer.Render(text, block, TimeZoneInfo.Local, culture));

        this.previewCaption.Text = Texts.PreviewCaption(
            block.Start.ToLocalTime().ToString("ddd dd.MM.", CultureInfo.CurrentCulture),
            block.End.AddTicks(-1).ToLocalTime().ToString("ddd dd.MM.yyyy", CultureInfo.CurrentCulture));
        var preview = Render(this.internalBox.Text);
        if (this.externalBox.Visible && this.externalBox.Text != this.internalBox.Text)
        {
            preview += Environment.NewLine + "────────" + Environment.NewLine + Render(this.externalBox.Text);
        }

        this.previewBox.Text = preview;
    }

    private static AbsenceBlock SampleBlock()
    {
        // Monday to Friday of next week, all day.
        var today = DateTime.Today;
        var daysToMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var monday = today.AddDays(daysToMonday == 0 ? 7 : daysToMonday);
        var events = Enumerable.Range(0, 5).Select(i => new TimeRange(new DateTimeOffset(monday.AddDays(i)), new DateTimeOffset(monday.AddDays(i + 1))));
        return AbsenceBlockCalculator.MergeBlocks(events, WorkingHours.Default(TimeZoneInfo.Local))[0];
    }

    private static void Select<T>(ComboBox box, T value)
    {
        var index = box.Items.Cast<Choice<T>>().ToList().FindIndex(c => EqualityComparer<T>.Default.Equals(c.Value, value));
        box.SelectedIndex = Math.Max(0, index);
    }

    private static T? Selected<T>(ComboBox box) =>
        box.SelectedIndex >= 0 && box.Items[box.SelectedIndex] is Choice<T> choice ? choice.Value : default;

    /// <summary>TextBox needs CRLF line breaks; templates may contain LF only.</summary>
    private static string ForTextBox(string text) => text.ReplaceLineEndings("\r\n");

    private sealed record Choice<T>(T Value, string Text)
    {
        public override string ToString() => this.Text;
    }
}
