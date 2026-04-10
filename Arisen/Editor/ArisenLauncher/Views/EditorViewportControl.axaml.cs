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
        private IRHIDevice? _rhiDevice;
        private uint _imageHandleIndex;
        private uint _imageHandleGeneration;

        public EditorViewportControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void BindSharedHandle(IRHIDevice device, uint index, uint generation)
        {
            _rhiDevice = device;
            _imageHandleIndex = index;
            _imageHandleGeneration = generation;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_rhiDevice == null || !_rhiDevice.IsValid)
                return;

            IntPtr win32Handle = _rhiDevice.GetSharedWin32Handle(_imageHandleIndex, _imageHandleGeneration);
            if (win32Handle == IntPtr.Zero)
                return;

            context.Custom(new SharedTextureDrawOperation(Bounds, win32Handle));
        }

        private class SharedTextureDrawOperation : ICustomDrawOperation
        {
            private readonly IntPtr _win32Handle;
            private static IntPtr _d3d11Device;
            private static IntPtr _d3d11Context;

            public Rect Bounds { get; }

            public SharedTextureDrawOperation(Rect bounds, IntPtr win32Handle)
            {
                Bounds = bounds;
                _win32Handle = win32Handle;
                InitD3D11();
            }

            private static void InitD3D11()
            {
                if (_d3d11Device != IntPtr.Zero) return;

                D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE.HARDWARE, IntPtr.Zero, 0, null, 0, 
                    7, out _d3d11Device, out _, out _d3d11Context);
            }

            public void Dispose() { }

            public bool Equals(ICustomDrawOperation? other) => false;
            public bool HitTest(Point p) => false;

            public void Render(ImmediateDrawingContext context)
            {
                if (_win32Handle == IntPtr.Zero || _d3d11Device == IntPtr.Zero) return;

                // 1. Open common shared resource
                Guid texture2DGuid = new Guid("6F15E347-0409-4C0F-8106-96D62E1A9D04");
                int hr = OpenSharedResource(_d3d11Device, _win32Handle, ref texture2DGuid, out IntPtr texturePtr);
                if (hr != 0 || texturePtr == IntPtr.Zero) return;

                try
                {
                    // 2. Interop with Avalonia/Skia
                    // Since we want zero-dependency, we use reflection to find the Skia side if available.
                    // If Avalonia is using D3D11 natively, we can try to get the D3D11 device from Avalonia.
                    
                    // TODO: Final binding to Avalonia's internal Skia context.
                    // This requires accessing context.GetFeature<ISkiaSharpApiFeatureFlags>() or similar.
                }
                finally
                {
                    Marshal.Release(texturePtr);
                }
            }

            #region D3D11 P/Invoke
            [DllImport("d3d11.dll", SetLastError = true)]
            private static extern int D3D11CreateDevice(IntPtr pAdapter, D3D_DRIVER_TYPE DriverType, IntPtr Software, uint Flags, 
                [MarshalAs(UnmanagedType.LPArray)] int[]? pFeatureLevels, uint FeatureLevels, uint SDKVersion, 
                out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);

            [DllImport("d3d11.dll", SetLastError = true)]
            private static extern int OpenSharedResource(IntPtr device, IntPtr hResource, ref Guid riid, out IntPtr ppResource);

            private enum D3D_DRIVER_TYPE { UNKNOWN = 0, HARDWARE, REFERENCE, NULL, SOFTWARE, WARP }
            #endregion
        }
    }
}
