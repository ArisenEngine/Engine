using ArisenEngine.ShaderLab;

namespace ArisenEngineTest.Shaders;

public class ShaderProcessor
{
    public static void ParseShader(string path, string fileName)
    {
        var fullPath = Path.Combine(path, fileName);
        if (File.Exists(fullPath))
        {
            var shaderContent = File.ReadAllText(fullPath);
            var shaderLabParser = new ShaderLabParser(shaderContent);
            var shaderLabShader = shaderLabParser.ParseGraphicsShader();
            var metaPath = Path.Combine(path, fileName + ".meta");
            Serialization.SerializationUtil.Serialize(shaderLabShader, metaPath);
            Console.WriteLine($"Parse shader:{fileName}, output:{metaPath}");
        }
    }
}