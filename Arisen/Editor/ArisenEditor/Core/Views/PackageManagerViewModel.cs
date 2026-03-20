using System.Collections.ObjectModel;
using ArisenEngine.Core.Lifecycle;
using ArisenEditorFramework.Core;
using ReactiveUI;
using System.Linq;
using Avalonia.Controls;

namespace ArisenEditor.ViewModels;

internal class PackageManagerViewModel : EditorPanelBase
{
    public override string Title => "Package Manager";
    public override string Id => "PackageManagerViewModel";
    public override object Content => new TextBlock { Text = "Package Manager Placeholder" };

    private ObservableCollection<ArisenPackageInfo> m_Packages = new();
    public ObservableCollection<ArisenPackageInfo> Packages
    {
        get => m_Packages;
        set => this.RaiseAndSetIfChanged(ref m_Packages, value);
    }

    internal PackageManagerViewModel()
    {
        RefreshPackages();
    }

    public void RefreshPackages()
    {
        var packageSubsystem = EngineKernel.Instance.GetSubsystem<PackageSubsystem>();
        if (packageSubsystem != null)
        {
            Packages = new ObservableCollection<ArisenPackageInfo>(packageSubsystem.GetAllPackages());
        }
    }
}
