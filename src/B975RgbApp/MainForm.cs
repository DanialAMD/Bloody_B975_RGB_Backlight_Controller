using System.Drawing;
using System.Windows.Forms;
using B975RgbApp.Models;
using B975RgbApp.Services;

namespace B975RgbApp;

internal sealed class MainForm : Form
{
    private readonly LightingEngine _engine = new();
    private readonly KeyboardHook _keyboardHook = new();
    private readonly LightingSettings _settings;
    private readonly NotifyIcon _trayIcon;

    private readonly Label _statusLabel = new();
    private readonly Button _backgroundColorButton = new();
    private readonly Button _loadProfileButton = new();
    private readonly Button _editProfileButton = new();
    private readonly Button _clearProfileButton = new();
    private readonly Label _profileNameLabel = new();
    private readonly ComboBox _effectModeInput = new();
    private readonly Button _loadMeteorButton = new();
    private readonly Label _meteorProfileLabel = new();
    private readonly Button[] _effectColorButtons =
        [new(), new(), new(), new(), new(), new(), new(), new()];
    private readonly NumericUpDown _periodInput = new();
    private readonly NumericUpDown _minimumBrightnessInput = new();
    private readonly NumericUpDown _fadeInput = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();
    private readonly CheckBox _autoStartCheckBox = new();
    private readonly CheckBox _closeToTrayCheckBox = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();

    private Color _backgroundColor;
    private readonly Color[] _effectColors;
    private bool _allowExit;

    public MainForm()
    {
        _settings = SettingsStore.Load();
        _backgroundColor = LightingSettings.ParseColor(_settings.BackgroundHex);
        _effectColors = LoadEffectColors(_settings.EffectColors);

        Text = "Bloody B975 RGB Controller";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 660);
        Size = new Size(860, 720);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(244, 246, 249);
        Font = new Font("Segoe UI", 10F);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildInterface();
        ApplySettingsToControls();

        _keyboardHook.LedPressed += _engine.TriggerKey;
        _engine.Faulted += HandleEngineFault;

