
using ArisenEngineTest.Shaders;
Console.WriteLine("###### Start Shader Lab Test ######");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "SimpleLit.shader");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "Lit.shader");
Console.WriteLine("###### End Shader Lab Test ######");

ArisenEngine.Debug.Logger.Dispose();