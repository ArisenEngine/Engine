using System;

namespace ArisenKernel.Lifecycle;

public struct EngineVersion : IComparable<EngineVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public static readonly EngineVersion Current = new EngineVersion(0, 1, 0);

    public EngineVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static bool TryParse(string version, out EngineVersion result)
    {
        result = default;
        if (string.IsNullOrEmpty(version)) return false;

        var parts = version.Split('.');
        if (parts.Length != 3) return false;

        if (int.TryParse(parts[0], out int major) &&
            int.TryParse(parts[1], out int minor) &&
            int.TryParse(parts[2], out int patch))
        {
            result = new EngineVersion(major, minor, patch);
            return true;
        }

        return false;
    }

    public int CompareTo(EngineVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool operator >(EngineVersion a, EngineVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(EngineVersion a, EngineVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(EngineVersion a, EngineVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(EngineVersion a, EngineVersion b) => a.CompareTo(b) <= 0;
}

