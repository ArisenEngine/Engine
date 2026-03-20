namespace ArisenEngine.Rendering;

/// <summary>
/// Contract for a rendering pipeline execution.
/// </summary>
public interface IRenderPipeline
{
    /// <summary>
    /// Initializes the rendering pipeline with the active RHI device.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Executes the rendering pipeline for the current frame.
    /// </summary>
    void Render();

    /// <summary>
    /// Cleans up pipeline resources.
    /// </summary>
    void Shutdown();
}
