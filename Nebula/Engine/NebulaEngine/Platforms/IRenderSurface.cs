namespace NebulaEngine.Platforms;

internal interface IRenderSurface
{
    
    public IntPtr GetHandle();
    public void OnCreate();
    public void OnResizing();
    public void OnResized();
    public void OnDestroy();

    public bool IsValid();
}