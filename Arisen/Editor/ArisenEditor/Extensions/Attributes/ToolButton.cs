using System;

namespace ArisenEditor.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ToolButton : System.Attribute
{
    public string content;
    public string icon;
    public ToolButton(string itemName, string icon = "")
    {
        this.content = itemName;
        this.icon = icon;
    }
}