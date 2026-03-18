using System;
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

    /// <summary>
    /// Converts a Quaternion to Euler angles (in degrees) using ZYX decomposition.
    /// Returns (Pitch, Yaw, Roll) as X, Y, Z respectively.
    /// </summary>
    public static Vector3 QuaternionToEulerDegrees(this Quaternion q)
    {
        // Roll (Z-axis)
        float sinRcosP = 2.0f * (q.W * q.Z + q.X * q.Y);
        float cosRcosP = 1.0f - 2.0f * (q.Y * q.Y + q.Z * q.Z);
        float roll = Mathf.Atan2(sinRcosP, cosRcosP);

        // Pitch (X-axis)
        float sinP = 2.0f * (q.W * q.X - q.Z * q.Y);
        float pitch;
        if (Mathf.Abs(sinP) >= 1.0f)
            pitch = MathF.CopySign(Mathf.PI / 2.0f, sinP); // Gimbal lock
        else
            pitch = Mathf.Asin(sinP);

        // Yaw (Y-axis)
        float sinYcosP = 2.0f * (q.W * q.Y + q.Z * q.X);
        float cosYcosP = 1.0f - 2.0f * (q.X * q.X + q.Y * q.Y);
        float yaw = Mathf.Atan2(sinYcosP, cosYcosP);

        return new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
    }
}