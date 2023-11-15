
using System.Collections.ObjectModel;
using Avalonia.Threading;
using DynamicData;
using NebulaEditor.Models;
using NebulaEngine.Debugger;
using ReactiveUI;

namespace NebulaEditor.ViewModels;

public class ConsoleViewModel : ViewModelBase
{

    public ObservableCollection<MessageItemNode> Messages { get; set; } = new ObservableCollection<MessageItemNode>()
    {
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "",0)),
        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "",0)),
    };

    private int m_SelectedIndex = 0;

    public int SelectedIndex
    {
        get { return m_SelectedIndex; }
        set
        {
            this.RaiseAndSetIfChanged(ref m_SelectedIndex, value);
        }
    }

    public void OnAddMessage(Logger.LogMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Messages.Add(new MessageItemNode(message));
            
        });
    }
}