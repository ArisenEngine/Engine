using System;
namespace ArisenEngine.Rendering;

public enum SurfaceType
{
    GameView = 0,
    SceneView,
    AssetView,
    Count
}

public struct SurfaceInfo
{
    public string Name;
    public IntPtr Parent;
    public IRenderSurface Surface;
    public SurfaceType SurfaceType;
}

public interface IRenderSurface
{
    public IntPtr GetHandle();
    public uint SurfaceId { get; }
    public void DisposeSurface();
    public void OnCreate();
    public void OnResizing();
    public void OnResized();
    public void OnDestroy();
    public bool IsValid();
}