using ArisenEditorFramework.Docking;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace EditorTest;

public abstract class PlaceholderWindow : IEditorWindow
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    private readonly string _message;
    private readonly Color _bgColor;

    protected PlaceholderWindow(string message, Color bgColor)
    {
        _message = message;
        _bgColor = bgColor;
    }

    public object GetContent()
    {
        return new Border
        {
            Background = new SolidColorBrush(_bgColor),
            Child = new TextBlock
            {
                Text = _message,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18,
                Foreground = Brushes.White
            }
        };
    }

    public string SerializeState() => "{}";
    public void DeserializeState(string state) { }
}

public class HierarchyWindow : PlaceholderWindow
{
    public override string Id => "Hierarchy";
    public override string Title => "Hierarchy";
    public HierarchyWindow() : base("Scene Hierarchy View", Color.FromRgb(45, 45, 48)) { }
}

public class InspectorWindow : PlaceholderWindow
{
    public override string Id => "Inspector";
    public override string Title => "Inspector";
    public InspectorWindow() : base("Inspector / properties", Color.FromRgb(30, 30, 30)) { }
}

public class ViewportWindow : PlaceholderWindow
{
    public override string Id => "Viewport";
    public override string Title => "Viewport";
    public ViewportWindow() : base("3D Game Engine Viewport", Colors.Black) { }
}
