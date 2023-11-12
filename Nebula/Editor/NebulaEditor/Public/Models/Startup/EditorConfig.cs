using Serialization.Interface;
using System;
using System.Collections.Generic;


namespace NebulaEditor.Models.Startup
{

    public class EditorConfig : ISerializationCallbackReceiver
    {
        // TODO: get the installation location
        public readonly static string EDITOR_CONFIG_PATH = "configs/editor_config.yaml";

        public static EditorConfig Instance { get; set; }

        public List<ProjectInfo> Projects { get; set; } = new List<ProjectInfo>();

        public string TemplatesPath { get; set; } = "./Templates/templateslist.yaml";

        public void OnAfterDeserialize()
        {
           if (Projects == null)
            {
                Projects = new List<ProjectInfo>();

            }

        }

        public void OnBeforeSerialize()
        {
           
        }
    }
}
