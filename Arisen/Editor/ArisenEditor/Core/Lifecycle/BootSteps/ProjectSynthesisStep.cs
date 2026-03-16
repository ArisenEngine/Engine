using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArisenEditorFramework.Lifecycle;
using ArisenEngine;
using ArisenEngine.Core.Lifecycle;
using ArisenEditor.Core.Services;
using System;

namespace ArisenEditor.Core.Lifecycle.BootSteps;

public class ProjectSynthesisStep : IBootStep
{
    public string Name => "Project Synthesis";
    public string Description => "Loading project manifest and assembly metadata...";

    public async Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
    {
        // Set the project root for the engine
        var projectRoot = System.IO.Path.GetDirectoryName(context.ProjectPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            context.Success = false;
            context.ErrorMessage = $"Could not determine project root directory from path: {context.ProjectPath}";
            return;
        }
        ArisenApplication.s_ProjectRoot = projectRoot;
        
        // Load user settings early
        EditorProjectService.Instance.LoadUserSettings();
        
        // Ensure default scene exists and handle auto-loading
        EnsureDefaultSceneExists(projectRoot);

        await Task.Delay(1, cancellationToken);
    }

    private void EnsureDefaultSceneExists(string projectRoot)
    {
        string contentDir = Path.Combine(projectRoot, "Content");
        if (!Directory.Exists(contentDir))
        {
            Directory.CreateDirectory(contentDir);
        }

        string scenesDir = Path.Combine(contentDir, "Scenes");
        if (!Directory.Exists(scenesDir))
        {
            Directory.CreateDirectory(scenesDir);
        }

        // Check for any .arisen file in Content (recursive)
        var existingScenes = Directory.GetFiles(contentDir, "*.arisen", SearchOption.AllDirectories);
        if (existingScenes.Length == 0)
        {
            ArisenEngine.Core.Diagnostics.Logger.Log("[ProjectSynthesis] No scenes found. Generating default SampleScene.");
            
            // Create and save the default scene
            var newScene = SceneManagerService.Instance.CreateNewScene("SampleScene");
            
            // At this stage, ECS components aren't fully registered/accessible without
            // potentially requiring the Native Core, so we will just leave the scene completely empty.
            
            string defaultScenePath = Path.Combine(scenesDir, "SampleScene.arisen");
            
            ArisenEngine.Core.Serialization.SceneSerializer.SaveScene(defaultScenePath, newScene.Registry);
            
            // For a brand new project, auto-load the sample scene
            SceneManagerService.Instance.LoadScene(defaultScenePath);
        }
        else
        {
            // Scenes exist. Try loading the last opened scene by Guid
            Guid lastOpened = EditorProjectService.Instance.UserSettings.LastOpenedSceneGuid;
            bool loaded = false;

            if (lastOpened != Guid.Empty)
            {
                ArisenEngine.Core.Diagnostics.Logger.Log($"[ProjectSynthesis] Attempting to auto-load last scene (Guid: {lastOpened}).");
                loaded = SceneManagerService.Instance.LoadScene(lastOpened);
            }

            // Fallback: If no valid LastOpened Guid (or load failed), just load the first one we found.
            if (!loaded && existingScenes.Length > 0)
            {
                ArisenEngine.Core.Diagnostics.Logger.Log($"[ProjectSynthesis] Auto-loading fallback scene: {existingScenes[0]}");
                SceneManagerService.Instance.LoadScene(existingScenes[0]);
            }
        }
    }
}
