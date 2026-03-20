using ArisenEngine.Core.Math;
using System.Runtime.InteropServices;

namespace ArisenEngine.Rendering;

public enum CameraProjectionType
{
    Perspective,
    Orthographic
}

[StructLayout(LayoutKind.Sequential)]
public struct Camera
{
    public float FieldOfView;
    public float NearClip;
    public float FarClip;
    public float AspectRatio;
    public float OrthographicSize;
    public CameraProjectionType ProjectionType;

    public Vector3 Position;
    public Vector3 Rotation; // Eulers in degrees

    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            if (ProjectionType == CameraProjectionType.Perspective)
            {
                return Matrix4x4.CreatePerspectiveFieldOfView(Mathf.Deg2Rad * FieldOfView, AspectRatio, NearClip,
                    FarClip);
            }
            else
            {
                float h = OrthographicSize * 2.0f;
                float w = h * AspectRatio;
                return Matrix4x4.CreateOrthographic(w, h, NearClip, FarClip);
            }
        }
    }

    public Matrix4x4 ViewMatrix
    {
        get
        {
            // Simple FPS-style camera for now
            Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(
                Mathf.Deg2Rad * Rotation.Y,
                Mathf.Deg2Rad * Rotation.X,
                Mathf.Deg2Rad * Rotation.Z
            );

            Vector3 forward = Vector3.Transform(MathExtensions.Forward, rotation);
            Vector3 target = Position + forward;
            Vector3 up = Vector3.Transform(MathExtensions.Up, rotation);

            return Matrix4x4.CreateLookAt(Position, target, up);
        }
    }
}