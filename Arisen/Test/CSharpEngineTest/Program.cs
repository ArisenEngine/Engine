
using ArisenEngineTest.Shaders;

ArisenEngine.Core.Diagnostics.Logger.Initialize();
ArisenEngine.Core.Diagnostics.Logger.Log("###### Start Shader Lab Test ######");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "SimpleLit.shader");
ShaderProcessor.ParseShader("ShaderLabRes/Packages/com.unity.render-pipelines.universal/Shaders", "Lit.shader");
ArisenEngine.Core.Diagnostics.Logger.Log("###### End Shader Lab Test ######");

ArisenEngine.Core.Diagnostics.Logger.Dispose();