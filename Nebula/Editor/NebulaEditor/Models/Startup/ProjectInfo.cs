using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.Models.Startup
{
    /// <summary>
    /// project info 
    /// </summary>
    public class ProjectInfo
    {
        /// <summary>
        /// the project location
        /// </summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>
        /// project name
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;
        /// <summary>
        /// project description
        /// </summary>
        public string Desc { get; set; } = string.Empty;
        /// <summary>
        /// preview image url
        /// </summary>
        public string PreviewImageURL { get; set; } = string.Empty;

        public string IconURL { get; set; } = string.Empty;
        /// <summary>
        /// engine version project currently used
        /// </summary>
        public string EngineVersion { get; set; } = string.Empty;

        public ProjectInfo()
        {

        }

    }
}
