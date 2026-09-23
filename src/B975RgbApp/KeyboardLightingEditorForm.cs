using B975RgbApp.Models;

namespace B975RgbApp;

internal sealed class KeyboardLightingEditorForm : Form
{
    private const int LedCount = 116;

    private readonly KeyboardLayoutControl _keyboard = new();
    private readonly Label _selectionLabel = new();
    private readonly Button _solidColorButton = new();
    private readonly Button[] _gradientColorButtons =
        [new(), new(), new(), new(), new(), new(), new(), new()];
    private readonly NumericUpDown _gradientStopCountInput = new();
    private readonly NumericUpDown _angleInput = new();
    private readonly Button _undoButton = new();
    private readonly Button _redoButton = new();
    private readonly ToolTip _toolTip = new();
    private readonly Stack<string[]> _undo = new();
    private readonly Stack<string[]> _redo = new();
    private readonly string[] _originalColors;

    private Color _solidColor = Color.FromArgb(145, 55, 255);
    private readonly Color[] _gradientColors =
    [
        Color.FromArgb(0, 210, 255),
        Color.FromArgb(0, 125, 255),
        Color.FromArgb(80, 85, 255),
        Color.FromArgb(160, 60, 255),
        Color.FromArgb(220, 35, 220),
        Color.FromArgb(255, 40, 130),
        Color.FromArgb(255, 115, 35),
        Color.FromArgb(255, 215, 0)
    ];

