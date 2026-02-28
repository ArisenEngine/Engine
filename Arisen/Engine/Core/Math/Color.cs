using System.Runtime.InteropServices;

namespace ArisenEngine.Core.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Color
{
    public float r, g, b, a;

    public Color(float r, float g, float b, float a = 1.0f)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public static Color white => new Color(1f, 1f, 1f, 1f);
    public static Color black => new Color(0f, 0f, 0f, 1f);
    public static Color red => new Color(1f, 0f, 0f, 1f);
    public static Color green => new Color(0f, 1f, 0f, 1f);
    public static Color blue => new Color(0f, 0f, 1f, 1f);
    public static Color yellow => new Color(1f, 0.92f, 0.016f, 1f);
    public static Color cyan => new Color(0f, 1f, 1f, 1f);
    public static Color magenta => new Color(1f, 0f, 1f, 1f);
    public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
    public static Color clear => new Color(0f, 0f, 0f, 0f);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Mathf.Clamp01(t);
        return new Color(
            a.r + (b.r - a.r) * t,
            a.g + (b.g - a.g) * t,
            a.b + (b.b - a.b) * t,
            a.a + (b.a - a.a) * t
        );
    }

    public override string ToString() => $"RGBA({r:F3}, {g:F3}, {b:F3}, {a:F3})";
}