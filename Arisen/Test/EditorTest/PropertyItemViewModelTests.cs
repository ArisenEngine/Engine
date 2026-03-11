using Xunit;
using System.ComponentModel;
using ArisenEditorFramework.Inspector;

namespace EditorTest;

public class PropertyItemViewModelTests
{
    private class TestTarget : INotifyPropertyChanged
    {
        private string _name = "Default";
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int ReadOnlyValue => 42;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Value_Get_ReturnsPropertyValue()
    {
        var target = new TestTarget();
        var prop = typeof(TestTarget).GetProperty(nameof(TestTarget.Name))!;
        var vm = new PropertyItemViewModel(target, prop);

        Assert.Equal("Default", vm.Value);
    }

    [Fact]
    public void Value_Set_UpdatesTargetProperty()
    {
        var target = new TestTarget();
        var prop = typeof(TestTarget).GetProperty(nameof(TestTarget.Name))!;
        var vm = new PropertyItemViewModel(target, prop);

        vm.Value = "NewValue";

        Assert.Equal("NewValue", target.Name);
    }

    [Fact]
    public void Dispose_UnsubscribesFromPropertyChanged()
    {
        var target = new TestTarget();
        var prop = typeof(TestTarget).GetProperty(nameof(TestTarget.Name))!;
        var vm = new PropertyItemViewModel(target, prop);
        
        bool notified = false;
        vm.PropertyChanged += (_, _) => notified = true;

        // Before dispose: changes to target should notify vm
        target.Name = "Changed";
        Assert.True(notified);

        // After dispose: changes to target should NOT notify vm
        vm.Dispose();
        notified = false;
        target.Name = "ChangedAgain";
        Assert.False(notified);
    }

    [Fact]
    public void IsReadOnly_TrueForReadOnlyProperty()
    {
        var target = new TestTarget();
        var prop = typeof(TestTarget).GetProperty(nameof(TestTarget.ReadOnlyValue))!;
        var vm = new PropertyItemViewModel(target, prop);

        Assert.True(vm.IsReadOnly);
    }

    [Fact]
    public void PropertyType_ReflectsActualType()
    {
        var target = new TestTarget();
        var prop = typeof(TestTarget).GetProperty(nameof(TestTarget.Name))!;
        var vm = new PropertyItemViewModel(target, prop);

        Assert.Equal(typeof(string), vm.PropertyType);
    }
}
