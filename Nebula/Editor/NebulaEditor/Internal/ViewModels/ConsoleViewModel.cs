using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NebulaEditor.Models;
using NebulaEngine.Debugger;
using ReactiveUI;

namespace NebulaEditor.ViewModels;

public class ConsoleViewModel : ViewModelBase, IDisposable
{
    private ReadOnlyObservableCollection<MessageItemNode> m_Messages;
    

    private SourceList<MessageItemNode> m_SourceList = new SourceList<MessageItemNode>();

    public ReadOnlyObservableCollection<MessageItemNode> Messages
    {
        get
        {
            if (Design.IsDesignMode)
            {
                return new ReadOnlyObservableCollection<MessageItemNode>(
                    new ObservableCollection<MessageItemNode>()
                    {
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 10223,
                            "KDFJF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 103423,
                            "DASDAF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 10342,
                            "AFSF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 10324,
                            "FASF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 1032,
                            "FGDSD", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 102342,
                            "FSDF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 102342,
                            "GDFGS", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 102342,
                            "DSFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 10234,
                            "DFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 2342,
                            "DFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 234,
                            "DSFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 243,
                            "DSFGDH", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),

                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 10223,
                            "KDFJF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 103423,
                            "DASDAF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 10342,
                            "AFSF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 10324,
                            "FASF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 1032,
                            "FGDSD", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 102342,
                            "FSDF", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 102342,
                            "GDFGS", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 102342,
                            "DSFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 10234,
                            "DFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 2342,
                            "DFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Error, "aaa", "", "", 0, 234,
                            "DSFG", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                        new MessageItemNode(new Logger.LogMessage(Logger.MessageType.Info, "aaa", "", "", 0, 243,
                            "DSFGDH", "aaaaaa \n bbbbbbbbbbb \n cccccccccccccccccc")),
                    });
            }
            return m_Messages;
        }
    }

    private int m_SelectedIndex;

    public int SelectedIndex
    {
        get { return m_SelectedIndex; }
        set { this.RaiseAndSetIfChanged(ref m_SelectedIndex, value); }
    }

    private bool m_ThreadChecked;

    public bool ThreadChecked
    {
        get
        {
            return m_ThreadChecked;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_ThreadChecked, value);
        }
    }
    
    private bool m_InfoChecked = true;

    public bool InfoChecked
    {
        get
        {
            return m_InfoChecked;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_InfoChecked, value);
        }
    }
    
    private bool m_LogChecked = true;

    public bool LogChecked
    {
        get
        {
            return m_LogChecked;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_LogChecked, value);
        }
    }
    
    private bool m_WarningChecked = true;
    public bool WarningChecked
    {
        get
        {
            return m_WarningChecked;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_WarningChecked, value);
        }
    }

    private MessageItemNode m_SelectedItem;

    public MessageItemNode SelectedItem
    {
        get
        {
            return m_SelectedItem;
        }
        set
        {
            m_SelectedItem = value;
            StackTrace = m_SelectedItem?.StackTrace;
        }
    }

    private string m_StackTrace;

    public string StackTrace
    {
        get
        {
            return m_StackTrace;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_StackTrace, value);
        }
    }
    
    private bool m_ErrorChecked = true;
    public bool ErrorChecked
    {
        get
        {
            return m_ErrorChecked;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_ErrorChecked, value);
        }
    }
    
    private string m_SearchText;
    public string SearchText
    {
        get
        {
            return m_SearchText;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_SearchText, value);
        }
    }

    internal void OnAddMessage(Logger.LogMessage message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            m_SourceList.Add(new MessageItemNode(message));
            
        }, DispatcherPriority.Background);
    }

    public ICommand? ClearCommand { get; }
    
    private readonly Subject<bool> m_CountChanged = new();
    private readonly CompositeDisposable m_Disposable = new();

    public ConsoleViewModel() : base()
    {
        
        ClearCommand = ReactiveCommand.Create(Clear);
        
        var filter = new BehaviorSubject<Func<MessageItemNode, bool>>(Filter);
        
        m_SourceList
            .Connect()
            .Filter(filter)
            .Sort(SortExpressionComparer<MessageItemNode>.Descending(x => x.DateTime))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out m_Messages)
            .Subscribe(_=>filter.OnNext(Filter)).DisposeWith(m_Disposable);

        
        this.WhenAnyPropertyChanged(
                nameof(InfoChecked),
                nameof(LogChecked), 
                nameof(WarningChecked),
                nameof(ErrorChecked),
                nameof(ThreadChecked),
                nameof(SearchText)
                )
            .Subscribe(_ => filter.OnNext(Filter))
            .DisposeWith(m_Disposable);
    }

    public void Clear()
    {
        m_SourceList.Clear();
    }
    
    private bool Filter(MessageItemNode messageItemNodes)
    {
        bool typeFilter = true;

        if (!ErrorChecked)
        {
            typeFilter &= !(((int)messageItemNodes.MessageType & (int)Logger.MessageType.Error) != 0);
        }
        
        if (!WarningChecked)
        {
            typeFilter &= !(((int)messageItemNodes.MessageType & (int)Logger.MessageType.Warning) != 0);
        }
        
        if (!InfoChecked)
        {
            typeFilter &= !(((int)messageItemNodes.MessageType &(int)Logger.MessageType.Info) != 0);
        }
        
        if (!LogChecked)
        {
            typeFilter &= !(((int)messageItemNodes.MessageType & (int)Logger.MessageType.Log) != 0);
        }

        bool searchFilter = true;

        if (!string.IsNullOrEmpty(SearchText))
        {
            searchFilter = messageItemNodes.FullText.Contains(SearchText);
        }
        

        return searchFilter && typeFilter;
    }

    public void OnMessageClear()
    {
        this.Clear();
    }


    public void Dispose()
    {
        m_SourceList.Dispose();
        m_Disposable.Dispose();
        m_CountChanged.Dispose();
    }
}