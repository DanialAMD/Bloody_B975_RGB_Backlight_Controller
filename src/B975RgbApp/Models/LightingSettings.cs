using System.Drawing;
using System.Globalization;

namespace B975RgbApp.Models;

public sealed class LightingSettings
{
    public string Language { get; set; } = "fa";
    public string BackgroundHex { get; set; } = "300000";
    public string[]? BackgroundLedHex { get; set; }
    public string? BackgroundProfileName { get; set; }
    public string EffectHex { get; set; } = "00D2FF";
    public string[] EffectColors { get; set; } = CreateDefaultEffectColors();
    public string ReactiveEffectMode { get; set; } = "FlashFade";
    public string[] MeteorColors { get; set; } =
        ["0000FF", "00FF00", "FF0000", "00FFFF", "FF00FF", "FFFF00"];
    public int MeteorSpeed { get; set; } = 5;
    public string? MeteorProfileName { get; set; }
    public int BreathingPeriodMilliseconds { get; set; } = 2400;
    public int MinimumBrightnessPercent { get; set; } = 8;
    public int FadeMilliseconds { get; set; } = 550;
    public bool StartWithWindows { get; set; }
    public bool AutoStartEffect { get; set; }
    public bool CloseToTray { get; set; } = true;

    public LightingSettings Clone() => new()
    {
        Language = Language,
        BackgroundHex = BackgroundHex,
        BackgroundLedHex = BackgroundLedHex?.ToArray(),
        BackgroundProfileName = BackgroundProfileName,
        EffectHex = EffectHex,
        EffectColors = EffectColors?.ToArray() ?? CreateDefaultEffectColors(),
        ReactiveEffectMode = ReactiveEffectMode,
        MeteorColors = MeteorColors?.ToArray() ??
            ["0000FF", "00FF00", "FF0000", "00FFFF", "FF00FF", "FFFF00"],
        MeteorSpeed = MeteorSpeed,
        MeteorProfileName = MeteorProfileName,
        BreathingPeriodMilliseconds = BreathingPeriodMilliseconds,
        MinimumBrightnessPercent = MinimumBrightnessPercent,
        FadeMilliseconds = FadeMilliseconds,
        StartWithWindows = StartWithWindows,
        AutoStartEffect = AutoStartEffect,
        CloseToTray = CloseToTray
    };

    public static Color ParseColor(string value)
    {
        var normalized = NormalizeHex(value);
        return Color.FromArgb(
            int.Parse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public static string ToHex(Color color) => $"{color.R:X2}{color.G:X2}{color.B:X2}";

    public static string[] CreateDefaultEffectColors() =>
        ["00D2FF", "007DFF", "5055FF", "A03CFF", "DC23DC", "FF2882", "FF7323", "FFD700"];

    private static string NormalizeHex(string value)
    {
        var result = (value ?? string.Empty).Trim().TrimStart('#');
        return result.Length == 6 && result.All(Uri.IsHexDigit) ? result.ToUpperInvariant() : "000000";
    }
}
