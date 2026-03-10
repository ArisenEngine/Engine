using System;
using ArisenEditorFramework.Core;
using ArisenEditor.Core.Models;

namespace ArisenEditor.Core;

public class EditorProjectContext
{
    private static EditorProjectContext? _instance;
    public static EditorProjectContext Instance => _instance ?? throw new InvalidOperationException("Project context not initialized.");

    public EngineProjectMetadata CurrentProject { get; private set; }

    public static void Initialize(EngineProjectMetadata project)
    {
        _instance = new EditorProjectContext(project);
    }

    private EditorProjectContext(EngineProjectMetadata project)
    {
        CurrentProject = project;
    }
}
