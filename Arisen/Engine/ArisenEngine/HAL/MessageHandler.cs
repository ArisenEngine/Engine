using ArisenEngine.Rendering;

namespace ArisenEngine.HAL;

internal abstract class MessageHandler
{
    public MessageHandler()
    {
    }
  
    public abstract bool NextFrame();
}