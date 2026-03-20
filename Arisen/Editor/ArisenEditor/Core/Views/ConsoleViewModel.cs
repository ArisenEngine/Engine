using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using ArisenEditor.Models;
using ArisenEditor.Views;
using ArisenEditorFramework.Core;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using System.Collections.Generic;
using ArisenEditor.Core.Services;
using ArisenEngine.Core.Diagnostics;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace ArisenEditor.ViewModels;
using LogMessage = Logger.LogMessage;
using LogLevel = Logger.LogLevel;
public class ConsoleViewModel : EditorPanelBase, IDisposable
{
    public override string Title => "Console";
    public override string Id => "Console";
    public override object Content => new ConsoleView { DataContext = this };

    private ReadOnlyObservableCollection<MessageItemNode> m_Messages;
    

    private SourceList<MessageItemNode> m_SourceList = new SourceList<MessageItemNode>();

    internal ReadOnlyObservableCollection<MessageItemNode> Messages
    {
        get
        {
            if (Design.IsDesignMode)
            {
                return new ReadOnlyObservableCollection<MessageItemNode>(
                    new ObservableCollection<MessageItemNode>()
                    {
                        new MessageItemNode(new LogMessage(LogLevel.Error, " In design mode.", "000", "DesignMode", DateTime.Now, "trace"), LogSource.Editor),
                        new MessageItemNode(new LogMessage(LogLevel.Error, " In design mode.", "000", "DesignMode", DateTime.Now, "trace"), LogSource.Player),
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

    private bool m_ShowPlayerLogs = true;
    public bool ShowPlayerLogs
    {
        get => m_ShowPlayerLogs;
        set => this.RaiseAndSetIfChanged(ref m_ShowPlayerLogs, value);
    }

    private bool m_ShowEditorLogs = true;
    public bool ShowEditorLogs
    {
        get => m_ShowEditorLogs;
        set => this.RaiseAndSetIfChanged(ref m_ShowEditorLogs, value);
    }

    private MessageItemNode m_SelectedItem;

    internal MessageItemNode SelectedItem
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

    internal void OnAddMessage(LogMessage message, LogSource source = LogSource.Player)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            m_SourceList.Add(new MessageItemNode(message, source));
            
        }, DispatcherPriority.Background);
    }

    public System.Windows.Input.ICommand ClearCommand { get; }
    
    private readonly Subject<bool> m_CountChanged = new();
    private readonly CompositeDisposable m_Disposable = new();

    public ConsoleViewModel()
    {
        
        ClearCommand = ReactiveCommand.Create(Clear, outputScheduler: AvaloniaScheduler.Instance);
        
        var filter = new BehaviorSubject<Func<MessageItemNode, bool>>(Filter);
        
        m_SourceList
            .Connect()
            .Filter(filter)
            .Sort(SortExpressionComparer<MessageItemNode>.Descending(x => x.DateTime))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out m_Messages)
            .Subscribe(_=>filter.OnNext(Filter)).DisposeWith(m_Disposable);

        
        this.WhenAnyPropertyChanged(
                nameof(InfoChecked),
                nameof(LogChecked), 
                nameof(WarningChecked),
                nameof(ErrorChecked),
                nameof(ThreadChecked),
                nameof(SearchText),
                nameof(ShowPlayerLogs),
                nameof(ShowEditorLogs)
                )
            .Subscribe(_ => filter.OnNext(Filter))
            .DisposeWith(m_Disposable);
        
        EditorLogService.MessageAdded += OnEditorMessageAdded;
    }

    public void Clear()
    {
        m_SourceList.Clear();
    }
    
    private bool Filter(MessageItemNode messageItemNodes)
    {
        if (!ShowPlayerLogs && messageItemNodes.Source == LogSource.Player) return false;
        if (!ShowEditorLogs && messageItemNodes.Source == LogSource.Editor) return false;

        bool typeFilter = true;

        if (!ErrorChecked)
        {
            typeFilter &= !(((int)messageItemNodes.LogLevel & (int)Logger.LogLevel.Error) != 0);
        }
        
        if (!WarningChecked)
        {
            typeFilter &= !(((int)messageItemNodes.LogLevel & (int)Logger.LogLevel.Warning) != 0);
        }
        
        if (!InfoChecked)
        {
            typeFilter &= !(((int)messageItemNodes.LogLevel &(int)Logger.LogLevel.Info) != 0);
        }
        
        if (!LogChecked)
        {
            typeFilter &= !(((int)messageItemNodes.LogLevel & (int)Logger.LogLevel.Log) != 0);
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


    public override void Dispose()
    {
        base.Dispose();
        EditorLogService.MessageAdded -= OnEditorMessageAdded;
        m_SourceList.Dispose();
        m_Disposable.Dispose();
        m_CountChanged.Dispose();
    }

    private void OnEditorMessageAdded(Logger.LogMessage msg) => OnAddMessage(msg, LogSource.Editor);
}