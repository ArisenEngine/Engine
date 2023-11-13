namespace NebulaEditor.Interfaces;

public interface IAssetsProcess
{
    public void OnPreProcess(string[] path);

    public void OnPostProcess();
}