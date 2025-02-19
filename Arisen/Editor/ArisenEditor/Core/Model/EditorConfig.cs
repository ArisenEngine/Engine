using Serialization.Interface;
using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;


namespace ArisenEditor.Models.Startup
{

    public class EditorConfig : ISerializationCallbackReceiver
    {
        // TODO: get the installation location
        internal readonly static string EDITOR_CONFIG_PATH = "./configs/editor_config.yaml";

        internal static EditorConfig Instance { get; set; }

        [YamlMember]
        public List<ProjectInfo> Projects { get; set; } = new List<ProjectInfo>();

        [YamlMember]
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
