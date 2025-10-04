
using ArisenEngineTest.Shaders;
ArisenEngine.Debug.Logger.Log("###### Start Shader Lab Test ######");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "SimpleLit.shader");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "Lit.shader");
ArisenEngine.Debug.Logger.Log("###### End Shader Lab Test ######");

ArisenEngine.Debug.Logger.Dispose();