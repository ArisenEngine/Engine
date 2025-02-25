using System;
using ArisenEditor.Extensions.GameView;
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
        
        GameViewResolutionConfig m_ResolutionConfig;
        
        public RenderSurfaceView(GameViewResolutionConfig resolutionConfig = null)
        {
            m_ResolutionConfig = resolutionConfig;
            InitializeComponent();
            
            GameViewResolution.s_OnResolutionChanged -= OnGameViewResolutionChanged;
            GameViewResolution.s_OnResolutionChanged += OnGameViewResolutionChanged;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            if (!Design.IsDesignMode)
            {
                int finalWidth = (int)RenderViewContainer.Bounds.Width;
                int finalHeight = (int)RenderViewContainer.Bounds.Height;
                InitRenderViewSize(ref finalWidth, ref finalHeight);
                m_Host = new RenderSurfaceHost(finalWidth, finalHeight, SurfaceType)
                {
                    Name = Name
                };
                
                Console.WriteLine($"Create Render Surface Host: {SurfaceType}");
                RenderViewContainer.Children.Insert(0, m_Host);
            }

        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            
            if (!Design.IsDesignMode)
            {
                GameViewResolution.s_OnResolutionChanged -= OnGameViewResolutionChanged;
                Console.WriteLine($"Remove Render Surface Host: {SurfaceType}");
                RenderViewContainer.Children.RemoveAt(0);
            
                m_Host.Dispose();
                m_Host = null;
            }
            
        }

        void OnGameViewResolutionChanged(GameViewResolutionConfig resolutionConfig)
        {
            m_ResolutionConfig = resolutionConfig;
        }
        
        void InitRenderViewSize(ref int originalWidth, ref int originalHeight)
        {
            if (m_ResolutionConfig == null)
            {
                // TODO: log
                return;
            }

            float aspect = m_ResolutionConfig.width / (float)m_ResolutionConfig.height;
            
            if (m_ResolutionConfig.Orientation == GameViewOrientation.Landscape)
            {
                originalHeight = (int)(originalWidth / aspect);
            }
            else if (m_ResolutionConfig.Orientation == GameViewOrientation.Portrait)
            {
                originalWidth = (int)(originalHeight * aspect);
            }
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            Debug.Logger.Log($"Focus on :{Name}");
        }
        
    }
}