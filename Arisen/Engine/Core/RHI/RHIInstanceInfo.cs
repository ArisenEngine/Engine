namespace ArisenEngine.Core.RHI;

public struct RHIInstanceInfo
{
    public string Name;
    public string EngineName;
    public bool ValidationLayer;
    public uint Variant;
    public uint Major, Minor, Patch;
    public uint AppMajor, AppMinor, AppPatch;
    public uint EngineMajor, EngineMinor, EnginePatch;
    public uint MaxFramesInFlight;
}
