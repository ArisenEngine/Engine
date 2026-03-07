using ArisenEditorFramework.Docking;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace EditorTest;

public class ConsoleWindow : IEditorWindow
{
    public string Id { get; set; } = "Console";
    public string Title { get; set; } = "Console";

    private readonly ObservableCollection<string> _logs = new();

    public ConsoleWindow()
    {
        ArisenEngine.Core.Diagnostics.Logger.MessageAdded += OnLogMessageAdded;
    }

    private void OnLogMessageAdded(ArisenEngine.Core.Diagnostics.Logger.LogMessage msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logs.Add($"[{msg.Time:HH:mm:ss}] [{msg.LogLevel}] {msg.Message}");
            if (_logs.Count > 1000) _logs.RemoveAt(0);
        });
    }

    public object GetContent()
    {
        return new ListBox
        {
            ItemsSource = _logs,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            FontSize = 12,
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            ItemTemplate = new Avalonia.Markup.Xaml.Templates.DataTemplate
            {
                DataType = typeof(string),
                Content = new TextBlock
                {
                    [!TextBlock.TextProperty] = new Avalonia.Data.Binding(".")
                }
            }
        };
    }

    public string SerializeState()
    {
        // For now, no persistent console state needed
        return string.Empty;
    }

    public void DeserializeState(string state)
    {
    }

    // Cleanup when window is "destroyed" if necessary
    public void Shutdown()
    {
        ArisenEngine.Core.Diagnostics.Logger.MessageAdded -= OnLogMessageAdded;
    }
}
