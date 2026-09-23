using System.Drawing;
using System.Windows.Forms;

namespace B975RgbApp;

internal static class AppLanguage
{
    private sealed record LocalizedText(string Persian, string English);

    public static string Current { get; private set; } = "fa";
    public static bool IsEnglish => string.Equals(Current, "en", StringComparison.OrdinalIgnoreCase);

    public static void SetLanguage(string? language)
    {
        Current = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fa";
    }

    public static string T(string persian, string english) => IsEnglish ? english : persian;

    public static TControl BindControl<TControl>(TControl control, string persian, string english)
        where TControl : Control
    {
        control.Tag = new LocalizedText(persian, english);
        ApplyControl(control);
        return control;
    }

    public static TItem BindItem<TItem>(TItem item, string persian, string english)
        where TItem : ToolStripItem
    {
        item.Tag = new LocalizedText(persian, english);
        item.Text = T(persian, english);
        return item;
    }

    public static void RefreshControls(Control root)
    {
        ApplyControl(root);
        foreach (Control child in root.Controls)
        {
            RefreshControls(child);
        }
    }

    public static void RefreshItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            if (item.Tag is LocalizedText text)
            {
                item.Text = T(text.Persian, text.English);
            }

            if (item is ToolStripDropDownItem dropDown)
            {
                RefreshItems(dropDown.DropDownItems);
            }
        }
    }

    private static void ApplyControl(Control control)
    {
        if (control.Tag is LocalizedText text)
        {
            control.Text = T(text.Persian, text.English);
        }

        if (control is Label label && label.TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.MiddleRight)
        {
            label.TextAlign = IsEnglish ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        if (control is CheckBox checkBox)
        {
            checkBox.TextAlign = IsEnglish ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
        }

        if (control is FlowLayoutPanel flow)
        {
            flow.FlowDirection = IsEnglish ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        }
    }
}
