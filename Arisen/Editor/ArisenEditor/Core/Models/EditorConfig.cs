using YamlDotNet.Serialization;
using System.Collections.Generic;
using ArisenEngine.Core.Serialization;
using ArisenEditor.Core.Models;

namespace ArisenEditor.Core.Models;

public class EditorConfig : ISerializationCallbackReceiver
{
    public readonly static string EDITOR_CONFIG_PATH = "./configs/editor_config.yaml";

    public static EditorConfig Instance { get; set; }

    [YamlMember]
    public List<EngineProjectMetadata> Projects { get; set; } = new List<EngineProjectMetadata>();

    [YamlMember]
    public string TemplatesPath { get; set; } = "./Templates/templateslist.yaml";

    public void OnAfterDeserialize()
    {
       if (Projects == null)
        {
            Projects = new List<EngineProjectMetadata>();
        }
    }

    public void OnBeforeSerialize()
    {
       
    }
}
