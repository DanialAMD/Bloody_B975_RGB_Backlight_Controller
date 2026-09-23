using System.Xml.Linq;
using B975RgbApp;
using B975RgbApp.Models;

namespace B975RgbApp.Services;

internal sealed record CkPannelProfile(string Name, string[] LedHexColors)
{
    private const int LedCount = 116;

    public static CkPannelProfile Load(string path)
    {
        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root
                   ?? throw new InvalidDataException(AppLanguage.T(
                       "ساختار فایل ckPannel معتبر نیست.",
                       "The ckPannel file structure is invalid."));
        var colorPicture = root.Element("ColorPicture")?.Value;
        if (string.IsNullOrWhiteSpace(colorPicture))
        {
            throw new InvalidDataException(AppLanguage.T(
                "بخش ColorPicture در فایل ckPannel پیدا نشد.",
                "The ColorPicture section was not found in the ckPannel file."));
        }

        var source = colorPicture.Split(',', StringSplitOptions.TrimEntries);
        if (source.Length < 104)
        {
            throw new InvalidDataException(AppLanguage.T(
                "تعداد رنگ‌های فایل برای چیدمان B975 کافی نیست.",
                "The file does not contain enough colors for the B975 layout."));
        }

        var colors = new string[LedCount];
        for (var index = 0; index < colors.Length; index++)
        {
            colors[index] = index < source.Length
                ? NormalizeColor(source[index], index)
                : "000000";
        }

        return new CkPannelProfile(Path.GetFileName(path), colors);
    }

    private static string NormalizeColor(string value, int index)
    {
        var normalized = value.Trim().TrimStart('#');
        if (normalized.Length != 6 || !normalized.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException(AppLanguage.T(
                $"رنگ شماره {index} در فایل ckPannel معتبر نیست.",
                $"Color number {index} in the ckPannel file is invalid."));
        }

        // ckPannel uses logical RGB. The verified physical red/blue channel swap
        // remains isolated in B975HidDevice and must not be repeated here.
        _ = LightingSettings.ParseColor(normalized);
        return normalized.ToUpperInvariant();
    }
}
