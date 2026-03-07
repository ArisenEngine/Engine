using ArisenEditorFramework.Docking;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace EditorTest;

public class MockCustomWindow : IEditorWindow
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public MockCustomWindow()
    {
    }

    public object GetContent()
    {
        var textBox = new TextBox 
        { 
            Watermark = "Type something, then click simulate hot reload...",
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
            [!TextBox.TextProperty] = new Avalonia.Data.Binding(nameof(Text)) { Source = this, Mode = Avalonia.Data.BindingMode.TwoWay }
        };
        
        return new StackPanel
        {
            Margin = new Avalonia.Thickness(10),
            Children = 
            {
                new TextBlock { Text = "This is a custom tool window." },
                new TextBlock { 
                    Text = "Any state typed below will survive an ALC Hot Reload.", 
                    TextWrapping = TextWrapping.Wrap, 
                    Foreground = Brushes.Gray 
                },
                textBox
            }
        };
    }

    public string SerializeState()
    {
        return Text;
    }

    public void DeserializeState(string state)
    {
        Text = state;
    }
}
