using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Markup.Xaml;
using ArisenKernel.Contracts;

namespace ArisenLauncher.Views
{
    public partial class EditorViewportControl : UserControl
    {
        private IRenderSurface? _surface;
        private CompositionDrawingSurface? _compositionSurface;
        private ICompositionGpuInterop? _interop;

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
            InitializeComposition();
        }

        private void InitializeComposition()
        {
            var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
            if (compositor == null) return;

            _interop = compositor.TryGetCompositionGpuInterop();
            if (_interop == null) return;

            _compositionSurface = compositor.CreateDrawingSurface();
            
            // Create a CompositionVisual to host our surface
            var visual = compositor.CreateSurfaceVisual();
            visual.Surface = _compositionSurface;
            visual.Size = new Vector(Bounds.Width, Bounds.Height);
            
            ElementComposition.SetElementChildVisual(this, visual);
        }

        public void BindSurface(IRenderSurface surface)
        {
            _surface = surface;
            InvalidateVisual();
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
                var pixelSize = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
                
                // Import the D3D11 shared texture into Avalonia
                var importedImage = _interop.ImportImage(
                    KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle,
                    sharedHandle,
                    pixelSize);

                // Phase 1 Synchronization: RenderSubsystem already performs a WaitIdle for virtual surfaces
                // to ensure the texture is ready before this update.
                await _compositionSurface.UpdateAsync(importedImage);
                
                // Successfully updated the compositor with the new frame.
                importedImage.Dispose();
            }
            catch (Exception ex)
            {
                // Log or handle interop failure
            }
        }
    }
    }
}
