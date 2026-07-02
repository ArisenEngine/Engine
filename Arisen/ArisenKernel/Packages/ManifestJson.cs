using System.IO;
using System.Text.Json;

namespace ArisenKernel.Packages;

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

    public static T? DeserializeFile<T>(string path)
    {
        string json = File.ReadAllText(path);
        return Deserialize<T>(json);
    }

    public static JsonDocument ParseDocumentFile(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
    }
}
