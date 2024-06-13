using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;
using System.IO;
using NebulaEditor.Models.Startup;
using NebulaEngine.FileSystem;
using Avalonia;
using System.Reflection;

namespace NebulaEditor.GameDev
{
    public static partial class ProjectSolution
    {
        private static readonly string k_TemplateSuffix = @".template";
        private static readonly string k_Sln = @".sln";
        private static readonly string k_Csproj = @".csproj";
        
        private static object m_VisualStudioInstance = null;
        private static EnvDTE80.DTE2 DTE => (m_VisualStudioInstance as EnvDTE80.DTE2);

        // use visual studio 2022
        private static readonly string k_ProgID = @"VisualStudio.DTE.17.0";
        

        // TODO: find a probably way to handle IDE in cross-platform
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);
        
        internal static void OpenVisualStudio(string solutionFullPath)
        {
            IRunningObjectTable rot = null;
            IEnumMoniker monikerTable = null;
            IBindCtx bindCtx = null;    

            try
            {
                if (m_VisualStudioInstance == null)
                {
                    // find and open visual studio
                    var hResult = GetRunningObjectTable(0, out rot);
                    if (hResult < 0 || rot == null) throw new COMException($"GetRunningObjectTable() returned HRESULT: {hResult:X8}");

                    rot.EnumRunning(out monikerTable);

                    hResult = CreateBindCtx(0, out bindCtx);
                    if (hResult < 0 || rot == null) throw new COMException($"CreateBindCtx() returned HRESULT: {hResult:X8}");

                    IMoniker[] currentMoniker = new IMoniker[1];

                    while (monikerTable.Next(1, currentMoniker, IntPtr.Zero) == 0)
                    {
                        string name = string.Empty;
                        currentMoniker[0]?.GetDisplayName(bindCtx, null, out name);
                        if (name.Contains(k_ProgID))
                        {
                            hResult = rot.GetObject(currentMoniker[0], out object obj);
                            if (hResult < 0 || obj == null) throw new COMException($"Running object table's GetObject() returned HRESULT: {hResult:X8}");

                            EnvDTE80.DTE2 dte = obj as EnvDTE80.DTE2;

                            var solutionName = dte.Solution.FullName;
                            if (solutionName.ToString() == solutionFullPath)
                            {
                                m_VisualStudioInstance = obj;
                                break;
                            }
                        }
                    }

                    if (m_VisualStudioInstance == null)
                    {
                        Type visualStudioType = Type.GetTypeFromProgID(k_ProgID, true);
                        m_VisualStudioInstance = Activator.CreateInstance(visualStudioType);
                        DTE.Solution.Open(solutionFullPath);
                    }

                } else
                {
                    if (DTE.Solution.FullName != solutionFullPath)
                    {
                        DTE.Solution.Open(solutionFullPath);
                    } 
                }

                DTE.MainWindow.Activate();
                DTE.MainWindow.Visible = true;

            } 
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                if (monikerTable != null) Marshal.ReleaseComObject(monikerTable);
                if (rot != null) Marshal.ReleaseComObject(rot);
                if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
            }
        }

        internal static void CloseVisualStudio()
        {
            if (DTE != null)
            {
                if (DTE.Solution.IsOpen)
                {
                    DTE.ExecuteCommand("File.SaveAll");
                    DTE.Solution.Close(true);
                }

                m_VisualStudioInstance = null;
                DTE.Quit();
            }
        }

        internal static bool HandleFiles(FileInfo file, DirectoryInfo sourceDir, DirectoryInfo destinationDir)
        {
       
            if (file.Name.Contains(k_Sln))
            {
                var fileName = destinationDir.Name + k_Sln;

                string sourceFilePath = Path.Combine(sourceDir.FullName, file.Name);
                string targetFilePath = Path.Combine(destinationDir.FullName, fileName);

                string sln = File.ReadAllText(sourceFilePath);
                var runtimeGuid = @"{" + Guid.NewGuid().ToString().ToUpper() + @"}";
                var editorGuid = @"{" + Guid.NewGuid().ToString().ToUpper() + @"}";
                var solutionGuid = @"{" + Guid.NewGuid().ToString().ToUpper() + @"}";

                sln = string.Format(sln, runtimeGuid, editorGuid, solutionGuid);

                File.WriteAllText(targetFilePath, sln);

                return true;
            }

            
            if (file.Name.Contains(k_Csproj))
            {
                if (file.Name == @"Assembly-Editor.csproj.template")
                {
                    string sourceFilePath = Path.Combine(sourceDir.FullName, file.Name);
                    string targetFilePath = Path.Combine(destinationDir.FullName, file.Name.Replace(".template",""));
                    string proj = File.ReadAllText(sourceFilePath);
                    proj = string.Format(proj, "\"NebulaEditor\"", InstallationRoot + @"NebulaEditor.dll");
                    File.WriteAllText(targetFilePath, proj);

                    return true;
                }

                if (file.Name == @"Assembly-Runtime.csproj.template")
                {
                    string sourceFilePath = Path.Combine(sourceDir.FullName, file.Name);
                    string targetFilePath = Path.Combine(destinationDir.FullName, file.Name.Replace(".template",""));
                    string proj = File.ReadAllText(sourceFilePath);
                    proj = string.Format(proj, InstallationRoot + @"NebulaEngine.dll");
                    File.WriteAllText(targetFilePath, proj);

                    return true;
                }
            }



            return false;
        }

        internal static void CreateProjectSolution(ProjectInfo template, string newProjectPath, string newProjectName)
        {
            var sourcePath = Path.Combine (template.ProjectPath, template.ProjectName);
            var fullPath = Path.Combine(newProjectPath, newProjectName);
            FileSystemUtilities.CopyDirectoryRecursively(sourcePath, fullPath, HandleFiles);
        }

        //public static void BuildProject(GameBuilder.TargetPlatform target)
        //{

        //}

        /// <summary>
        /// Validation a project
        /// </summary>
        /// <returns></returns>
        internal static bool ProjectValidation(ProjectInfo project)
        {
            if (!Directory.Exists(project.ProjectPath))
            {
                return false;
            }

            var projectRoot = Path.Combine(project.ProjectPath, project.ProjectName);
            if (!Directory.Exists(projectRoot))
            {
                return false;
            }

            var slnFile = new FileInfo(Path.Combine(projectRoot, project.ProjectName + @".sln"));
            if (!slnFile.Exists)
            {
                return false;
            }

            var runtimeProj = new FileInfo(Path.Combine(projectRoot, @"Assembly-Runtime.csproj"));
            if (!runtimeProj.Exists)
            {
                return false;
            }

            var editorProj = new FileInfo(Path.Combine(projectRoot, @"Assembly-Editor.csproj"));
            if (!editorProj.Exists)
            {
                return false;
            }

            return true;
        }
    }
}
