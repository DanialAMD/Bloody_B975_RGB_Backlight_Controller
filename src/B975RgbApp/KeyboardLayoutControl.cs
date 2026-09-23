using System.Drawing.Drawing2D;
using B975RgbApp.Models;

namespace B975RgbApp;

internal sealed class KeyboardLayoutControl : Control
{
    private const float LayoutWidth = 24F;
    private const float LayoutHeight = 6.4F;
    private const int LedCount = 116;

    private static readonly KeyboardKey[] KeyLayout = CreateKeys();
    private readonly HashSet<int> _selected = [];
    private readonly HashSet<int> _selectionBeforeDrag = [];
    private string[] _colors = Enumerable.Repeat("000000", LedCount).ToArray();
    private Point _dragStart;
    private Rectangle _selectionRectangle;
    private bool _dragging;
    private bool _dragAddsToSelection;

    public KeyboardLayoutControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(29, 32, 38);
        ForeColor = Color.White;
        MinimumSize = new Size(800, 280);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyCollection<int> SelectedIndices => _selected;

    public string[] GetColors() => _colors.ToArray();

    public void SetColors(IEnumerable<string> colors)
    {
        var source = colors?.ToArray() ?? [];
        var next = Enumerable.Repeat("000000", LedCount).ToArray();
        for (var index = 0; index < Math.Min(source.Length, LedCount); index++)
        {
            next[index] = LightingSettings.ToHex(LightingSettings.ParseColor(source[index]));
        }

        _colors = next;
        Invalidate();
    }

    public void SelectAllKeys()
    {
        _selected.Clear();
        foreach (var key in KeyLayout)
        {
            _selected.Add(key.LedIndex);
        }

        OnSelectionChanged();
    }

    public void ClearSelection()
    {
        if (_selected.Count == 0)
        {
            return;
        }

        _selected.Clear();
        OnSelectionChanged();
    }

    public void ApplySolid(Color color)
    {
        foreach (var index in TargetIndices())
        {
            _colors[index] = LightingSettings.ToHex(color);
        }

        Invalidate();
    }

