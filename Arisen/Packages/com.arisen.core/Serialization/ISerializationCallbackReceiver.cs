namespace ArisenEngine.Core.Serialization;

public interface ISerializationCallbackReceiver
{
    public void OnBeforeSerialize();
    public void OnAfterDeserialize();
}