    public KeyboardLightingEditorForm(string[]? colors, Color fallbackColor)
    {
        _originalColors = NormalizeColors(colors, fallbackColor);
        ResultColors = _originalColors.ToArray();

        Text = "طراحی رنگ پیش‌فرض Bloody B975";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 650);
        Size = new Size(1180, 740);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(241, 244, 248);
        Font = new Font("Segoe UI", 9.5F);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        BuildInterface();
        _keyboard.SetColors(_originalColors);
        _keyboard.SelectionChanged += (_, _) => UpdateSelectionLabel();
        Shown += (_, _) => FitInsideWorkingArea();
        UpdateColorButtons();
        UpdateSelectionLabel();
        UpdateHistoryButtons();
    }

    public string[] ResultColors { get; private set; }

    private void FitInsideWorkingArea()
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        var width = Math.Min(Width, workingArea.Width - 32);
        var height = Math.Min(Height, workingArea.Height - 32);
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
            Padding = new Padding(18),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = "طراحی رنگ پیش‌فرض کلیدها",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 35, 45),
            AutoSize = true,
            Location = new Point(8, 0)
        });
        header.Controls.Add(new Label
        {
            Text = "روی کلید کلیک کن؛ برای چندانتخاب Ctrl را نگه دار یا با ماوس کادر بکش.",
            ForeColor = Color.FromArgb(90, 98, 110),
            AutoSize = true,
            Location = new Point(10, 32)
        });
        root.Controls.Add(header, 0, 0);

        _keyboard.Dock = DockStyle.Fill;
        _keyboard.Margin = new Padding(0, 4, 0, 10);
        root.Controls.Add(_keyboard, 0, 1);

        var tools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(7),
            ColumnCount = 3,
            RowCount = 1
        };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        tools.Controls.Add(BuildSelectionTools(), 2, 0);
        tools.Controls.Add(BuildGradientTools(), 1, 0);
        tools.Controls.Add(BuildSolidTools(), 0, 0);
        root.Controls.Add(tools, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var saveButton = MakeActionButton("اعمال و بستن", Color.FromArgb(35, 150, 95));
        var cancelButton = MakeActionButton("انصراف", Color.FromArgb(100, 108, 120));
        saveButton.Click += (_, _) =>
        {
            ResultColors = _keyboard.GetColors();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        footer.Controls.Add(saveButton);
        footer.Controls.Add(cancelButton);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private Control BuildSelectionTools()
    {
        var box = MakeGroup("انتخاب و تاریخچه");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(3, 4, 3, 2)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        _selectionLabel.TextAlign = ContentAlignment.MiddleCenter;
        _selectionLabel.Dock = DockStyle.Fill;
        _selectionLabel.ForeColor = Color.FromArgb(60, 68, 80);

        var selectionButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        selectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        selectionButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var selectAll = MakeSmallButton("انتخاب همه");
        var clear = MakeSmallButton("لغو انتخاب");
        selectAll.Dock = DockStyle.Fill;
        clear.Dock = DockStyle.Fill;
        selectAll.Click += (_, _) => _keyboard.SelectAllKeys();
        clear.Click += (_, _) => _keyboard.ClearSelection();
        selectionButtons.Controls.Add(selectAll, 0, 0);
        selectionButtons.Controls.Add(clear, 1, 0);

        var historyButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        historyButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        historyButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        historyButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        ConfigureSmallButton(_undoButton, "Undo");
        ConfigureSmallButton(_redoButton, "Redo");
        var reset = MakeSmallButton("بازنشانی");
        _undoButton.Dock = DockStyle.Fill;
        _redoButton.Dock = DockStyle.Fill;
        reset.Dock = DockStyle.Fill;
        _undoButton.Click += (_, _) => Undo();
        _redoButton.Click += (_, _) => Redo();
        reset.Click += (_, _) =>
        {
            PushUndo();
            _keyboard.SetColors(_originalColors);
            _redo.Clear();
            UpdateHistoryButtons();
        };
        historyButtons.Controls.Add(_undoButton, 0, 0);
        historyButtons.Controls.Add(_redoButton, 1, 0);
        historyButtons.Controls.Add(reset, 2, 0);

        panel.Controls.Add(_selectionLabel, 0, 0);
        panel.Controls.Add(selectionButtons, 0, 1);
        panel.Controls.Add(historyButtons, 0, 2);
        box.Controls.Add(panel);
        return box;
    }

    private Control BuildSolidTools()
    {
        var box = MakeGroup("رنگ ثابت برای کلیدهای انتخاب‌شده");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6, 10, 6, 3)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        ConfigureColorButton(_solidColorButton, () => PickColor(ref _solidColor, _solidColorButton));
        var apply = MakeSmallButton("اعمال رنگ");
        _solidColorButton.Dock = DockStyle.Fill;
        apply.Dock = DockStyle.Fill;
        apply.Click += (_, _) => ApplyChange(() => _keyboard.ApplySolid(_solidColor));
        actions.Controls.Add(apply, 0, 0);
        actions.Controls.Add(_solidColorButton, 1, 0);
        panel.Controls.Add(actions, 0, 0);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "اگر هیچ کلیدی انتخاب نباشد روی همه اعمال می‌شود.",
            ForeColor = Color.FromArgb(100, 108, 120),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(2)
        }, 0, 1);
        box.Controls.Add(panel);
        return box;
    }

    private Control BuildGradientTools()
    {
        var box = MakeGroup("گرادیان آزاد");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(5, 7, 5, 3)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 28));

        var colorPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0)
        };
        for (var column = 0; column < 4; column++)
        {
            colorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        colorPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        colorPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        for (var index = 0; index < _gradientColorButtons.Length; index++)
        {
            var stopIndex = index;
            var button = _gradientColorButtons[index];
            ConfigureColorButton(button, () => PickGradientColor(stopIndex));
            button.Dock = DockStyle.Fill;
            button.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            button.Margin = new Padding(3);
            colorPanel.Controls.Add(button, index % 4, index / 4);
        }
        _gradientStopCountInput.Minimum = 2;
        _gradientStopCountInput.Maximum = 8;
        _gradientStopCountInput.Value = 8;
        _gradientStopCountInput.Dock = DockStyle.Fill;
        _gradientStopCountInput.TextAlign = HorizontalAlignment.Center;
        _gradientStopCountInput.ValueChanged += (_, _) => UpdateGradientColorButtons();
        panel.Controls.Add(colorPanel, 0, 0);

        var anglePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            Margin = new Padding(0)
        };
        anglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        anglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        anglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
        anglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        for (var index = 0; index < 4; index++)
        {
            anglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        }
        _angleInput.Minimum = -180;
        _angleInput.Maximum = 180;
        _angleInput.Value = 45;
        _angleInput.Dock = DockStyle.Fill;
        _angleInput.TextAlign = HorizontalAlignment.Center;
        anglePanel.Controls.Add(new Label
        {
            Text = "تعداد رنگ",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);
        anglePanel.Controls.Add(_gradientStopCountInput, 1, 0);
        anglePanel.Controls.Add(new Label
        {
            Text = "زاویه",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        }, 2, 0);
        anglePanel.Controls.Add(_angleInput, 3, 0);
        var angles = new[] { 0, 45, 90, -45 };
        for (var index = 0; index < angles.Length; index++)
        {
            var angle = angles[index];
            var shortcut = MakeSmallButton($"{angle}°");
            shortcut.Dock = DockStyle.Fill;
            shortcut.Click += (_, _) => _angleInput.Value = angle;
            anglePanel.Controls.Add(shortcut, index + 4, 0);
        }
        panel.Controls.Add(anglePanel, 0, 1);

        var apply = MakeSmallButton("اعمال گرادیان");
        apply.Dock = DockStyle.Fill;
        apply.Click += (_, _) => ApplyChange(() =>
        {
            var stops = _gradientColors
                .Take((int)_gradientStopCountInput.Value)
                .ToArray();
            _keyboard.ApplyGradient(stops, (float)_angleInput.Value);
        });
        panel.Controls.Add(apply, 0, 2);

        box.Controls.Add(panel);
        return box;
    }

    private void ApplyChange(Action change)
    {
        PushUndo();
        change();
        _redo.Clear();
        UpdateHistoryButtons();
    }

    private void PushUndo()
    {
        _undo.Push(_keyboard.GetColors());
    }

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Push(_keyboard.GetColors());
        _keyboard.SetColors(_undo.Pop());
        UpdateHistoryButtons();
    }

    private void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Push(_keyboard.GetColors());
        _keyboard.SetColors(_redo.Pop());
        UpdateHistoryButtons();
    }

    private void UpdateSelectionLabel()
    {
        var count = _keyboard.SelectedIndices.Count;
        _selectionLabel.Text = count == 0
            ? "بدون انتخاب — عملیات روی همه کلیدها"
            : $"{count} کلید انتخاب شده";
    }

    private void UpdateHistoryButtons()
    {
        _undoButton.Enabled = _undo.Count > 0;
        _redoButton.Enabled = _redo.Count > 0;
    }

    private void PickColor(ref Color target, Button button)
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = target };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        target = dialog.Color;
        UpdateColorButton(button, target);
    }

    private void PickGradientColor(int index)
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = _gradientColors[index]
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _gradientColors[index] = dialog.Color;
        UpdateGradientColorButtons();
    }

    private void UpdateColorButtons()
    {
        UpdateColorButton(_solidColorButton, _solidColor);
        UpdateGradientColorButtons();
    }

    private void UpdateGradientColorButtons()
    {
        var activeCount = (int)_gradientStopCountInput.Value;
        for (var index = 0; index < _gradientColorButtons.Length; index++)
        {
            var button = _gradientColorButtons[index];
            var color = _gradientColors[index];
            UpdateColorButton(button, color);
            button.Text = (index + 1).ToString();
            button.Enabled = index < activeCount;
            _toolTip.SetToolTip(
                button,
                index < activeCount
                    ? $"رنگ {index + 1}: #{LightingSettings.ToHex(color)}"
                    : $"رنگ {index + 1} غیرفعال است");
        }
    }

    private static string[] NormalizeColors(string[]? colors, Color fallback)
    {
        var result = Enumerable.Repeat(LightingSettings.ToHex(fallback), LedCount).ToArray();
        if (colors is null)
        {
            return result;
        }

        for (var index = 0; index < Math.Min(colors.Length, LedCount); index++)
        {
            result[index] = LightingSettings.ToHex(LightingSettings.ParseColor(colors[index]));
        }

        return result;
    }

    private static GroupBox MakeGroup(string title) => new()
    {
        Text = title,
        Dock = DockStyle.Fill,
        Margin = new Padding(4),
        Padding = new Padding(5),
        ForeColor = Color.FromArgb(48, 55, 65)
    };

    private static Button MakeSmallButton(string text)
    {
        var button = new Button();
        ConfigureSmallButton(button, text);
        return button;
    }

    private static void ConfigureSmallButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = false;
        button.Size = new Size(78, 28);
        button.Margin = new Padding(2);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(194, 201, 211);
        button.BackColor = Color.FromArgb(248, 249, 251);
        button.Cursor = Cursors.Hand;
    }

    private static void ConfigureColorButton(Button button, Action clickAction)
    {
        button.Size = new Size(82, 29);
        button.Margin = new Padding(2);
        button.FlatStyle = FlatStyle.Flat;
        button.Cursor = Cursors.Hand;
        button.Click += (_, _) => clickAction();
    }

    private static void UpdateColorButton(Button button, Color color)
    {
        button.BackColor = color;
        button.ForeColor = color.GetBrightness() > 0.55F ? Color.Black : Color.White;
        button.Text = $"#{LightingSettings.ToHex(color)}";
    }

    private static Button MakeActionButton(string text, Color color) => new()
    {
        Text = text,
        Size = new Size(145, 37),
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Cursor = Cursors.Hand,
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        Margin = new Padding(8, 0, 0, 0)
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
