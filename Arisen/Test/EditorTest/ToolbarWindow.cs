using ArisenEditorFramework.Docking;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;

namespace EditorTest;

public class ToolbarWindow : IEditorWindow
{
    public string Id { get; set; } = "Toolbar";
    public string Title { get; set; } = "Toolbar";

    public object GetContent()
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(8)
        };

        var openBtn = new Button { Content = "Open Custom Tool" };
        openBtn.Click += (s, e) => (MainWindow.Instance as MainWindow)?.OpenTool();

        var reloadBtn = new Button 
        { 
            Content = "Simulate Hot Reload Sequence",
            Background = new SolidColorBrush(Color.Parse("#4CAF50")),
            Foreground = Brushes.White
        };
        reloadBtn.Click += (s, e) => (MainWindow.Instance as MainWindow)?.SimulateHotReload();

        stack.Children.Add(openBtn);
        stack.Children.Add(reloadBtn);

        return stack;
    }

    public string SerializeState() => string.Empty;
    public void DeserializeState(string state) { }
    public void Shutdown() { }
}
