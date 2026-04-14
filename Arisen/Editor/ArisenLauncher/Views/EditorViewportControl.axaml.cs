using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using ArisenKernel.Contracts;
using System.Threading.Tasks;

namespace ArisenLauncher.Views
{
    public partial class EditorViewportControl : UserControl
    {
        private IRenderSurface? _surface;
        private CompositionDrawingSurface? _compositionSurface;
        private ICompositionGpuInterop? _interop;

        private PixelSize GetPhysicalPixelSize()
        {
            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            return new PixelSize((int)Math.Max(1, Bounds.Width * scaling), (int)Math.Max(1, Bounds.Height * scaling));
        }

        public EditorViewportControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _ = InitializeCompositionAsync();
        }

        private async Task InitializeCompositionAsync()
        {
            try 
            {
                var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
                if (compositor == null) return;

                _interop = await compositor.TryGetCompositionGpuInterop();
                if (_interop == null) return;

                _compositionSurface = compositor.CreateDrawingSurface();
                
                // Create a CompositionVisual to host our surface
                var visual = compositor.CreateSurfaceVisual();
                visual.Surface = _compositionSurface;
                visual.Size = new Vector(Bounds.Width, Bounds.Height);
                
                ElementComposition.SetElementChildVisual(this, visual);

                // Trigger visual update once interop is ready
                Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
            }
            catch (Exception)
            {
                // Silently fail or log if possible
            }
        }

        public void BindSurface(IRenderSurface surface)
        {
            _surface = surface;
            
            // Apply initial size (Physical Pixels)
            var size = GetPhysicalPixelSize();
            if (size.Width > 0 && size.Height > 0)
            {
                _surface.Resize((uint)size.Width, (uint)size.Height);
            }
            
            InvalidateVisual();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty)
            {
                var size = GetPhysicalPixelSize();
                if (_surface != null && size.Width > 0 && size.Height > 0)
                {
                    _surface.Resize((uint)size.Width, (uint)size.Height);
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_surface == null || _compositionSurface == null || _interop == null)
                return;

            IntPtr win32Handle = _surface.GetSharedHandle();
            if (win32Handle == IntPtr.Zero)
                return;

            UpdateCompositionSurface(win32Handle);
        }

        private async void UpdateCompositionSurface(IntPtr sharedHandle)
        {
            if (_interop == null || _compositionSurface == null) return;

            try 
            {
                var pixelSize = GetPhysicalPixelSize();
                
                // Import the D3D11 shared texture into Avalonia
                var importedImage = _interop.ImportImage(
                    new Avalonia.Platform.PlatformHandle(sharedHandle, KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle),
                    new Avalonia.Platform.PlatformGraphicsExternalImageProperties
                    {
                        Width = pixelSize.Width,
                        Height = pixelSize.Height,
                        Format = Avalonia.Platform.PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm
                    });

                try 
                {
                    // Phase 1.5 Synchronization: RenderSubsystem already performs a WaitQueueTicket(ticket)
                    // for virtual surfaces to ensure the texture is ready before this update.
                    await _compositionSurface.UpdateAsync(importedImage);
                }
                finally 
                {
                    importedImage.Dispose();
                }

            }
            catch (Exception ex)
            {
                // Log or handle interop failure
            }
        }
    }
}
