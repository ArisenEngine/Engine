using ArisenEditorFramework.Docking;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace EditorTest;

public class MockCustomWindow : IEditorWindow
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    private readonly TextBox _textBox;
    private readonly StackPanel _panel;

    public MockCustomWindow()
    {
        _textBox = new TextBox 
        { 
            Watermark = "Type something, then click simulate hot reload...",
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };
        
        _panel = new StackPanel
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
                _textBox
            }
        };
    }

    public object GetContent()
    {
        return _panel;
    }

    public string SerializeState()
    {
        return _textBox.Text ?? string.Empty;
    }

    public void DeserializeState(string state)
    {
        _textBox.Text = state;
    }
}
