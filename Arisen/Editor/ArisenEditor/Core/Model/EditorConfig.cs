using ArisenEditorFramework.Core;
using YamlDotNet.Serialization;
using System.Collections.Generic;
using ArisenEngine.Core.Serialization;

namespace ArisenEditor.Models;

public class EditorConfig : ISerializationCallbackReceiver
{
    // TODO: get the installation location
    internal readonly static string EDITOR_CONFIG_PATH = "./configs/editor_config.yaml";

    internal static EditorConfig Instance { get; set; }

    [YamlMember]
    public List<ProjectMetadata> Projects { get; set; } = new List<ProjectMetadata>();

    [YamlMember]
    public string TemplatesPath { get; set; } = "./Templates/templateslist.yaml";

    public void OnAfterDeserialize()
    {
       if (Projects == null)
        {
            Projects = new List<ProjectMetadata>();
        }
    }

        public void OnBeforeSerialize()
        {
           
        }
    }
