using ArisenEditorFramework.Docking;
using Avalonia;
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
            Content = "Hot Reload",
            Background = new SolidColorBrush(Color.Parse("#4CAF50")),
            Foreground = Brushes.White
        };
        reloadBtn.Click += (s, e) => (MainWindow.Instance as MainWindow)?.SimulateHotReload();

        var defaultLayoutBtn = new Button { Content = "Default Layout" };
        defaultLayoutBtn.Click += (s, e) => (MainWindow.Instance as MainWindow)?.LayoutManager?.ApplyPreset("Default");

        var wideLayoutBtn = new Button { Content = "Wide Layout" };
        wideLayoutBtn.Click += (s, e) => (MainWindow.Instance as MainWindow)?.LayoutManager?.ApplyPreset("Wide");

        var tallLayoutBtn = new Button { Content = "Tall Layout" };
        tallLayoutBtn.Click += (s, e) => (MainWindow.Instance as MainWindow)?.LayoutManager?.ApplyPreset("Tall");

        stack.Children.Add(openBtn);
        stack.Children.Add(reloadBtn);
        stack.Children.Add(new Separator { Width = 1, Height = 20, Background = Brushes.Gray, Margin = new Thickness(4, 0) });
        stack.Children.Add(defaultLayoutBtn);
        stack.Children.Add(wideLayoutBtn);
        stack.Children.Add(tallLayoutBtn);

        return stack;
    }

    public string SerializeState() => string.Empty;
    public void DeserializeState(string state) { }
    public void Shutdown() { }
}
