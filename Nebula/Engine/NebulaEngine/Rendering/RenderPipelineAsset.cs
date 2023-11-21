using NebulaEngine.Debugger;
using Serialization.Interface;

namespace NebulaEngine.Rendering;

public abstract class RenderPipelineAsset : ISerializationCallbackReceiver
{

    internal RenderPipeline InternalCreatePipeline()
    {
        RenderPipeline pipeline = (RenderPipeline) null;
        
        try
        {
            pipeline = this.CreatePipeline();
        }
        catch (Exception ex)
        {
           Logger.Error(ex.Message);
           throw;
        }
        
        return pipeline;
        
    }
    
    
    /// <summary>
    ///   <para>Create a IRenderPipeline specific to this asset.</para>
    /// </summary>
    /// <returns>
    ///   <para>Created pipeline.</para>
    /// </returns>
    protected abstract RenderPipeline CreatePipeline();
    
    
    

    #region Serialization Hook

    protected abstract void BeforeSerialize();
    protected abstract void AfterDeserialize();
    
    public void OnBeforeSerialize()
    {
        BeforeSerialize();
    }

    public void OnAfterDeserialize()
    {
        AfterDeserialize();
    }

    #endregion
}