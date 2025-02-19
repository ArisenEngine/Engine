using Avalonia;
using Avalonia.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace ArisenEditor.Models.Startup
{
    /// <summary>
    /// project info 
    /// </summary>
    public class ProjectInfo
    {
        internal static string s_ContentFolderName = "Content";
        internal static string s_DependenciesFolderName = "Dependencies";
        internal static string s_HidenEngineFolderName = ".arisen";
        internal static string s_ProjectSettingsFolderName = "ProjectSettings";
        internal static string s_UserSettingsFolderName = "UserSettings";
        
        
        internal static readonly string DefaultProjectPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\ArisenProjects\";
       
        /// <summary>
        /// project name
        /// </summary>
        [YamlMember]
        public string ProjectName { get; set; } = "NewProject";
        /// <summary>
        /// the project location
        /// </summary>
        [YamlMember]
        public string ProjectPath { get; set; } = DefaultProjectPath;

        /// <summary>
        /// project description
        /// </summary>
        [YamlMember]
        public string Desc { get; set; } = "New arisen project";
        /// <summary>
        /// preview image url
        /// </summary>
        [YamlMember]
        public string PreviewImageURL { get; set; } = @"/Assets/LOGO.png";
        [YamlMember]
        public string IconURL { get; set; } = @"/Assets/Icons/game-console.png";
        /// <summary>
        /// engine version project currently used
        /// </summary>
        [YamlMember]
        public string EngineVersion { get; set; } = "1.0.0";
        [YamlMember]
        public string LastEditTime { get; set; } = "just now";

        internal static List<string> Folders = new List<string>()
        {
            s_HidenEngineFolderName,
            s_ContentFolderName,
            s_DependenciesFolderName,
            s_ProjectSettingsFolderName,
            s_UserSettingsFolderName
        };

        public ProjectInfo()
        {
            
        }

        internal string ProjectFullPath() => System.IO.Path.Combine(ProjectPath, ProjectName);
    }
}