        _trayIcon = BuildTrayIcon();
        FormClosing += HandleFormClosing;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        };

        Shown += async (_, _) =>
        {
            FitInsideWorkingArea();

            if (Environment.GetCommandLineArgs().Any(x => x.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
            {
                BeginInvoke((Action)HideToTray);
            }

            if (_settings.AutoStartEffect)
            {
                await StartLightingAsync();
            }
        };
    }

    private void FitInsideWorkingArea()
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        var width = Math.Min(Width, workingArea.Width - 24);
        var height = Math.Min(Height, workingArea.Height - 24);
        Size = new Size(width, height);
        Location = new Point(
            workingArea.Left + (workingArea.Width - width) / 2,
            workingArea.Top + (workingArea.Height - height) / 2);
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(20),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = "کنترل نورپردازی Bloody B975",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 35, 45),
            AutoSize = true,
            Location = new Point(10, 4)
        });
        header.Controls.Add(new Label
        {
            Text = "Breathing پس‌زمینه + Flash/Fade، Meteor یا انفجار شعاعی",
            ForeColor = Color.FromArgb(95, 103, 115),
            AutoSize = true,
            Location = new Point(12, 40)
        });
        root.Controls.Add(header, 0, 0);

        var statusPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 10)
        };
        _statusLabel.Text = "متوقف — KeyDominator باید بسته باشد";
        _statusLabel.ForeColor = Color.FromArgb(110, 75, 15);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        statusPanel.Controls.Add(_statusLabel);
        root.Controls.Add(statusPanel, 0, 1);

        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 10,
            Margin = new Padding(0)
        };
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        for (var row = 0; row < 10; row++)
        {
            settingsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));
        }

        ConfigureColorButton(_backgroundColorButton, (_, _) => SelectBackgroundColor());
        AddSettingRow(settingsPanel, 0, "رنگ Breathing پس‌زمینه", _backgroundColorButton);

        var profileControls = BuildProfileControls();
        AddSettingRow(settingsPanel, 1, "رنگ‌بندی جداگانه کلیدها", profileControls);
        AddSettingRow(settingsPanel, 2, "نوع افکت واکنشی", BuildReactiveEffectControls());
        AddSettingRow(settingsPanel, 3, "رنگ‌های افکت واکنشی", BuildEffectColorControls());

        ConfigureNumber(_periodInput, 500, 10_000, 100, " ms");
        ConfigureNumber(_minimumBrightnessInput, 0, 90, 1, "%");
        ConfigureNumber(_fadeInput, 100, 3_000, 50, " ms");
        AddSettingRow(settingsPanel, 4, "زمان یک چرخه Breathing", _periodInput);
        AddSettingRow(settingsPanel, 5, "حداقل روشنایی", _minimumBrightnessInput);
        AddSettingRow(settingsPanel, 6, "مدت افکت واکنشی", _fadeInput);

        _startWithWindowsCheckBox.Text = "همراه ویندوز اجرا شود";
        _autoStartCheckBox.Text = "پس از اجرای برنامه افکت خودکار شروع شود";
        _closeToTrayCheckBox.Text = "با بستن پنجره کنار ساعت باقی بماند";
        ConfigureCheckBox(_startWithWindowsCheckBox);
        ConfigureCheckBox(_autoStartCheckBox);
        ConfigureCheckBox(_closeToTrayCheckBox);
        settingsPanel.Controls.Add(_startWithWindowsCheckBox, 0, 7);
        settingsPanel.SetColumnSpan(_startWithWindowsCheckBox, 2);
        settingsPanel.Controls.Add(_autoStartCheckBox, 0, 8);
        settingsPanel.SetColumnSpan(_autoStartCheckBox, 2);
        settingsPanel.Controls.Add(_closeToTrayCheckBox, 0, 9);
        settingsPanel.SetColumnSpan(_closeToTrayCheckBox, 2);
        root.Controls.Add(settingsPanel, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 9, 0, 0),
            WrapContents = false
        };
        ConfigureActionButton(_startButton, "شروع", Color.FromArgb(30, 155, 95));
        ConfigureActionButton(_stopButton, "توقف", Color.FromArgb(90, 100, 115));
        _startButton.Click += async (_, _) => await StartLightingAsync();
        _stopButton.Click += async (_, _) => await StopLightingAsync();
        actions.Controls.Add(_startButton);
        actions.Controls.Add(_stopButton);
        root.Controls.Add(actions, 0, 3);

        Controls.Add(root);
    }

    private Control BuildProfileControls()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));

        _profileNameLabel.Dock = DockStyle.Fill;
        _profileNameLabel.TextAlign = ContentAlignment.MiddleRight;
        _profileNameLabel.AutoEllipsis = true;
        _profileNameLabel.ForeColor = Color.FromArgb(90, 98, 110);

        ConfigureSmallButton(_loadProfileButton, "بارگذاری");
        ConfigureSmallButton(_editProfileButton, "طراحی");
        ConfigureSmallButton(_clearProfileButton, "حذف");
        _loadProfileButton.Click += (_, _) => LoadCkPannel();
        _editProfileButton.Click += (_, _) => OpenKeyboardEditor();
        _clearProfileButton.Click += (_, _) => ClearCkPannel();

        panel.Controls.Add(_profileNameLabel, 0, 0);
        panel.Controls.Add(_clearProfileButton, 1, 0);
        panel.Controls.Add(_editProfileButton, 2, 0);
        panel.Controls.Add(_loadProfileButton, 3, 0);
        return panel;
    }

    private Control BuildReactiveEffectControls()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));

        _effectModeInput.DropDownStyle = ComboBoxStyle.DropDownList;
        _effectModeInput.Dock = DockStyle.Fill;
        _effectModeInput.Margin = new Padding(3);
        _effectModeInput.Items.AddRange(["Flash / Fade", "Meteor ردیفی", "انفجار شعاعی"]);
        _effectModeInput.SelectedIndexChanged += (_, _) => UpdateEffectUiState();

        _meteorProfileLabel.Dock = DockStyle.Fill;
        _meteorProfileLabel.TextAlign = ContentAlignment.MiddleRight;
        _meteorProfileLabel.AutoEllipsis = true;
        _meteorProfileLabel.ForeColor = Color.FromArgb(90, 98, 110);

        ConfigureSmallButton(_loadMeteorButton, "بارگذاری ckButton");
        _loadMeteorButton.Click += (_, _) => LoadMeteorProfile();

        panel.Controls.Add(_meteorProfileLabel, 0, 0);
        panel.Controls.Add(_loadMeteorButton, 1, 0);
        panel.Controls.Add(_effectModeInput, 2, 0);
        return panel;
    }

    private Control BuildEffectColorControls()
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = _effectColorButtons.Length,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        for (var index = 0; index < _effectColorButtons.Length; index++)
        {
            var colorIndex = index;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / _effectColorButtons.Length));
            var button = _effectColorButtons[index];
            ConfigureColorButton(button, (_, _) => SelectEffectColor(colorIndex));
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(2, 4, 2, 4);
            button.Text = (index + 1).ToString();
            button.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            panel.Controls.Add(button, index, 0);
        }

        return panel;
    }

    private static void ConfigureSmallButton(Button button, string text)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(2);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(200, 207, 216);
        button.Cursor = Cursors.Hand;
        button.Font = new Font("Segoe UI", 8.5F);
    }

    private static void AddSettingRow(TableLayoutPanel panel, int row, string title, Control input)
    {
        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(45, 52, 62),
            Padding = new Padding(0, 0, 8, 0)
        };
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(4);
        panel.Controls.Add(label, 1, row);
        panel.Controls.Add(input, 0, row);
    }

    private static void ConfigureColorButton(Button button, EventHandler clickHandler)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 222);
        button.Cursor = Cursors.Hand;
        button.Click += clickHandler;
    }

    private static void ConfigureNumber(NumericUpDown input, int minimum, int maximum, int increment, string suffix)
    {
        input.Minimum = minimum;
        input.Maximum = maximum;
        input.Increment = increment;
        input.TextAlign = HorizontalAlignment.Center;
        input.ThousandsSeparator = true;
    }

    private static void ConfigureCheckBox(CheckBox checkBox)
    {
        checkBox.Dock = DockStyle.Fill;
        checkBox.TextAlign = ContentAlignment.MiddleRight;
        checkBox.Padding = new Padding(8, 0, 8, 0);
        checkBox.Cursor = Cursors.Hand;
    }

    private static void ConfigureActionButton(Button button, string text, Color color)
    {
        button.Text = text;
        button.Size = new Size(135, 40);
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        button.Margin = new Padding(10, 0, 0, 0);
    }

    private void ApplySettingsToControls()
    {
        _periodInput.Value = Math.Clamp(_settings.BreathingPeriodMilliseconds, 500, 10_000);
        _minimumBrightnessInput.Value = Math.Clamp(_settings.MinimumBrightnessPercent, 0, 90);
        _fadeInput.Value = Math.Clamp(_settings.FadeMilliseconds, 100, 3_000);
        _startWithWindowsCheckBox.Checked = _settings.StartWithWindows;
        _autoStartCheckBox.Checked = _settings.AutoStartEffect;
        _closeToTrayCheckBox.Checked = _settings.CloseToTray;
        _effectModeInput.SelectedIndex = _settings.ReactiveEffectMode switch
        {
            var mode when string.Equals(mode, "Meteor", StringComparison.OrdinalIgnoreCase) => 1,
            var mode when string.Equals(mode, "Explosion", StringComparison.OrdinalIgnoreCase) => 2,
            _ => 0
        };
        UpdateColorButton(_backgroundColorButton, _backgroundColor);
        UpdateEffectColorButtons();
        UpdateProfileLabel();
        UpdateEffectUiState();
        UpdateUiState();
    }

    private void SelectBackgroundColor()
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = _backgroundColor
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _backgroundColor = dialog.Color;
        _settings.BackgroundLedHex = null;
        _settings.BackgroundProfileName = null;
        UpdateColorButton(_backgroundColorButton, _backgroundColor);
        UpdateProfileLabel();
    }

    private void SelectEffectColor(int index)
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = _effectColors[index]
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _effectColors[index] = dialog.Color;
        UpdateEffectColorButtons();
    }

    private void LoadCkPannel()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "انتخاب پروفایل رنگ Bloody",
            Filter = "Bloody panel (*.ckPannel)|*.ckPannel|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var profile = CkPannelProfile.Load(dialog.FileName);
            _settings.BackgroundLedHex = profile.LedHexColors;
            _settings.BackgroundProfileName = profile.Name;
            UpdateProfileLabel();
            _statusLabel.Text = $"پروفایل {profile.Name} آماده است — برای اعمال، شروع را بزن";
            _statusLabel.ForeColor = Color.FromArgb(30, 95, 150);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ClearCkPannel()
    {
        _settings.BackgroundLedHex = null;
        _settings.BackgroundProfileName = null;
        UpdateProfileLabel();
    }

    private void OpenKeyboardEditor()
    {
        using var editor = new KeyboardLightingEditorForm(_settings.BackgroundLedHex, _backgroundColor);
        if (editor.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.BackgroundLedHex = editor.ResultColors;
        _settings.BackgroundProfileName = "طرح ساخته‌شده در برنامه";
        SettingsStore.Save(CaptureSettings());
        UpdateProfileLabel();
        _statusLabel.Text = "طرح جدید آماده است — برای دیدنش روی کیبورد، شروع را بزن";
        _statusLabel.ForeColor = Color.FromArgb(30, 95, 150);
    }

    private void LoadMeteorProfile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "انتخاب افکت Meteor نرم‌افزار Bloody",
            Filter = "Bloody button effect (*.ckButton)|*.ckButton|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var profile = CkButtonProfile.Load(dialog.FileName);
            _settings.MeteorColors = profile.DownColors;
            _settings.MeteorSpeed = profile.Speed;
            _settings.MeteorProfileName = profile.Name;
            _effectModeInput.SelectedIndex = 1;
            UpdateEffectUiState();
            _statusLabel.Text = $"افکت {profile.Name} آماده است — برای اعمال، شروع را بزن";
            _statusLabel.ForeColor = Color.FromArgb(30, 95, 150);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void UpdateProfileLabel()
    {
        var active = _settings.BackgroundLedHex is { Length: >= 116 };
        _profileNameLabel.Text = active
            ? _settings.BackgroundProfileName ?? "پروفایل سفارشی"
            : "رنگ یکپارچه";
        _clearProfileButton.Enabled = active && !_engine.IsRunning;
    }

    private void UpdateEffectUiState()
    {
        var meteor = _effectModeInput.SelectedIndex == 1;
        _meteorProfileLabel.Text = meteor
            ? _settings.MeteorProfileName ?? "Meteor استاندارد"
            : "—";
        _loadMeteorButton.Enabled = !_engine.IsRunning && meteor;
        foreach (var button in _effectColorButtons)
        {
            button.Enabled = !_engine.IsRunning && !meteor;
        }
        _fadeInput.Enabled = !_engine.IsRunning && !meteor;
    }

    private static void UpdateColorButton(Button button, Color color)
    {
        button.BackColor = color;
        button.ForeColor = color.GetBrightness() > 0.55F ? Color.Black : Color.White;
        button.Text = $"#{LightingSettings.ToHex(color)}";
    }

    private void UpdateEffectColorButtons()
    {
        for (var index = 0; index < _effectColorButtons.Length; index++)
        {
            var button = _effectColorButtons[index];
            var color = _effectColors[index];
            button.BackColor = color;
            button.ForeColor = color.GetBrightness() > 0.55F ? Color.Black : Color.White;
            button.Text = (index + 1).ToString();
        }
    }

    private static Color[] LoadEffectColors(string[]? values)
    {
        var defaults = LightingSettings.CreateDefaultEffectColors();
        var source = values is { Length: > 0 } ? values : defaults;
        return Enumerable.Range(0, 8)
            .Select(index => LightingSettings.ParseColor(
                index < source.Length ? source[index] : defaults[index]))
            .ToArray();
    }

    private LightingSettings CaptureSettings()
    {
        _settings.BackgroundHex = LightingSettings.ToHex(_backgroundColor);
        _settings.EffectHex = LightingSettings.ToHex(_effectColors[0]);
        _settings.EffectColors = _effectColors.Select(LightingSettings.ToHex).ToArray();
        _settings.ReactiveEffectMode = _effectModeInput.SelectedIndex switch
        {
            1 => "Meteor",
            2 => "Explosion",
            _ => "FlashFade"
        };
        _settings.BreathingPeriodMilliseconds = (int)_periodInput.Value;
        _settings.MinimumBrightnessPercent = (int)_minimumBrightnessInput.Value;
        _settings.FadeMilliseconds = (int)_fadeInput.Value;
        _settings.StartWithWindows = _startWithWindowsCheckBox.Checked;
        _settings.AutoStartEffect = _autoStartCheckBox.Checked;
        _settings.CloseToTray = _closeToTrayCheckBox.Checked;
        return _settings.Clone();
    }

    private async Task StartLightingAsync()
    {
        if (_engine.IsRunning)
        {
            return;
        }

        try
        {
            var settings = CaptureSettings();
            SettingsStore.Save(settings);
            StartupManager.SetEnabled(settings.StartWithWindows);

            _keyboardHook.Start();
            _engine.Start(settings);
            _statusLabel.Text = settings.ReactiveEffectMode switch
            {
                "Meteor" => "فعال — Breathing و Meteor ردیفی در حال اجراست",
                "Explosion" => "فعال — Breathing و انفجار شعاعی در حال اجراست",
                _ => "فعال — Breathing و Flash/Fade در حال اجراست"
            };
            _statusLabel.ForeColor = Color.FromArgb(20, 125, 75);
            UpdateUiState();
        }
        catch (Exception exception)
        {
            _keyboardHook.Stop();
            await _engine.StopAsync();
            ShowError(exception.Message);
            _statusLabel.Text = "خطا — ارتباط با کیبورد برقرار نشد";
            _statusLabel.ForeColor = Color.FromArgb(180, 45, 45);
            UpdateUiState();
        }
    }

    private async Task StopLightingAsync()
    {
        _keyboardHook.Stop();
        await _engine.StopAsync();
        _statusLabel.Text = "متوقف — نور روی رنگ پس‌زمینه ثابت شد";
        _statusLabel.ForeColor = Color.FromArgb(110, 75, 15);
        UpdateUiState();
    }

    private void HandleEngineFault(Exception exception)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke((Action)(() =>
        {
            _keyboardHook.Stop();
            _statusLabel.Text = "خطا در ارسال فریم‌های نورپردازی";
            _statusLabel.ForeColor = Color.FromArgb(180, 45, 45);
            UpdateUiState();
            ShowError(exception.Message);
        }));
    }

    private void UpdateUiState()
    {
        var running = _engine.IsRunning;
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _backgroundColorButton.Enabled = !running;
        _loadProfileButton.Enabled = !running;
        _editProfileButton.Enabled = !running;
        _clearProfileButton.Enabled = !running && _settings.BackgroundLedHex is { Length: >= 116 };
        _effectModeInput.Enabled = !running;
        _loadMeteorButton.Enabled = !running && _effectModeInput.SelectedIndex == 1;
        foreach (var button in _effectColorButtons)
        {
            button.Enabled = !running && _effectModeInput.SelectedIndex != 1;
        }
        _periodInput.Enabled = !running;
        _minimumBrightnessInput.Enabled = !running;
        _fadeInput.Enabled = !running && _effectModeInput.SelectedIndex != 1;
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        var openItem = new ToolStripMenuItem("بازکردن برنامه");
        var startItem = new ToolStripMenuItem("شروع افکت");
        var stopItem = new ToolStripMenuItem("توقف افکت");
        var exitItem = new ToolStripMenuItem("خروج کامل");

        openItem.Click += (_, _) => RestoreFromTray();
        startItem.Click += async (_, _) => await StartLightingAsync();
        stopItem.Click += async (_, _) => await StopLightingAsync();
        exitItem.Click += async (_, _) => await ExitApplicationAsync();
        menu.Items.AddRange([openItem, startItem, stopItem, new ToolStripSeparator(), exitItem]);

        var icon = new NotifyIcon
        {
            Text = "Bloody B975 RGB Controller",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => RestoreFromTray();
        return icon;
    }

    private void HideToTray()
    {
        Hide();
        WindowState = FormWindowState.Normal;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private async Task ExitApplicationAsync()
    {
        _allowExit = true;
        await StopLightingAsync();
        Close();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        CaptureSettings();
        SettingsStore.Save(_settings);
        StartupManager.SetEnabled(_settings.StartWithWindows);

        if (!_allowExit && _settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _keyboardHook.Dispose();
        _engine.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private void ShowError(string message)
    {
        MessageBox.Show(
            this,
            message,
            "Bloody B975 RGB",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }
}
