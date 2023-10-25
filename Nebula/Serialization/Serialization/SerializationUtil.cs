using Serialization.Interface;
using YamlDotNet.Serialization;

namespace Serialization
{
    public static class SerializationUtil
    {
        // TODO: maybe some class can't not be serialized
        public static void Serialize<T>(T serializableObject, string fullPath) where T : new()
        {
            FileInfo fi = new FileInfo(fullPath);
            if (!Directory.Exists(fi.DirectoryName))
            {
                Directory.CreateDirectory(fi.DirectoryName);
            }

            StreamWriter streamWriter = File.CreateText(fi.FullName);
            Serializer serializer = new Serializer();
            if (serializableObject is ISerializationCallbackReceiver)
            {
                (serializableObject as ISerializationCallbackReceiver)?.OnBeforeSerialize();
            }
            serializer.Serialize(streamWriter, serializableObject);
            streamWriter.Close();
        }

        // TODO: private member will not be serialize
        public static T Deserialize<T>(string fullPath, bool serializeIfNotExist = true) where T : new()
        {
            if (!File.Exists(fullPath))
            {
                var result = new T();

                if (serializeIfNotExist)
                {
                    Serialize(result, fullPath);
                }
                if (result is ISerializationCallbackReceiver)
                {
                    (result as ISerializationCallbackReceiver)?.OnAfterDeserialize();
                }
                    
                return result;
            }

            StreamReader streamReader = File.OpenText(fullPath);
            Deserializer serializer = new Deserializer();
            T serializableObject = serializer.Deserialize<T>(streamReader);
            if (serializableObject is ISerializationCallbackReceiver)
            {
                (serializableObject as ISerializationCallbackReceiver)?.OnAfterDeserialize();
            }
            streamReader.Close();
            return serializableObject;
        }
    }
}
