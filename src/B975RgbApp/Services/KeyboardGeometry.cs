using System.Drawing;

namespace B975RgbApp.Services;

internal static class KeyboardGeometry
{
    private static readonly PointF[] KeyCenters =
    [
        // Function row: LED 0..15
        P(0.5F, 0.5F), P(2.5F, 0.5F), P(3.5F, 0.5F), P(4.5F, 0.5F),
        P(5.5F, 0.5F), P(7F, 0.5F), P(8F, 0.5F), P(9F, 0.5F),
        P(10F, 0.5F), P(11.5F, 0.5F), P(12.5F, 0.5F), P(13.5F, 0.5F),
        P(14.5F, 0.5F), P(16.5F, 0.5F), P(17.5F, 0.5F), P(18.5F, 0.5F),

        // Number row: LED 16..36
        P(0.5F, 1.85F), P(1.5F, 1.85F), P(2.5F, 1.85F), P(3.5F, 1.85F),
        P(4.5F, 1.85F), P(5.5F, 1.85F), P(6.5F, 1.85F), P(7.5F, 1.85F),
        P(8.5F, 1.85F), P(9.5F, 1.85F), P(10.5F, 1.85F), P(11.5F, 1.85F),
        P(12.5F, 1.85F), P(14F, 1.85F), P(16.5F, 1.85F), P(17.5F, 1.85F),
        P(18.5F, 1.85F), P(20.5F, 1.85F), P(21.5F, 1.85F), P(22.5F, 1.85F),
        P(23.5F, 1.85F),

        // QWERTY row: LED 37..57
        P(0.75F, 2.85F), P(2F, 2.85F), P(3F, 2.85F), P(4F, 2.85F),
        P(5F, 2.85F), P(6F, 2.85F), P(7F, 2.85F), P(8F, 2.85F),
        P(9F, 2.85F), P(10F, 2.85F), P(11F, 2.85F), P(12F, 2.85F),
        P(13F, 2.85F), P(14.25F, 2.85F), P(16.5F, 2.85F), P(17.5F, 2.85F),
        P(18.5F, 2.85F), P(20.5F, 2.85F), P(21.5F, 2.85F), P(22.5F, 2.85F),
        P(23.5F, 2.85F),

        // Home row: LED 58..73
        P(0.875F, 3.85F), P(2.25F, 3.85F), P(3.25F, 3.85F), P(4.25F, 3.85F),
        P(5.25F, 3.85F), P(6.25F, 3.85F), P(7.25F, 3.85F), P(8.25F, 3.85F),
        P(9.25F, 3.85F), P(10.25F, 3.85F), P(11.25F, 3.85F), P(12.25F, 3.85F),
        P(13.875F, 3.85F), P(20.5F, 3.85F), P(21.5F, 3.85F), P(22.5F, 3.85F),

        // Shift row: LED 74..90
        P(1.125F, 4.85F), P(2.75F, 4.85F), P(3.75F, 4.85F), P(4.75F, 4.85F),
        P(5.75F, 4.85F), P(6.75F, 4.85F), P(7.75F, 4.85F), P(8.75F, 4.85F),
        P(9.75F, 4.85F), P(10.75F, 4.85F), P(11.75F, 4.85F), P(13.625F, 4.85F),
        P(17.5F, 4.85F), P(20.5F, 4.85F), P(21.5F, 4.85F), P(22.5F, 4.85F),
        P(23.5F, 4.85F),

        // Bottom row: LED 91..103
        P(0.625F, 5.85F), P(1.875F, 5.85F), P(3.125F, 5.85F), P(6.875F, 5.85F),
        P(10.625F, 5.85F), P(11.875F, 5.85F), P(13.125F, 5.85F), P(14.375F, 5.85F),
        P(16.5F, 5.85F), P(17.5F, 5.85F), P(18.5F, 5.85F), P(21F, 5.85F),
        P(22.5F, 5.85F)
    ];

    public static int KeyCount => KeyCenters.Length;

    public static bool TryGetCenter(int ledIndex, out PointF center)
    {
        if (ledIndex >= 0 && ledIndex < KeyCenters.Length)
        {
            center = KeyCenters[ledIndex];
            return true;
        }

        center = PointF.Empty;
        return false;
    }

    private static PointF P(float x, float y) => new(x, y);
}
