using Xunit;
using ArisenEditorFramework.Hierarchy;

namespace EditorTest;

public class HierarchyViewModelTests
{
    [Fact]
    public void AddItem_AppearsInItems()
    {
        var vm = new HierarchyViewModel();
        var item = new HierarchyItemViewModel { Name = "TestNode" };

        vm.Items.Add(item);

        Assert.Contains(item, vm.Items);
    }

    [Fact]
    public void DeleteItem_RemovesFromRootItems()
    {
        var vm = new HierarchyViewModel();
        var item = new HierarchyItemViewModel { Name = "TestNode" };
        
        // Manually hook up deletion request like the ViewModel would in CreateNewItem
        // but we'll use a private method or just a lambda for simplicity here
        item.RequestDelete += (s, i) => vm.Items.Remove(i);
        
        vm.Items.Add(item);

        // Simulate the delete command
        item.DeleteCommand!.Execute(null);

        Assert.DoesNotContain(item, vm.Items);
    }

    [Fact]
    public void DeleteItem_RemovesFromParentChildren()
    {
        var vm = new HierarchyViewModel();
        var parent = new HierarchyItemViewModel { Name = "Parent" };
        var child = new HierarchyItemViewModel { Name = "Child", Parent = parent };
        
        child.RequestDelete += (s, i) => i.Parent?.Children.Remove(i);
        
        parent.Children.Add(child);
        vm.Items.Add(parent);

        // Simulate the delete command on the child
        child.DeleteCommand!.Execute(null);

        Assert.DoesNotContain(child, parent.Children);
    }

    [Fact]
    public void DeleteItem_ClearsSelectedIfDeleted()
    {
        var vm = new HierarchyViewModel();
        var item = new HierarchyItemViewModel { Name = "TestNode" };
        
        // We need to use the actual handler from the VM to test selection clearing logic
        // The VM hooks this up in CreateNewItem, but we can just use the public OnItemDeletedRequest if it was public...
        // It's private. Let's use the AddRootItemCommand to get a properly hooked up item if possible, 
        // or just test the VM's behavior via its commands.
        
        vm.AddRootItemCommand.Execute(null);
        var createdItem = vm.Items[0];
        vm.SelectedItem = createdItem;

        createdItem.DeleteCommand!.Execute(null);

        Assert.Null(vm.SelectedItem);
    }
}
