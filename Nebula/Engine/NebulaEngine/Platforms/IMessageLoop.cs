namespace NebulaEngine.Platforms;

public interface IMessageLoop : IDisposable
{
    bool NextFrame();
}