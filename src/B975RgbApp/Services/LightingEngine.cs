using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using B975RgbApp.Models;

namespace B975RgbApp.Services;

internal sealed class LightingEngine : IDisposable
{
    private const int LedCount = 116;
    private const int FrameMilliseconds = 40;

    private readonly ConcurrentDictionary<int, long> _activeKeys = new();
    private readonly object _stateLock = new();
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private bool _disposed;
    private volatile bool _isRunning;

    public event Action<Exception>? Faulted;

    public bool IsRunning => _isRunning;

    public void Start(LightingSettings sourceSettings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_stateLock)
        {
            if (_isRunning)
            {
                return;
            }

            var settings = sourceSettings.Clone();
            var device = B975HidDevice.OpenPreferred();

            try
            {
                device.InitializeLighting();
            }
            catch
            {
                device.Dispose();
                throw;
            }

            _activeKeys.Clear();
            _cancellation = new CancellationTokenSource();
            _isRunning = true;
            _worker = Task.Run(() => RunLoop(device, settings, _cancellation.Token));
        }
    }

    public void TriggerKey(int ledIndex)
    {
        if (!_isRunning || ledIndex is < 0 or >= LedCount)
        {
            return;
        }

        _activeKeys[ledIndex] = Stopwatch.GetTimestamp();
    }

    public async Task StopAsync()
    {
        Task? worker;
        lock (_stateLock)
        {
            if (!_isRunning && _worker is null)
            {
                return;
            }

            _cancellation?.Cancel();
            worker = _worker;
        }

        if (worker is not null)
        {
            try
            {
                await worker.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }
    }

    private void RunLoop(B975HidDevice device, LightingSettings settings, CancellationToken token)
    {
        var background = LightingSettings.ParseColor(settings.BackgroundHex);
        var backgroundColors = CreateBackgroundColors(settings, background);
        var effectColors = CreateEffectColors(settings);
        var timer = Stopwatch.StartNew();

        try
        {
            while (!token.IsCancellationRequested)
            {
                var frameStarted = timer.ElapsedMilliseconds;
                var phase = frameStarted % settings.BreathingPeriodMilliseconds /
                            (double)settings.BreathingPeriodMilliseconds;
                var wave = 0.5 - 0.5 * Math.Cos(phase * Math.PI * 2.0);
                var minimum = settings.MinimumBrightnessPercent / 100.0;
                var intensity = minimum + (1.0 - minimum) * wave;

                var reds = ScaleValues(backgroundColors.Reds, intensity);
                var greens = ScaleValues(backgroundColors.Greens, intensity);
                var blues = ScaleValues(backgroundColors.Blues, intensity);
                ApplyReactiveEffects(reds, greens, blues, effectColors, settings);

                device.SendFrame(reds, greens, blues);

                var elapsed = timer.ElapsedMilliseconds - frameStarted;
                var remaining = FrameMilliseconds - (int)elapsed;
                if (remaining > 0 && token.WaitHandle.WaitOne(remaining))
                {
                    break;
                }
            }

            device.SendFrame(backgroundColors.Reds, backgroundColors.Greens, backgroundColors.Blues);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Faulted?.Invoke(exception);
        }
        finally
        {
            device.Dispose();
            _activeKeys.Clear();

            lock (_stateLock)
            {
                _isRunning = false;
                _worker = null;
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }
    }

    private void ApplyReactiveEffects(
        byte[] reds,
        byte[] greens,
        byte[] blues,
        Color[] effectColors,
        LightingSettings settings)
    {
        if (string.Equals(settings.ReactiveEffectMode, "Explosion", StringComparison.OrdinalIgnoreCase))
        {
            ApplyExplosionEffects(reds, greens, blues, effectColors, settings.FadeMilliseconds);
            return;
        }

        if (string.Equals(settings.ReactiveEffectMode, "Meteor", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMeteorEffects(reds, greens, blues, settings);
            return;
        }

        ApplyFlashEffects(reds, greens, blues, effectColors, settings.FadeMilliseconds);
    }

    private void ApplyExplosionEffects(
        byte[] reds,
        byte[] greens,
        byte[] blues,
        Color[] colors,
        int durationMilliseconds)
    {
        const double maximumRadius = 26.0;
        var duration = Math.Clamp(durationMilliseconds, 250, 5_000);
        var now = Stopwatch.GetTimestamp();

        foreach (var item in _activeKeys)
        {
            if (!KeyboardGeometry.TryGetCenter(item.Key, out var origin))
            {
                _activeKeys.TryRemove(item.Key, out _);
                continue;
            }

            var ageMilliseconds = (now - item.Value) * 1000.0 / Stopwatch.Frequency;
            if (ageMilliseconds >= duration)
            {
                _activeKeys.TryRemove(item.Key, out _);
                continue;
            }

            var progress = ageMilliseconds / duration;
            var radius = progress * maximumRadius;
            var ringThickness = 1.15 + progress * 1.35;
            var lifetimeStrength = 1.0 - progress * 0.62;

            for (var ledIndex = 0; ledIndex < KeyboardGeometry.KeyCount; ledIndex++)
            {
                if (!KeyboardGeometry.TryGetCenter(ledIndex, out var target))
                {
                    continue;
                }

                var deltaX = target.X - origin.X;
                var deltaY = target.Y - origin.Y;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                var ringStrength = 1.0 - Math.Abs(distance - radius) / ringThickness;
                var direction = (Math.Atan2(deltaY, deltaX) + Math.PI) / (Math.PI * 2.0);
                var targetColor = InterpolatePalette(
                    colors,
                    direction * colors.Length + progress * colors.Length * 0.65);

                // A short colored core starts the burst; the multicolor ring then
                // continues travelling in every physical direction.
                var coreStrength = progress < 0.16
                    ? Math.Max(0.0, 1.0 - distance / (1.4 + progress * 13.0)) *
                      (1.0 - progress / 0.16)
                    : 0.0;
                var strength = Math.Clamp(
                    Math.Max(ringStrength, coreStrength) * lifetimeStrength,
                    0.0,
                    1.0);
                if (strength <= 0.0)
                {
                    continue;
                }

                reds[ledIndex] = Blend(reds[ledIndex], targetColor.R, strength);
                greens[ledIndex] = Blend(greens[ledIndex], targetColor.G, strength);
                blues[ledIndex] = Blend(blues[ledIndex], targetColor.B, strength);
            }
        }
    }

    private void ApplyFlashEffects(
        byte[] reds,
        byte[] greens,
        byte[] blues,
        Color[] colors,
        int fadeMilliseconds)
    {
        var now = Stopwatch.GetTimestamp();
        foreach (var item in _activeKeys)
        {
            var ageMilliseconds = (now - item.Value) * 1000.0 / Stopwatch.Frequency;
            if (ageMilliseconds >= fadeMilliseconds)
            {
                _activeKeys.TryRemove(item.Key, out _);
                continue;
            }

            var strength = 1.0 - ageMilliseconds / fadeMilliseconds;
            var color = InterpolatePalette(colors, ageMilliseconds / fadeMilliseconds * colors.Length);
            reds[item.Key] = Blend(reds[item.Key], color.R, strength);
            greens[item.Key] = Blend(greens[item.Key], color.G, strength);
            blues[item.Key] = Blend(blues[item.Key], color.B, strength);
        }
    }

    private static Color[] CreateEffectColors(LightingSettings settings)
    {
        var colors = (settings.EffectColors ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(LightingSettings.ParseColor)
            .ToArray();
        return colors.Length > 0
            ? colors
            : LightingSettings.CreateDefaultEffectColors().Select(LightingSettings.ParseColor).ToArray();
    }

    private static Color InterpolatePalette(Color[] colors, double position)
    {
        if (colors.Length == 1)
        {
            return colors[0];
        }

        var wrapped = position % colors.Length;
        if (wrapped < 0)
        {
            wrapped += colors.Length;
        }

        var firstIndex = (int)Math.Floor(wrapped);
        var secondIndex = (firstIndex + 1) % colors.Length;
        var amount = wrapped - firstIndex;
        return Color.FromArgb(
            Blend(colors[firstIndex].R, colors[secondIndex].R, amount),
            Blend(colors[firstIndex].G, colors[secondIndex].G, amount),
            Blend(colors[firstIndex].B, colors[secondIndex].B, amount));
    }

    private void ApplyMeteorEffects(
        byte[] reds,
        byte[] greens,
        byte[] blues,
        LightingSettings settings)
    {
        var colors = (settings.MeteorColors ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(LightingSettings.ParseColor)
            .ToArray();
        if (colors.Length == 0)
        {
            colors = [Color.White];
        }

        var stepMilliseconds = Math.Clamp(140 - settings.MeteorSpeed * 15, 35, 125);
        var now = Stopwatch.GetTimestamp();
        foreach (var item in _activeKeys)
        {
            if (!TryGetRow(item.Key, out var rowStart, out var rowEnd))
            {
                _activeKeys.TryRemove(item.Key, out _);
                continue;
            }

            var ageMilliseconds = (now - item.Value) * 1000.0 / Stopwatch.Frequency;
            var headStep = (int)(ageMilliseconds / stepMilliseconds);
            var distanceLeft = item.Key - rowStart;
            var distanceRight = rowEnd - item.Key;
            var direction = distanceRight >= distanceLeft ? 1 : -1;
            var travelLength = Math.Max(distanceLeft, distanceRight) + 1;
            if (headStep >= travelLength + colors.Length)
            {
                _activeKeys.TryRemove(item.Key, out _);
                continue;
            }

            for (var tail = 0; tail < colors.Length; tail++)
            {
                var travelStep = headStep - tail;
                if (travelStep < 0 || travelStep >= travelLength)
                {
                    continue;
                }

                var ledIndex = item.Key + direction * travelStep;
                if (ledIndex < rowStart || ledIndex > rowEnd)
                {
                    continue;
                }

                var colorIndex = PositiveModulo(headStep - tail, colors.Length);
                var color = colors[colorIndex];
                var strength = 1.0 - tail / (colors.Length + 1.0);
                reds[ledIndex] = Blend(reds[ledIndex], color.R, strength);
                greens[ledIndex] = Blend(greens[ledIndex], color.G, strength);
                blues[ledIndex] = Blend(blues[ledIndex], color.B, strength);
            }
        }
    }

    private static int PositiveModulo(int value, int divisor) => (value % divisor + divisor) % divisor;

    private static bool TryGetRow(int ledIndex, out int start, out int end)
    {
        (start, end) = ledIndex switch
        {
            >= 0 and <= 15 => (0, 15),
            >= 16 and <= 36 => (16, 36),
            >= 37 and <= 57 => (37, 57),
            >= 58 and <= 73 => (58, 73),
            >= 74 and <= 90 => (74, 90),
            >= 91 and <= 103 => (91, 103),
            _ => (-1, -1)
        };
        return start >= 0;
    }

    private static (byte[] Reds, byte[] Greens, byte[] Blues) CreateBackgroundColors(
        LightingSettings settings,
        Color solidColor)
    {
        if (settings.BackgroundLedHex is not { Length: >= LedCount } profile)
        {
            return (FilledValues(solidColor.R), FilledValues(solidColor.G), FilledValues(solidColor.B));
        }

        var reds = new byte[LedCount];
        var greens = new byte[LedCount];
        var blues = new byte[LedCount];
        for (var index = 0; index < LedCount; index++)
        {
            var color = LightingSettings.ParseColor(profile[index]);
            reds[index] = color.R;
            greens[index] = color.G;
            blues[index] = color.B;
        }

        return (reds, greens, blues);
    }

    private static byte[] ScaleValues(byte[] source, double factor)
    {
        var result = new byte[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            result[index] = Scale(source[index], factor);
        }

        return result;
    }

    private static byte[] FilledValues(byte value)
    {
        var result = new byte[LedCount];
        Array.Fill(result, value);
        return result;
    }

    private static byte Scale(byte value, double factor) =>
        (byte)Math.Clamp((int)Math.Round(value * factor), 0, 255);

    private static byte Blend(byte background, byte effect, double strength) =>
        (byte)Math.Clamp((int)Math.Round(background + (effect - background) * strength), 0, 255);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cancellation?.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // The UI has already received the engine error.
        }

        _cancellation?.Dispose();
        _disposed = true;
    }
}
