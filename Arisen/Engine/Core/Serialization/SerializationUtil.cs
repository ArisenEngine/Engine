using System.IO;
using YamlDotNet.Serialization;

namespace ArisenEngine.Core.Serialization;

public static class SerializationUtil
{
    public static void Serialize<T>(T serializableObject, string fullPath) where T : new()
    {
        FileInfo fi = new FileInfo(fullPath);
        if (fi.DirectoryName != null && !Directory.Exists(fi.DirectoryName))
        {
            Directory.CreateDirectory(fi.DirectoryName);
        }

        using StreamWriter streamWriter = File.CreateText(fi.FullName);
        Serializer serializer = new Serializer();

        if (serializableObject is ISerializationCallbackReceiver receiver)
        {
            receiver.OnBeforeSerialize();
        }

        serializer.Serialize(streamWriter, serializableObject);
    }

    public static T Deserialize<T>(string fullPath, bool serializeIfNotExist = true) where T : new()
    {
        if (!File.Exists(fullPath))
        {
            var result = new T();

            if (serializeIfNotExist)
            {
                Serialize(result, fullPath);
            }

            if (result is ISerializationCallbackReceiver receiver)
            {
                receiver.OnAfterDeserialize();
            }

            return result;
        }

        using StreamReader streamReader = File.OpenText(fullPath);
        Deserializer serializer = new Deserializer();
        T serializableObject = serializer.Deserialize<T>(streamReader);

        if (serializableObject is ISerializationCallbackReceiver callbackReceiver)
        {
            callbackReceiver.OnAfterDeserialize();
        }

        return serializableObject;
    }
}
