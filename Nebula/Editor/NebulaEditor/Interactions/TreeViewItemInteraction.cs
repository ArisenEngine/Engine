using Avalonia;
using Avalonia.Controls;
using NebulaEditor.Models;

namespace NebulaEditor.Interactions;

/// <summary>
/// reference = "https://github.com/AvaloniaUI/Avalonia/issues/11901"
/// </summary>
public class TreeViewItemInteraction : AvaloniaObject
{
    /// <summary>
    /// Defines the ListenToExpanded attached property
    /// </summary>
    public static readonly AttachedProperty<bool> ListenToExpandedProperty =
        AvaloniaProperty.RegisterAttached<TreeViewItemInteraction, TreeViewItem, bool>("ListenToExpanded");
    
    static TreeViewItemInteraction()
    {
        // ListenToExpandedProperty.Changed.Subscribe(x => OnListenToExpandedChanged(x.Sender, x.NewValue.GetValueOrDefault<bool>()));
    }
    
    private static void OnListenToExpandedChanged(AvaloniaObject  sender, bool value)
    {
        // if the property was set to true, add the needed event listener
        if (value && sender is TreeViewItem item)
        {
            item.PropertyChanged += TreeViewItemOnPropertyChanged;
        }
    }
    
    private static void TreeViewItemOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // if IsExpanded was changed, perform the needed actions on our node. You may want to extract a base-class or an interface
        if(e.Property == TreeViewItem.IsExpandedProperty)
        {
            TreeViewItem treeViewItem = (sender as TreeViewItem);
            (treeViewItem?.DataContext as TreeNode)?.OnExpandRequested(treeViewItem.IsExpanded);
        }
        
    }
    
    /// <summary>
    /// Set Accessor for Attached property ListenToExpanded
    /// </summary>
    public static void SetListenToExpanded(AvaloniaObject element, bool commandValue)
    {
        element.SetValue(ListenToExpandedProperty, commandValue);
    }

    /// <summary>
    /// GEt Accessor for Attached property ListenToExpanded
    /// </summary>
    public static bool GetListenToExpanded(AvaloniaObject element)
    {
        return element.GetValue(ListenToExpandedProperty);
    }
}