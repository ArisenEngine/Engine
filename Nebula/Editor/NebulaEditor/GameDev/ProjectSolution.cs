using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;

namespace NebulaEditor.GameDev
{
    public static class ProjectSolution
    {
        private static object m_VisualStudioInstance = null;
        
        
        // use visual studio 2022
        private static readonly string m_ProgID = "VisualStudio.DTE.17.4";

        // TODO: find a probably way to handle IDE in cross-platform
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);

        public static void OpenVisualStudio(string solutionPath)
        {
            IRunningObjectTable rot = null;
            IEnumMoniker monikerTable = null;
            try
            {
                if (m_VisualStudioInstance == null)
                {

                    if (m_VisualStudioInstance == null)
                    {
                        Type visualStudioType = Type.GetType(m_ProgID, true);
                        m_VisualStudioInstance = Activator.CreateInstance(visualStudioType);

                    }

                }


            } 
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public static void CreateProjectSolution(string fullPath)
        {

        }
    }
}
