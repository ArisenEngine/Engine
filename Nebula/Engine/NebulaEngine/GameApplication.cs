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


    public class GameApplication
    {
        // TODO: constraint the gamecode.dll only has read access
        public static  string startupPath = string.Empty;
        public static  string dataPath = string.Empty;
        public static  string projectRoot = string.Empty;
        public static  string projectName = string.Empty;
        public static  bool isRunning = false;
        public static  bool isInEditor = false;
        public static  RuntimePlatform platform = RuntimePlatform.Unknow;


        #region Internal

        
        internal static bool IsDesignMode = false;

        #endregion
        
        public void Run(int width, int height)
        {
            
        }
    }
}
