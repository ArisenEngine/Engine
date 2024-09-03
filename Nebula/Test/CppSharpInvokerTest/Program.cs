// See https://aka.ms/new-console-template for more information

using NebulaEngine.Debug;

class Program
{
    static void Main()
    {
        if (!Logger.Instance.Initialize())
        {
            throw new Exception("Logger init failed.");
        }
        
        Logger.Instance.SetServerityLevel(Logger.LogLevel.Log);
        
        Logger.Instance.Log("aaa", "bbb", "ccc");
        
        Logger.Shutdown();
        
        Logger.Instance.Dispose();
    }
}