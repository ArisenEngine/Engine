using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace ArisenEditor.Views;

public partial class HierarchyView : UserControl
{
    public HierarchyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var treeView = this.FindControl<TreeView>("MainTreeView");
        if (treeView != null)
        {
            treeView.AddHandler(PointerPressedEvent, OnTreeViewPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            treeView.AddHandler(PointerMovedEvent, OnTreeViewPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            treeView.AddHandler(DragDrop.DragOverEvent, OnMainTreeViewDragOver);
            treeView.AddHandler(DragDrop.DropEvent, OnMainTreeViewDrop);
        }
    }

    private Avalonia.Point m_DragStartPoint;
    private bool m_IsDragging;

    private void OnTreeViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (e.Source is Control control)
            {
                if (control.DataContext is ViewModels.EntityNodeViewModel entityNode)
                {
                    if (this.DataContext is ViewModels.HierarchyViewModel vm)
                        vm.SelectedItem = entityNode;
                }
                else if (control.DataContext is ViewModels.SceneNodeViewModel sceneNode)
                {
                    if (this.DataContext is ViewModels.HierarchyViewModel vm)
                        vm.SelectedItem = sceneNode;
                }
                else if (this.DataContext is ViewModels.HierarchyViewModel vw)
                {
                    vw.SelectedItem = null;
                }
            }
        }
        else if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            m_DragStartPoint = e.GetPosition(this);
            m_IsDragging = true;
        }
    }

    private async void OnTreeViewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!m_IsDragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            m_IsDragging = false;
            return;
        }

        var point = e.GetPosition(this);
        var diff = m_DragStartPoint - point;

        if (System.Math.Abs(diff.X) > 3 || System.Math.Abs(diff.Y) > 3)
        {
            m_IsDragging = false;
            
            if (e.Source is Control control && control.DataContext is ViewModels.EntityNodeViewModel draggedNode)
            {
                var dragData = new DataObject();
                dragData.Set("DraggedEntity", draggedNode);
                
                await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
            }
        }
    }

    private void OnMainTreeViewDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("DraggedEntity"))
        {
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnMainTreeViewDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("DraggedEntity") && e.Data.Get("DraggedEntity") is ViewModels.EntityNodeViewModel draggedNode)
        {
            ViewModels.EntityNodeViewModel? targetNode = null;
            if (e.Source is Control control)
            {
                targetNode = control.DataContext as ViewModels.EntityNodeViewModel;
            }

            if (this.DataContext is ViewModels.HierarchyViewModel vm)
            {
                vm.MoveEntity(draggedNode, targetNode);
            }
            e.Handled = true;
        }
    }

    private void OnEntityDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is ViewModels.EntityNodeViewModel node)
        {
            node.IsRenaming = true;
            e.Handled = true;
        }
    }

    private void OnRenameTextBoxPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Avalonia.Visual.IsVisibleProperty)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.IsVisible)
                {
                    // Hook handled events too, ensuring we ALWAYS force e.Handled=true on bubble
                    // so no global HotKey or parent TreeView intercepts it.
                    textBox.AddHandler(InputElement.KeyDownEvent, OnRenameTextBoxKeyDown, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
                    
                    // Delay focusing to ensure layout visually attaches the TextBox
                    Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }, System.TimeSpan.FromMilliseconds(50));
                }
                else
                {
                    textBox.RemoveHandler(InputElement.KeyDownEvent, OnRenameTextBoxKeyDown);
                }
            }
        }
    }

    private void OnRenameTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter || e.Key == Avalonia.Input.Key.Escape)
        {
            if (sender is TextBox textBox && textBox.DataContext is ViewModels.EntityNodeViewModel node)
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    node.DraftName = node.Name; // Force match so no command triggers on exit
                }
                node.IsRenaming = false;
                e.Handled = true;
            }
        }
        else if (e.Key == Avalonia.Input.Key.Up ||
                 e.Key == Avalonia.Input.Key.Down ||
                 e.Key == Avalonia.Input.Key.Left ||
                 e.Key == Avalonia.Input.Key.Right ||
                 e.Key == Avalonia.Input.Key.Back ||
                 e.Key == Avalonia.Input.Key.Delete ||
                 e.Key == Avalonia.Input.Key.Space ||
                 e.Key == Avalonia.Input.Key.Home ||
                 e.Key == Avalonia.Input.Key.End ||
                 e.Key == Avalonia.Input.Key.PageUp ||
                 e.Key == Avalonia.Input.Key.PageDown)
        {
            e.Handled = true;
        }
    }

    private void OnRenameTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ViewModels.EntityNodeViewModel node)
        {
            node.IsRenaming = false;
        }
    }
}
