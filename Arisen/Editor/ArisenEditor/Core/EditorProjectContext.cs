using System;
using System.Threading;
using ArisenEditorFramework.Core;
using ArisenEditor.Core.Models;

namespace ArisenEditor.Core;

public class EditorProjectContext
{
    private static EditorProjectContext? _instance;
    public static EditorProjectContext Instance => _instance ?? throw new InvalidOperationException(
        "EditorProjectContext.Instance accessed before Initialize() was called. " +
        "Ensure the bootstrap sequence has completed before accessing project context.");

    public EngineProjectMetadata CurrentProject { get; private set; }

    public static void Initialize(EngineProjectMetadata project)
    {
        var newContext = new EditorProjectContext(project);
        Interlocked.CompareExchange(ref _instance, newContext, null);
    }

    private EditorProjectContext(EngineProjectMetadata project)
    {
        CurrentProject = project;
    }
}
