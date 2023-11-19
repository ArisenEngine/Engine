using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NebulaEngine.Graphics;
using System.Diagnostics;
using System.Drawing;
using System.Reflection.Metadata;
using NebulaEditor.ViewModels;
using static AvaloniaLib.Native.NativeAPI;

namespace NebulaEngine.Views.Rendering
{
    public partial class RenderSurfaceView : UserControl
    {
        private RenderSurfaceViewModel? ViewModel
        {
            get
            {
                return DataContext != null ? (DataContext as RenderSurfaceViewModel) : null;
            }
        }
        
        private NebulaEngine.Graphics.RenderSurfaceHost m_Host = null;
        public string Name = "RenderSurface";
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

            if (!Design.IsDesignMode)
            {
                m_Host = new RenderSurfaceHost(RenderViewContainer.Width, RenderViewContainer.Height);
                
                SizeChanged += OnSizeChanged;

                RenderViewContainer.Children.Insert(0, m_Host);
            }
            
        }

        private void OnRenderSurfaceViewUnloaded(object? sender, RoutedEventArgs e)
        {
            Debug.Assert(sender != null);
            Unloaded -= OnRenderSurfaceViewUnloaded;

            if (!Design.IsDesignMode)
            {
                SizeChanged -= OnSizeChanged;
                RenderViewContainer.Children.RemoveAt(0);

                m_Host.Dispose();
                m_Host = null;
            }
          
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (!Design.IsDesignMode)
            {
                m_Host.Resize();
            }
        }
    }
}