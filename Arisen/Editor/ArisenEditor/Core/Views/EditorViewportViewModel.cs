using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

public class EditorViewportViewModel : ReactiveObject
{
    private IImage? _viewportImage;

    /// <summary>
    /// The Image surface that the Shared GPU Texture will be bound to.
    /// Avalonia binds directly to this property.
    /// </summary>
    public IImage? ViewportImage
    {
        get => _viewportImage;
        set => this.RaiseAndSetIfChanged(ref _viewportImage, value);
    }
    
    public bool IsSceneView { get; }

    public EditorViewportViewModel(bool isSceneView)
    {
        IsSceneView = isSceneView;
        // In the future, this is where we will hook up `Avalonia.Platform.Interop.IExternalMemory`
        // or a `WriteableBitmap` bound to the Shared Handle exported by Arisen Engine's RHI.
    }
}
