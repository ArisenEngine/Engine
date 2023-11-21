using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEngine
{
    public enum RuntimePlatform
    {
        Unknow,
        Windows,
        Linux,
        MacOS,
        Android,
        IOS,
        Browser,
        XBox,
        PS5
    }


    public class NebulaApplication
    {
        static NebulaApplication()
        {
            NebulaInstance.AllSurfacesDestroyed += OnSurfacesAllClosed;
        }
        
        private static void OnSurfacesAllClosed()
        {
            NebulaInstance.AllSurfacesDestroyed -= OnSurfacesAllClosed;
            NebulaInstance.End();
        }
        
        #region Internal

        
        internal static  bool s_IsDesignMode = false;
        internal static  string s_StartupPath = string.Empty;
        internal static  string s_DataPath = string.Empty;
        internal static  string s_ProjectRoot = string.Empty;
        internal static  string s_ProjectName = string.Empty;
        internal static  bool s_IsRunning = false;
        internal static  bool s_IsInEditor = false;
        internal static  RuntimePlatform s_Platform = RuntimePlatform.Windows;

        #endregion

        #region Public 
        
        /// <summary>
        /// Windowed run
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="name"></param>
        public static void Run(int width, int height, string name = "")
        {
            NebulaInstance.RegisterSurface(name, width, height);
            NebulaInstance.Run(name);
        }
        
        /// <summary>
        /// Full-screen run
        /// </summary>
        /// <param name="name"></param>
        public static void Run(string name = "")
        {
            NebulaInstance.RegisterSurface(name);
            NebulaInstance.Run(name);
        }

        #endregion

        
    }
}
