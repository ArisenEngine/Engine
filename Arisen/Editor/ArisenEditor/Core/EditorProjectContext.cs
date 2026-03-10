using System;
using ArisenEditorFramework.Core;

namespace ArisenEditor.Core;

public class EditorProjectContext
{
    private static EditorProjectContext? _instance;
    public static EditorProjectContext Instance => _instance ?? throw new InvalidOperationException("Project context not initialized.");

    public ProjectMetadata CurrentProject { get; private set; }

    public static void Initialize(ProjectMetadata project)
    {
        _instance = new EditorProjectContext(project);
    }

    private EditorProjectContext(ProjectMetadata project)
    {
        CurrentProject = project;
    }
}
