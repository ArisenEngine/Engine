using System.Runtime.InteropServices;

namespace ArisenBinding.RHI
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RHIHandle
    {
        public uint Index;
        public uint Generation;

        public bool IsValid => Index != 0xFFFFFFFFu;

        public static RHIHandle Invalid => new RHIHandle { Index = 0xFFFFFFFFu, Generation = 0 };

        public override bool Equals(object obj)
        {
            if (obj is RHIHandle other)
            {
                return Index == other.Index && Generation == other.Generation;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Generation);
        }

        public static bool operator ==(RHIHandle left, RHIHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RHIHandle left, RHIHandle right)
        {
            return !(left == right);
        }
    }
}
