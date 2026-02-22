namespace ArisenEngine.Platform;

public abstract class MessageHandler : IMessageHandler
{
    public abstract bool NextFrame();
}

public interface IMessageHandler
{
    bool NextFrame();
}
