using System;
// using System.Runtime.InteropServices;
// using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;
using System.IO;
using ArisenEngine.FileSystem;
using Avalonia;
using System.Reflection;
using ArisenEditorFramework.Core;
using ArisenEditor.Core.Models;

namespace ArisenEditor.GameDev
{
    public static partial class ProjectSolution
    {
        public static string INSTALLATION_ENV_VARIABLE = "ARISEN_ENGINE_ROOT";
        public static string InstallationRoot = string.Empty;
        // private static readonly string k_TemplateSuffix = @".template";
        private static readonly string k_Sln = @".sln";
        private static readonly string k_Csproj = @".csproj";
        
        internal static void OpenVisualStudio(string solutionFullPath)
        {
        }

        internal static void CloseVisualStudio()
        {
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
                    proj = string.Format(proj, "\"ArisenEditor\"", InstallationRoot + @"ArisenEditor.dll");
                    File.WriteAllText(targetFilePath, proj);
            
                    return true;
                }
            
                if (file.Name == @"Assembly-Runtime.csproj.template")
                {
                    string sourceFilePath = Path.Combine(sourceDir.FullName, file.Name);
                    string targetFilePath = Path.Combine(destinationDir.FullName, file.Name.Replace(".template",""));
                    string proj = File.ReadAllText(sourceFilePath);
                    proj = string.Format(proj, InstallationRoot + @"ArisenEngine.dll");
                    File.WriteAllText(targetFilePath, proj);
            
                    return true;
                }
            }
            


            return false;
        }

        internal static void CreateProjectSolution(ProjectMetadataBase template, string newProjectPath, string newProjectName)
        {
            // For templates, ProjectPath might be the folder containing the template files
            var sourcePath = Path.GetDirectoryName(template.ProjectPath) ?? template.ProjectPath;
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
        internal static bool ProjectValidation(ProjectMetadataBase project)
        {
            string projectDir = Path.GetDirectoryName(project.ProjectPath) ?? string.Empty;
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            {
                return false;
            }

            var slnFile = new FileInfo(Path.Combine(projectDir, project.Name + @".sln"));
            if (!slnFile.Exists)
            {
                return false;
            }

            var runtimeProj = new FileInfo(Path.Combine(projectDir, @"Assembly-Runtime.csproj"));
            if (!runtimeProj.Exists)
            {
                return false;
            }

            var editorProj = new FileInfo(Path.Combine(projectDir, @"Assembly-Editor.csproj"));
            if (!editorProj.Exists)
            {
                return false;
            }

            return true;
        }
    }
}
