using System;

namespace ArisenEngine.Core.Math;

public static class Mathf
{
    public const float PI = 3.14159265f;
    public const float Infinity = float.PositiveInfinity;
    public const float NegativeInfinity = float.NegativeInfinity;
    public const float Deg2Rad = PI * 2.0f / 360.0f;
    public const float Rad2Deg = 1.0f / Deg2Rad;
    public const float Epsilon = 1e-6f;

    public static float Abs(float f) => MathF.Abs(f);
    public static float Min(float a, float b) => MathF.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);

    public static float Clamp(float value, float min, float max)
    {
        if (value < min) value = min;
        else if (value > max) value = max;
        return value;
    }

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);

    public static float Lerp(float a, float b, float t)
    {
        t = Clamp01(t);
        return a + (b - a) * t;
    }

    public static float Sin(float f) => MathF.Sin(f);
    public static float Cos(float f) => MathF.Cos(f);
    public static float Tan(float f) => MathF.Tan(f);
    public static float Asin(float f) => MathF.Asin(f);
    public static float Acos(float f) => MathF.Acos(f);
    public static float Atan(float f) => MathF.Atan(f);
    public static float Atan2(float y, float x) => MathF.Atan2(y, x);
    public static float Sqrt(float f) => MathF.Sqrt(f);
    public static float Pow(float f, float p) => MathF.Pow(f, p);

    public static bool Approximately(float a, float b)
    {
        return Abs(b - a) < Max(1e-6f * Max(Abs(a), Abs(b)), Epsilon * 8);
    }
}