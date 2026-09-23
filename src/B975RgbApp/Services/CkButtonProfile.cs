using System.Xml.Linq;
using B975RgbApp;

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
                   ?? throw new InvalidDataException(AppLanguage.T(
                       "ساختار فایل ckButton معتبر نیست.",
                       "The ckButton file structure is invalid."));

        if (!int.TryParse(root.Element("ButtonType")?.Value, out var buttonType) || buttonType != 1)
        {
            throw new InvalidDataException(AppLanguage.T(
                "این نسخه فقط ckButton نوع Meteor با ButtonType=1 را پشتیبانی می‌کند.",
                "This version only supports Meteor ckButton files with ButtonType=1."));
        }

        var colorsText = root.Element("ButtonDownColor")?.Value;
        if (string.IsNullOrWhiteSpace(colorsText))
        {
            throw new InvalidDataException(AppLanguage.T(
                "رنگ‌های ButtonDownColor در فایل ckButton پیدا نشد.",
                "ButtonDownColor values were not found in the ckButton file."));
        }

        var colors = colorsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((value, index) => NormalizeColor(value, $"ButtonDownColor[{index}]"))
            .ToArray();
        if (colors.Length == 0)
        {
            throw new InvalidDataException(AppLanguage.T(
                "فایل Meteor هیچ رنگ معتبری ندارد.",
                "The Meteor file does not contain any valid colors."));
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
            throw new InvalidDataException(AppLanguage.T(
                $"رنگ {field} در فایل ckButton معتبر نیست.",
                $"Color {field} in the ckButton file is invalid."));
        }

        return normalized.ToUpperInvariant();
    }
}
