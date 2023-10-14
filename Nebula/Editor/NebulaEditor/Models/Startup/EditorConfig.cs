using Serialization.Interface;
using System;
using System.Collections.Generic;


namespace NebulaEditor.Models.Startup
{

    public class EditorConfig : ISerializationCallbackReceiver
    {
        public static EditorConfig Instance { get; set; }

        public List<ProjectInfo> Projects { get; set; } = new List<ProjectInfo>();

        public string TemplatesPath { get; set; } = "./Templates/templateslist.yaml";

        public void OnAfterDeserialize()
        {
           
        }

        public void OnBeforeSerialize()
        {
           
        }
    }
}
