using NebulaEngine.Rendering;

namespace NebulaEngine.Platforms;

internal abstract class MessageHandler
{
    public MessageHandler()
    {
    }
  
    public abstract bool NextFrame();
}