using System;
using ArisenEngine.Rendering;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using ArisenEditor.ViewModels;

namespace ArisenEngine.Views.Rendering
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
        
        private RenderSurfaceHost m_Host = null;
        internal SurfaceType SurfaceType;
        
        public RenderSurfaceView()
        {
            InitializeComponent();
            Loaded -= OnRenderSurfaceViewLoaded;
            Loaded += OnRenderSurfaceViewLoaded;
            Unloaded -= OnRenderSurfaceViewUnloaded;
            Unloaded += OnRenderSurfaceViewUnloaded;
           
        }
        
        private void OnRenderSurfaceViewLoaded(object? sender, RoutedEventArgs e)
        {
            Debug.Logger.Assert(sender != null);

            Loaded -= OnRenderSurfaceViewLoaded;

            if (!Design.IsDesignMode)
            {
                m_Host = new RenderSurfaceHost((int)RenderViewContainer.Bounds.Width, (int)RenderViewContainer.Bounds.Height, SurfaceType)
                {
                    Name = Name
                };
                Console.WriteLine("Create Render Surface Host.");
                RenderViewContainer.Children.Insert(0, m_Host);
            }
            
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            Debug.Logger.Log($"Focus on :{Name}");
        }

        private void OnRenderSurfaceViewUnloaded(object? sender, RoutedEventArgs e)
        {
            Debug.Logger.Assert(sender != null);
            Unloaded -= OnRenderSurfaceViewUnloaded;

            if (!Design.IsDesignMode)
            {
                Console.WriteLine("Remove Render Surface Host.");
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