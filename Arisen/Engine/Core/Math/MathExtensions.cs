using System.Numerics;

namespace ArisenEngine.Core.Math;

public static class MathExtensions
{
    public static Vector3 Forward => new Vector3(0, 0, 1);
    public static Vector3 Up => new Vector3(0, 1, 0);
    public static Vector3 Right => new Vector3(1, 0, 0);

    public static Vector3 ForwardVector(this Quaternion rotation)
    {
        return Vector3.Transform(Forward, rotation);
    }
}