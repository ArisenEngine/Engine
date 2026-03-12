using System;

namespace ArisenEngine.Core.Serialization;

/// <summary>
/// Forces a property or field to be visible in the Inspector.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ShowInInspectorAttribute : Attribute { }

/// <summary>
/// Hides a property or field from the Inspector, even if it is public.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class HideInInspectorAttribute : Attribute { }

/// <summary>
/// Restricts a numeric property or field to a specific range, usually rendered as a slider in the Inspector.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class RangeAttribute : Attribute
{
    public float Min { get; }
    public float Max { get; }

    public RangeAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
}
