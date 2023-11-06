using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;
using System.IO;
using NebulaEditor.Models.Startup;
using EngineLib.FileSystem;

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

        public static bool HandleFiles(FileInfo file, DirectoryInfo sourceDir, DirectoryInfo destinationDir)
        {
       
            if (file.Extension == ".sln")
            {
                var fileName = destinationDir.Name + ".sln";

                string sourceFilePath = Path.Combine(sourceDir.FullName, file.Name);
                string targetFilePath = Path.Combine(destinationDir.FullName, fileName);

                string sln = File.ReadAllText(sourceFilePath);
                var runtimeGuid = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                var editorGuid = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
                var solutionGuid = "{" + Guid.NewGuid().ToString().ToUpper() + "}";

                sln = string.Format(sln, runtimeGuid, editorGuid, solutionGuid);

                File.WriteAllText(targetFilePath, sln);

                return true;
            }



            return false;
        }

        public static void CreateProjectSolution(ProjectInfo template, string newProjectPath, string newProjectName)
        {
            var sourcePath = Path.Combine (template.ProjectPath, template.ProjectName);
            var fullPath = Path.Combine(newProjectPath, newProjectName);
            DirectoryUtilities.CopyDirectoryRecursively(sourcePath, fullPath, HandleFiles);
        }
    }
}
