using System;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component to store entity names for the Editor hierarchy.
/// Uses fixed char buffers to avoid managed string memory pressure in the ECS tight loops.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct NameComponent : IComponent
{
    public const int MAX_LENGTH = 64;
    private fixed char m_Value[MAX_LENGTH];
    
    public string Name
    {
        get
        {
            fixed (char* ptr = m_Value)
            {
                return new string(ptr);
            }
        }
        set
        {
            if (value == null)
            {
                m_Value[0] = '\0';
                return;
            }

            int len = System.Math.Min(value.Length, MAX_LENGTH - 1);
            fixed (char* ptr = m_Value)
            {
                for (int i = 0; i < len; i++)
                {
                    ptr[i] = value[i];
                }
                ptr[len] = '\0'; // Null terminator
            }
        }
    }
}
