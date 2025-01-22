using ArisenEngine.Rendering;

namespace ArisenEngine.Platforms;

internal abstract class MessageHandler
{
    public MessageHandler()
    {
    }
  
    public abstract bool NextFrame();
}