using ArisenEngine.Core.Lifecycle;
using ArisenEditor.Core.Services;
using ArisenEditorFramework.Core;
using ReactiveUI;
using Avalonia.Controls;

namespace ArisenEditor.ViewModels;

internal class ProjectSettingsViewModel : EditorPanelBase
{
    public override string Title => "Project Settings";
    public override string Id => "ProjectSettingsViewModel";
    public override object Content => new TextBlock { Text = "Project Settings Placeholder" };

    private string m_ProjectName = string.Empty;
    public string ProjectName
    {
        get => m_ProjectName;
        set 
        {
            this.RaiseAndSetIfChanged(ref m_ProjectName, value);
            EditorProjectService.Instance.SetProjectName(value);
        }
    }

    private string m_EngineVersion = string.Empty;
    public string EngineVersion
    {
        get => m_EngineVersion;
        set => this.RaiseAndSetIfChanged(ref m_EngineVersion, value);
    }

    internal ProjectSettingsViewModel()
    {
        var manifest = EditorProjectService.Instance.ActiveProject;
        if (manifest != null)
        {
            m_ProjectName = manifest.Name;
            m_EngineVersion = manifest.EngineVersion;
        }
    }
}
