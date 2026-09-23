using System.Xml.Linq;
using B975RgbApp.Models;

namespace B975RgbApp.Services;

internal sealed record CkPannelProfile(string Name, string[] LedHexColors)
{
    private const int LedCount = 116;

    public static CkPannelProfile Load(string path)
    {
        var document = XDocument.Load(path, LoadOptions.None);
        var root = document.Root
                   ?? throw new InvalidDataException("ساختار فایل ckPannel معتبر نیست.");
        var colorPicture = root.Element("ColorPicture")?.Value;
        if (string.IsNullOrWhiteSpace(colorPicture))
        {
            throw new InvalidDataException("بخش ColorPicture در فایل ckPannel پیدا نشد.");
        }

        var source = colorPicture.Split(',', StringSplitOptions.TrimEntries);
        if (source.Length < 104)
        {
            throw new InvalidDataException("تعداد رنگ‌های فایل برای چیدمان B975 کافی نیست.");
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
            throw new InvalidDataException($"رنگ شماره {index} در فایل ckPannel معتبر نیست.");
        }

        // ckPannel uses logical RGB. The verified physical red/blue channel swap
        // remains isolated in B975HidDevice and must not be repeated here.
        _ = LightingSettings.ParseColor(normalized);
        return normalized.ToUpperInvariant();
    }
}
