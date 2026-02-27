using ArisenEngine.Core.Math;

namespace ArisenEngine.Rendering;

public enum CameraProjectionType
{
    Perspective,
    Orthographic
}

public class Camera
{
    public float FieldOfView = 60.0f;
    public float NearClip = 0.1f;
    public float FarClip = 1000.0f;
    public float AspectRatio = 1.0f;
    public float OrthographicSize = 5.0f;
    public CameraProjectionType ProjectionType = CameraProjectionType.Perspective;

    public Vector3 Position = Vector3.Zero;
    public Vector3 Rotation = Vector3.Zero; // Eulers in degrees

    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            if (ProjectionType == CameraProjectionType.Perspective)
            {
                return Matrix4x4.CreatePerspectiveFieldOfView(Mathf.Deg2Rad * FieldOfView, AspectRatio, NearClip, FarClip);
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