
using ArisenEngineTest.Shaders;

ArisenEngine.Debugger.Logger.Initialize();
ArisenEngine.Debugger.Logger.Log("###### Start Shader Lab Test ######");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "SimpleLit.shader");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "Lit.shader");
ArisenEngine.Debugger.Logger.Log("###### End Shader Lab Test ######");

ArisenEngine.Debugger.Logger.Dispose();