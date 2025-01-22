using Avalonia;
using Avalonia.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArisenEditor.Models.Startup
{
    /// <summary>
    /// project info 
    /// </summary>
    public class ProjectInfo
    {
        public static readonly string DefaultProjectPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\ArisenProjects\";
       
        /// <summary>
        /// project name
        /// </summary>
        public string ProjectName { get; set; } = "NewProject";
        /// <summary>
        /// the project location
        /// </summary>
        public string ProjectPath { get; set; } = DefaultProjectPath;

        /// <summary>
        /// project description
        /// </summary>
        public string Desc { get; set; } = "New arisen project";
        /// <summary>
        /// preview image url
        /// </summary>
        public string PreviewImageURL { get; set; } = @"/Assets/LOGO.png";

        public string IconURL { get; set; } = @"/Assets/Icons/game-console.png";
        /// <summary>
        /// engine version project currently used
        /// </summary>
        public string EngineVersion { get; set; } = "1.0.0";

        public string LastEditTime { get; set; } = "just now";

        public static List<string> Folders = new List<string>()
        {
            ".arisen",
            "Assets",
            "Packages",
            "ProjectSettings",
            "UserSettings"
        };

        public ProjectInfo()
        {
            //ProjectName = "New Project";
        }

        public string ProjectFullPath() => System.IO.Path.Combine(ProjectPath, ProjectName);
    }
}