    public void ApplyGradient(IReadOnlyList<Color> stops, float angleDegrees)
    {
        if (stops.Count < 2)
        {
            return;
        }

        var targets = TargetIndices().ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var angleRadians = angleDegrees * Math.PI / 180.0;
        var axisX = Math.Cos(angleRadians);
        var axisY = Math.Sin(angleRadians);
        var projections = targets.ToDictionary(
            index => index,
            index =>
            {
                var key = KeyLayout.First(item => item.LedIndex == index);
                return (key.X + key.Width / 2F) * axisX + (key.Y + key.Height / 2F) * axisY;
            });
        var minimum = projections.Values.Min();
        var maximum = projections.Values.Max();
        var range = Math.Max(0.0001, maximum - minimum);

        foreach (var index in targets)
        {
            var position = (projections[index] - minimum) / range;
            _colors[index] = LightingSettings.ToHex(InterpolateStops(stops, position));
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        e.Graphics.Clear(BackColor);

        using var borderPen = new Pen(Color.FromArgb(92, 99, 112), 1F);
        using var selectedPen = new Pen(Color.White, 2.4F);
        using var textBrush = new SolidBrush(Color.White);
        using var darkTextBrush = new SolidBrush(Color.FromArgb(22, 24, 28));
        using var font = new Font("Segoe UI", 8F, FontStyle.Bold);

        foreach (var key in KeyLayout)
        {
            var rectangle = ToScreen(key);
            var color = LightingSettings.ParseColor(_colors[key.LedIndex]);
            using var fillBrush = new SolidBrush(color);
            e.Graphics.FillRoundedRectangle(fillBrush, rectangle, 5F);
            e.Graphics.DrawRoundedRectangle(
                _selected.Contains(key.LedIndex) ? selectedPen : borderPen,
                rectangle,
                5F);

            var brush = color.GetBrightness() > 0.58F ? darkTextBrush : textBrush;
            TextRenderer.DrawText(
                e.Graphics,
                key.Label,
                font,
                Rectangle.Round(rectangle),
                ((SolidBrush)brush).Color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);
        }

        if (_dragging && _selectionRectangle.Width > 2 && _selectionRectangle.Height > 2)
        {
            using var selectionFill = new SolidBrush(Color.FromArgb(45, 80, 170, 255));
            using var selectionBorder = new Pen(Color.FromArgb(180, 130, 200, 255), 1.5F)
            {
                DashStyle = DashStyle.Dash
            };
            e.Graphics.FillRectangle(selectionFill, _selectionRectangle);
            e.Graphics.DrawRectangle(selectionBorder, _selectionRectangle);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        _dragStart = e.Location;
        _selectionRectangle = Rectangle.Empty;
        _dragging = true;
        _dragAddsToSelection = (ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control;
        _selectionBeforeDrag.Clear();
        _selectionBeforeDrag.UnionWith(_selected);

        var hit = HitTest(e.Location);
        if (hit is not null)
        {
            if (_dragAddsToSelection)
            {
                if (!_selected.Add(hit.LedIndex))
                {
                    _selected.Remove(hit.LedIndex);
                }
            }
            else
            {
                _selected.Clear();
                _selected.Add(hit.LedIndex);
            }

            OnSelectionChanged();
        }
        else if (!_dragAddsToSelection)
        {
            _selected.Clear();
            OnSelectionChanged();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || e.Button != MouseButtons.Left)
        {
            return;
        }

        _selectionRectangle = NormalizeRectangle(_dragStart, e.Location);
        if (_selectionRectangle.Width < 4 && _selectionRectangle.Height < 4)
        {
            return;
        }

        _selected.Clear();
        if (_dragAddsToSelection)
        {
            _selected.UnionWith(_selectionBeforeDrag);
        }

        foreach (var key in KeyLayout)
        {
            if (_selectionRectangle.IntersectsWith(Rectangle.Round(ToScreen(key))))
            {
                _selected.Add(key.LedIndex);
            }
        }

        OnSelectionChanged();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        _selectionRectangle = Rectangle.Empty;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData == (System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.A) ||
            keyData == System.Windows.Forms.Keys.Escape)
        {
            return true;
        }

        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == System.Windows.Forms.Keys.A)
        {
            SelectAllKeys();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == System.Windows.Forms.Keys.Escape)
        {
            ClearSelection();
            e.SuppressKeyPress = true;
        }
    }

    private IEnumerable<int> TargetIndices() =>
        _selected.Count > 0 ? _selected.OrderBy(index => index) : KeyLayout.Select(key => key.LedIndex);

    private KeyboardKey? HitTest(Point point) =>
        KeyLayout.FirstOrDefault(key => ToScreen(key).Contains(point));

    private RectangleF ToScreen(KeyboardKey key)
    {
        const float padding = 18F;
        var availableWidth = Math.Max(1F, ClientSize.Width - padding * 2F);
        var availableHeight = Math.Max(1F, ClientSize.Height - padding * 2F);
        var scale = Math.Min(availableWidth / LayoutWidth, availableHeight / LayoutHeight);
        var originX = (ClientSize.Width - LayoutWidth * scale) / 2F;
        var originY = (ClientSize.Height - LayoutHeight * scale) / 2F;
        const float gap = 0.08F;
        return new RectangleF(
            originX + key.X * scale + gap * scale,
            originY + key.Y * scale + gap * scale,
            Math.Max(3F, (key.Width - gap * 2F) * scale),
            Math.Max(3F, (key.Height - gap * 2F) * scale));
    }

    private void OnSelectionChanged()
    {
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Rectangle NormalizeRectangle(Point first, Point second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(second.X - first.X),
        Math.Abs(second.Y - first.Y));

    private static Color InterpolateStops(IReadOnlyList<Color> stops, double position)
    {
        position = Math.Clamp(position, 0.0, 1.0);
        var scaled = position * (stops.Count - 1);
        var segment = Math.Min(stops.Count - 2, (int)Math.Floor(scaled));
        var amount = scaled - segment;
        var first = stops[segment];
        var second = stops[segment + 1];
        return Color.FromArgb(
            Lerp(first.R, second.R, amount),
            Lerp(first.G, second.G, amount),
            Lerp(first.B, second.B, amount));
    }

    private static int Lerp(byte first, byte second, double amount) =>
        Math.Clamp((int)Math.Round(first + (second - first) * amount), 0, 255);

    private static KeyboardKey[] CreateKeys() =>
    [
        K(0, "Esc", 0, 0), K(1, "F1", 2, 0), K(2, "F2", 3, 0), K(3, "F3", 4, 0),
        K(4, "F4", 5, 0), K(5, "F5", 6.5F, 0), K(6, "F6", 7.5F, 0),
        K(7, "F7", 8.5F, 0), K(8, "F8", 9.5F, 0), K(9, "F9", 11, 0),
        K(10, "F10", 12, 0), K(11, "F11", 13, 0), K(12, "F12", 14, 0),
        K(13, "Prt", 16, 0), K(14, "Win", 17, 0), K(15, "Pause", 18, 0),

        K(16, "`", 0, 1.35F), K(17, "1", 1, 1.35F), K(18, "2", 2, 1.35F),
        K(19, "3", 3, 1.35F), K(20, "4", 4, 1.35F), K(21, "5", 5, 1.35F),
        K(22, "6", 6, 1.35F), K(23, "7", 7, 1.35F), K(24, "8", 8, 1.35F),
        K(25, "9", 9, 1.35F), K(26, "0", 10, 1.35F), K(27, "-", 11, 1.35F),
        K(28, "=", 12, 1.35F), K(29, "Back", 13, 1.35F, 2),
        K(30, "Ins", 16, 1.35F), K(31, "Home", 17, 1.35F), K(32, "PgUp", 18, 1.35F),
        K(33, "Num", 20, 1.35F), K(34, "/", 21, 1.35F), K(35, "*", 22, 1.35F),
        K(36, "-", 23, 1.35F),

        K(37, "Tab", 0, 2.35F, 1.5F), K(38, "Q", 1.5F, 2.35F),
        K(39, "W", 2.5F, 2.35F), K(40, "E", 3.5F, 2.35F), K(41, "R", 4.5F, 2.35F),
        K(42, "T", 5.5F, 2.35F), K(43, "Y", 6.5F, 2.35F), K(44, "U", 7.5F, 2.35F),
        K(45, "I", 8.5F, 2.35F), K(46, "O", 9.5F, 2.35F), K(47, "P", 10.5F, 2.35F),
        K(48, "[", 11.5F, 2.35F), K(49, "]", 12.5F, 2.35F), K(50, "\\", 13.5F, 2.35F, 1.5F),
        K(51, "Del", 16, 2.35F), K(52, "End", 17, 2.35F), K(53, "PgDn", 18, 2.35F),
        K(54, "7", 20, 2.35F), K(55, "8", 21, 2.35F), K(56, "9", 22, 2.35F),
        K(57, "+", 23, 2.35F, 1, 2),

        K(58, "Caps", 0, 3.35F, 1.75F), K(59, "A", 1.75F, 3.35F),
        K(60, "S", 2.75F, 3.35F), K(61, "D", 3.75F, 3.35F), K(62, "F", 4.75F, 3.35F),
        K(63, "G", 5.75F, 3.35F), K(64, "H", 6.75F, 3.35F), K(65, "J", 7.75F, 3.35F),
        K(66, "K", 8.75F, 3.35F), K(67, "L", 9.75F, 3.35F), K(68, ";", 10.75F, 3.35F),
        K(69, "'", 11.75F, 3.35F), K(70, "Enter", 12.75F, 3.35F, 2.25F),
        K(71, "4", 20, 3.35F), K(72, "5", 21, 3.35F), K(73, "6", 22, 3.35F),

        K(74, "Shift", 0, 4.35F, 2.25F), K(75, "Z", 2.25F, 4.35F),
        K(76, "X", 3.25F, 4.35F), K(77, "C", 4.25F, 4.35F), K(78, "V", 5.25F, 4.35F),
        K(79, "B", 6.25F, 4.35F), K(80, "N", 7.25F, 4.35F), K(81, "M", 8.25F, 4.35F),
        K(82, ",", 9.25F, 4.35F), K(83, ".", 10.25F, 4.35F), K(84, "/", 11.25F, 4.35F),
        K(85, "Shift", 12.25F, 4.35F, 2.75F), K(86, "↑", 17, 4.35F),
        K(87, "1", 20, 4.35F), K(88, "2", 21, 4.35F), K(89, "3", 22, 4.35F),
        K(90, "Enter", 23, 4.35F, 1, 2),

        K(91, "Ctrl", 0, 5.35F, 1.25F), K(92, "Win", 1.25F, 5.35F, 1.25F),
        K(93, "Alt", 2.5F, 5.35F, 1.25F), K(94, "Space", 3.75F, 5.35F, 6.25F),
        K(95, "Alt", 10, 5.35F, 1.25F), K(96, "Win", 11.25F, 5.35F, 1.25F),
        K(97, "Menu", 12.5F, 5.35F, 1.25F), K(98, "Ctrl", 13.75F, 5.35F, 1.25F),
        K(99, "←", 16, 5.35F), K(100, "↓", 17, 5.35F), K(101, "→", 18, 5.35F),
        K(102, "0", 20, 5.35F, 2), K(103, ".", 22, 5.35F)
    ];

    private static KeyboardKey K(
        int ledIndex,
        string label,
        float x,
        float y,
        float width = 1F,
        float height = 1F) => new(ledIndex, label, x, y, width, height);

    private sealed record KeyboardKey(
        int LedIndex,
        string Label,
        float X,
        float Y,
        float Width,
        float Height);
}

internal static class RoundedRectangleGraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(RectangleF bounds, float radius)
    {
        var diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        if (diameter <= 0.1F)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
