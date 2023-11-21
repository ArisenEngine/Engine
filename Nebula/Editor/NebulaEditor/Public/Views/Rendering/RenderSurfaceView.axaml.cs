using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NebulaEngine.Rendering;
using System.Diagnostics;
using System.Drawing;
using System.Reflection.Metadata;
using Avalonia.Input;
using NebulaEditor;
using NebulaEditor.ViewModels;
using NebulaEngine.Debugger;
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
        
        private NebulaEngine.Rendering.RenderSurfaceHost m_Host = null;
        internal NebulaEngine.Rendering.SurfaceType SurfaceType;
        public bool IsSceneView = false;
        public Window ParentWindow;
        
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
                m_Host = new RenderSurfaceHost((int)RenderViewContainer.Bounds.Width, (int)RenderViewContainer.Bounds.Height, SurfaceType)
                {
                    Name = Name
                };
                
                RenderViewContainer.Children.Insert(0, m_Host);
            }
            
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            Logger.Log($"Focus on :{Name}");
        }

        private void OnRenderSurfaceViewUnloaded(object? sender, RoutedEventArgs e)
        {
            Debug.Assert(sender != null);
            Unloaded -= OnRenderSurfaceViewUnloaded;

            if (!Design.IsDesignMode)
            {
                RenderViewContainer.Children.RemoveAt(0);
            
                m_Host.Dispose();
                m_Host = null;
            }
          
        }

        // protected override void OnSizeChanged(SizeChangedEventArgs e)
        // {
        //     base.OnSizeChanged(e);
        //     if (m_Host != null)
        //     {
        //         m_Host.Resize();
        //     }
        // }
    }
}