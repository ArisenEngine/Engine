
using ArisenEngineTest.Shaders;
Console.WriteLine("###### Start Shader Lab Test ######");
ShaderProcessor.ParseShader("ShaderLabRes", "SimpleLit.shader");
Console.WriteLine("###### End Shader Lab Test ######");

ArisenEngine.Debug.Logger.Dispose();