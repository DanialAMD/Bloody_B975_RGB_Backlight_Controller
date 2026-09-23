using System.Xml.Linq;

namespace B975RgbApp.Services;

internal sealed record CkButtonProfile(
    string Name,
    int ButtonType,
    string[] DownColors,
    string UpColor,
    int Speed)
{
    public static CkButtonProfile Load(string path)
    {
        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root
                   ?? throw new InvalidDataException("ساختار فایل ckButton معتبر نیست.");

        if (!int.TryParse(root.Element("ButtonType")?.Value, out var buttonType) || buttonType != 1)
        {
            throw new InvalidDataException("این نسخه فقط ckButton نوع Meteor با ButtonType=1 را پشتیبانی می‌کند.");
        }

        var colorsText = root.Element("ButtonDownColor")?.Value;
        if (string.IsNullOrWhiteSpace(colorsText))
        {
            throw new InvalidDataException("رنگ‌های ButtonDownColor در فایل ckButton پیدا نشد.");
        }

        var colors = colorsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((value, index) => NormalizeColor(value, $"ButtonDownColor[{index}]"))
            .ToArray();
        if (colors.Length == 0)
        {
            throw new InvalidDataException("فایل Meteor هیچ رنگ معتبری ندارد.");
        }

        var upColor = NormalizeColor(root.Element("ButtonUpColor")?.Value ?? "000000", "ButtonUpColor");
        if (!int.TryParse(root.Element("Speed")?.Value, out var speed))
        {
            speed = 5;
        }

        return new CkButtonProfile(
            Path.GetFileName(path),
            buttonType,
            colors,
            upColor,
            Math.Clamp(speed, 1, 12));
    }

    private static string NormalizeColor(string value, string field)
    {
        var normalized = value.Trim().TrimStart('#');
        if (normalized.Length != 6 || !normalized.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"رنگ {field} در فایل ckButton معتبر نیست.");
        }

        return normalized.ToUpperInvariant();
    }
}
