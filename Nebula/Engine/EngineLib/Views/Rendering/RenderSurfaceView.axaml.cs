using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NebulaEngine.Graphics;
using System.Diagnostics;
using System.Drawing;
using System.Reflection.Metadata;
using static AvaloniaLib.Native.NativeAPI;

namespace AvaloniaLib.Views.Rendering
{
    public partial class RenderSurfaceView : UserControl
    {
        private NebulaEngine.Graphics.RenderSurfaceHost m_Host = null;
        public RenderSurfaceView()
        {
            InitializeComponent();
            Loaded += OnRenderSurfaceViewLoaded;
            Unloaded += OnRenderSurfaceViewUnloaded;
            
        }
        private void OnRenderSurfaceViewLoaded(object? sender, RoutedEventArgs e)
        {
            Debug.Assert(sender != null);

            Loaded -= OnRenderSurfaceViewLoaded;

            m_Host = new RenderSurfaceHost(Width, Height);



#if (NEBULA_EDITOR || true)

            SizeChanged += OnSizeChanged;
            //m_Host.MessageHook += HostMsgFilter;

#endif

            Content = m_Host;
        }

        private void OnRenderSurfaceViewUnloaded(object? sender, RoutedEventArgs e)
        {
            Debug.Assert(sender != null);
            Unloaded -= OnRenderSurfaceViewUnloaded;

#if (NEBULA_EDITOR || true)

            //m_Host.MessageHook -= HostMsgFilter;
            SizeChanged -= OnSizeChanged;
#endif
            Content = null;

            m_Host.Dispose();
            m_Host = null;
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!Design.IsDesignMode)
            {
                m_Host.Resize();
            }
        }

        //private IntPtr HostMsgFilter(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam/*, ref bool handled*/)
        //{
        //    switch ((Win32Msg)msg)
        //    {
        //        case Win32Msg.WM_MOVE:
        //            {

        //            }
        //            break;

        //        case Win32Msg.WM_CREATE:
        //            {

        //            }
        //            break;
        //        case Win32Msg.WM_DESTROY:
        //            {

        //            }
        //            break;
        //        case Win32Msg.WM_SIZE:
        //            {
        //                m_Host.Resize();
        //            }
        //            break;
        //        case Win32Msg.WM_ACTIVATE:
        //            {

        //            }
        //            break;
        //        case Win32Msg.WM_ENTERSIZEMOVE:
        //            {

        //            }
        //            break;
        //        case Win32Msg.WM_EXITSIZEMOVE:
        //            {

        //            }
        //            break;
        //        case Win32Msg.WM_SIZING:
        //            {

        //            }
        //            break;
        //    }

        //    return IntPtr.Zero;
        //}
    }
}