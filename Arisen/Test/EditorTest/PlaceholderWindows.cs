using ArisenEditorFramework.Docking;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace EditorTest;

public abstract class PlaceholderWindow : IEditorWindow
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    private readonly string _message;
    private readonly Color _bgColor;

    protected PlaceholderWindow(string message, Color bgColor)
    {
        _message = message;
        _bgColor = bgColor;
    }

    public object GetContent()
    {
        return new Border
        {
            Background = new SolidColorBrush(_bgColor),
            Child = new TextBlock
            {
                Text = _message,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18,
                Foreground = Brushes.White
            }
        };
    }

    public string SerializeState() => "{}";
    public void DeserializeState(string state) { }
}

public class HierarchyWindow : IEditorWindow
{
    public string Id => "Hierarchy";
    public string Title => "Hierarchy";
    
    public ArisenEditorFramework.Hierarchy.HierarchyViewModel ViewModel { get; }
    
    // Quick static event to share selection in this sandbox environment
    public static event System.EventHandler<ArisenEditorFramework.Hierarchy.IHierarchyItem?>? GlobalSelectionChanged;

    public HierarchyWindow()
    {
        ViewModel = new ArisenEditorFramework.Hierarchy.HierarchyViewModel();
        
        ViewModel.SelectedItemChanged += (sender, item) => GlobalSelectionChanged?.Invoke(this, item);
        
        // Populate dummy scene data
        var rootItem = new ArisenEditorFramework.Hierarchy.HierarchyItemViewModel { Name = "Scene Root", IsExpanded = true };
        
        var cameraItem = new ArisenEditorFramework.Hierarchy.HierarchyItemViewModel { Name = "Main Camera", Parent = rootItem };
        var lightItem = new ArisenEditorFramework.Hierarchy.HierarchyItemViewModel { Name = "Directional Light", Parent = rootItem };
        var environmentNode = new ArisenEditorFramework.Hierarchy.HierarchyItemViewModel { Name = "Environment", Parent = rootItem, IsExpanded = true };
        
        var mesh1 = new ArisenEditorFramework.Hierarchy.HierarchyItemViewModel { Name = "Terrain Mesh", Parent = environmentNode };
        var mesh2 = new ArisenEditorFramework.Hierarchy.HierarchyItemViewModel { Name = "Water Plane", Parent = environmentNode };

        environmentNode.Children.Add(mesh1);
        environmentNode.Children.Add(mesh2);
        
        rootItem.Children.Add(cameraItem);
        rootItem.Children.Add(lightItem);
        rootItem.Children.Add(environmentNode);

        ViewModel.Items.Add(rootItem);
    }

    public object GetContent()
    {
        return new ArisenEditorFramework.Hierarchy.HierarchyControl
        {
            DataContext = ViewModel
        };
    }
    
    public string SerializeState() => "{}";
    public void DeserializeState(string state) { }
}

public class InspectorWindow : IEditorWindow
{
    public string Id => "Inspector";
    public string Title => "Inspector";
    
    private readonly ArisenEditorFramework.Inspector.InspectorViewModel _viewModel;

    public InspectorWindow() 
    {
        _viewModel = new ArisenEditorFramework.Inspector.InspectorViewModel();
        
        // Listen to the Hierarchy selection changes
        HierarchyWindow.GlobalSelectionChanged += (sender, item) => 
        {
            _viewModel.TargetObject = item;
        };
    }

    public object GetContent()
    {
        return new ArisenEditorFramework.Inspector.InspectorControl
        {
            DataContext = _viewModel
        };
    }
    
    public string SerializeState() => "{}";
    public void DeserializeState(string state) { }
}

public class ViewportWindow : PlaceholderWindow
{
    public override string Id => "Viewport";
    public override string Title => "Viewport";
    public ViewportWindow() : base("3D Game Engine Viewport", Colors.Black) { }
}
