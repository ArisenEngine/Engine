using System;
using DynamicData;

namespace NebulaEditor.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MenuItem : System.Attribute
{
    public static readonly string kMenuItemSeparators = "/";

    public string menuItem;
    public MenuItem(string itemName)
    {
        this.menuItem = itemName;
    }
}