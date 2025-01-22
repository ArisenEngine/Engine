using System;
using DynamicData;

namespace ArisenEditor.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MenuItem : System.Attribute
{
    public static readonly string kMenuItemSeparators = "/";

    public string menuItem;
    public bool seperator;
    public MenuItem(string itemName, bool seperator = false)
    {
        this.menuItem = itemName;
        this.seperator = seperator;
    }
}