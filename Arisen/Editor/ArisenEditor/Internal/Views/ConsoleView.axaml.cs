using System.Reactive.Disposables;
using System.Threading.Tasks;
using ArisenEditor.Models;
using ArisenEditor.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ArisenEngine.Debugger;

namespace ArisenEditor.Views;
using LogMessage = ArisenEngine.Debug.Logger.LogMessage;
using Logger = ArisenEngine.Debug.Logger;

public partial class ConsoleView : UserControl
{
    private ConsoleViewModel m_ViewModel = null;
    public ConsoleView()
    {
        InitializeComponent();
        m_ViewModel = new ConsoleViewModel();
        DataContext = m_ViewModel;
    }

    private void OnLogMessageAdd(LogMessage message)
    {
        m_ViewModel?.OnAddMessage(message);
    }

    private void OnLogMessageCleared()
    {
        m_ViewModel?.OnMessageClear();
    }
    
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        m_ViewModel?.Clear();
        
        Logger.MessageAdded += OnLogMessageAdd;
        Logger.MessageCleared += OnLogMessageCleared;

        #region Test

        
        // int logCount = 0;
        // Task.Run(async () =>
        // {
        //     while (logCount < 10)
        //     {
        //         ++logCount;
        //         Logger.Log($"Log:{logCount}");
        //         Logger.Info($"Info:{logCount}");
        //         Logger.Warning($"Warning:{logCount}");
        //         Logger.Error($"Error:{logCount}");
        //         await Task.Delay(10);
        //     }
        // });
        //     
        // int logCount2 = 0;
        // Task.Run(async () =>
        // {
        //     while (logCount < 10)
        //     {
        //         ++logCount2;
        //         Logger.Log($"Log11111:{logCount2}");
        //         Logger.Info($"Info:1111{logCount2}");
        //         Logger.Warning($"Warning111:{logCount2}");
        //         Logger.Error($"Error111:{logCount2}");
        //         await Task.Delay(10);
        //     }
        // });
        //
        // Dispatcher.UIThread.Invoke(() =>
        // {
        //     Logger.Log($"Log : UI Thread ");
        //     Logger.Info($"Info : UI Thread ");
        //     Logger.Warning($"Warning : UI Thread ");
        //     Logger.Error($"Error: UI Thread ");
        // });
        //

        #endregion
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        
        Logger.MessageAdded -= OnLogMessageAdd;
        Logger.MessageCleared -= OnLogMessageCleared;
        m_ViewModel?.Dispose();
    }

    private void LogDataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (m_ViewModel != null)
        {
            m_ViewModel.SelectedItem = ((DataGrid) e.Source)?.SelectedItem as MessageItemNode;
        }
    }
}