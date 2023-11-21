using Serialization.Interface;

namespace NebulaEngine.ProjectSettings;

public class ProjectSettings : ISerializationCallbackReceiver
{
    public string defaultPipelineAssetPath = "";
    
    public void OnBeforeSerialize()
    {
        
    }

    public void OnAfterDeserialize()
    {
        
    }
}