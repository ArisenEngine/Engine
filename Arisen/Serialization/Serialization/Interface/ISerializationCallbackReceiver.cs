

namespace Serialization.Interface
{
    public interface ISerializationCallbackReceiver
    {
        public void OnBeforeSerialize();

        public void OnAfterDeserialize();

    }
}
