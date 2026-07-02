using System.IO;
using System.Text.Json;

namespace ArisenBuildTool.Utils;

public static class ManifestJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    public static T? Deserialize<T>(Stream stream)
    {
        return JsonSerializer.Deserialize<T>(stream, SerializerOptions);
    }

    public static T? DeserializeFile<T>(string path)
    {
        string json = File.ReadAllText(path);
        return Deserialize<T>(json);
    }
}
