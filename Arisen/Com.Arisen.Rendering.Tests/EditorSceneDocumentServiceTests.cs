using System.Numerics;
using ArisenEditor.Core.Commands;
using ArisenEditor.Core.Services;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Automation;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorSceneDocumentServiceTests
{
    private static readonly Guid s_EditableEntityGuid =
        Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public void RuntimeSnapshotPreview_ActivatesAtFrameBoundaryWithoutWritingSource()
    {
        using var context = new SceneContext();
        string diskSource = File.ReadAllText(context.FirstScenePath);
        var edit = SceneAssetLoader.UpdateEntityTransformSource(
            context.FirstScenePath,
            diskSource,
            s_EditableEntityGuid,
            TransformAt(4, 5, 6));
        Assert.True(edit.Success, edit.Diagnostic);

        RuntimeSceneLoadReport? report = null;
        context.RuntimeScenes.SceneLoadCompleted += value => report = value;
        context.RuntimeScenes.RequestSceneLoad(new SceneSourceSnapshot(
            context.FirstScene,
            context.FirstScenePath,
            edit.UpdatedSource,
            42));

        Assert.Equal(diskSource, File.ReadAllText(context.FirstScenePath));
        Assert.Equal(new Vector3(1, 2, 3), context.ActivePosition);

        var processed = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(processed.HasValue);
        Assert.True(processed.Value.Success, processed.Value.Diagnostic);
        Assert.Equal(new Vector3(4, 5, 6), context.ActivePosition);
        Assert.Equal(42, context.RuntimeScenes.ActiveScene!.SourceRevision);
        Assert.NotNull(report);
        Assert.True(report!.Result.Success, report.Result.Diagnostic);
        Assert.Equal(42, report.SourceRevision);
        Assert.Equal(diskSource, File.ReadAllText(context.FirstScenePath));
    }

    [Fact]
    public void RuntimeSnapshotPreview_FailurePreservesActiveWorldAndPublishesReport()
    {
        using var context = new SceneContext();
        var previousWorld = context.ActiveWorld;
        RuntimeSceneLoadReport? report = null;
        context.RuntimeScenes.SceneLoadCompleted += value => report = value;

        context.RuntimeScenes.RequestSceneLoad(new SceneSourceSnapshot(
            context.FirstScene,
            context.FirstScenePath,
            "Name: Broken Preview\nEntities: []\n",
            7));
        var processed = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();

        Assert.True(processed.HasValue);
        Assert.False(processed.Value.Success);
        Assert.Same(previousWorld, context.ActiveWorld);
        Assert.Same(previousWorld, context.RuntimeScenes.ActiveScene!.EntityManager);
        Assert.NotNull(report);
        Assert.False(report!.Result.Success);
        Assert.Equal(7, report.SourceRevision);
    }

    [Fact]
    public void DocumentTransformEdit_PreviewsWithoutWritingUntilAtomicSave()
    {
        using var context = new SceneContext();
        string savedSource = File.ReadAllText(context.FirstScenePath);

        var edit = context.Documents.ApplyEntityTransform(s_EditableEntityGuid, TransformAt(4, 5, 6));

        Assert.True(edit.Success, edit.Diagnostic);
        Assert.True(context.Documents.Current!.IsDirty);
        Assert.Equal(savedSource, context.Documents.Current.SavedSource);
        Assert.NotEqual(savedSource, context.Documents.Current.WorkingSource);
        Assert.Equal(savedSource, File.ReadAllText(context.FirstScenePath));
        Assert.Equal(new Vector3(1, 2, 3), context.ActivePosition);

        var preview = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(preview.HasValue);
        Assert.True(preview.Value.Success, preview.Value.Diagnostic);
        Assert.Equal(new Vector3(4, 5, 6), context.ActivePosition);

        var save = context.Documents.Save();

        Assert.True(save.Success, save.Diagnostic);
        Assert.False(context.Documents.Current!.IsDirty);
        Assert.Equal(context.Documents.Current.WorkingSource, File.ReadAllText(context.FirstScenePath));
        Assert.Empty(Directory.GetFiles(
            context.AssetsRoot,
            $".{Path.GetFileName(context.FirstScenePath)}.*.tmp"));
    }

    [Fact]
    public void DocumentTransformEdit_TargetsStableGuidAfterWorkingSourceReorder()
    {
        using var context = new SceneContext();
        Guid decoyGuid = Guid.Parse("30000000-0000-0000-0000-000000000002");
        string reordered = $$"""
            Version: 2
            Name: Reordered Scene
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            - Guid: {{decoyGuid:D}}
              Name: Decoy
              Transform:
                Position: { X: 90, Y: 91, Z: 92 }
            - Guid: {{s_EditableEntityGuid:D}}
              Name: Editable Entity
              Transform:
                Position: { X: 1, Y: 2, Z: 3 }
            """;

        var reorder = context.Documents.ApplyWorkingSource(reordered);
        Assert.True(reorder.Success, reorder.Diagnostic);
        Assert.Equal(decoyGuid, context.Documents.Current!.Inspection.Entities[0].AuthoringGuid);

        var edit = context.Documents.ApplyEntityTransform(
            s_EditableEntityGuid,
            TransformAt(7, 8, 9));
        Assert.True(edit.Success, edit.Diagnostic);
        SceneEntityInspection editable = Assert.Single(
            context.Documents.Current!.Inspection.Entities,
            entity => entity.AuthoringGuid == s_EditableEntityGuid);
        SceneEntityInspection decoy = Assert.Single(
            context.Documents.Current.Inspection.Entities,
            entity => entity.AuthoringGuid == decoyGuid);
        Assert.Equal(new Vector3(7, 8, 9), editable.Transform.Position);
        Assert.Equal(new Vector3(90, 91, 92), decoy.Transform.Position);

        SceneLoadResult? preview =
            context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(preview.HasValue);
        Assert.True(preview.Value.Success, preview.Value.Diagnostic);
        Assert.Equal(new Vector3(7, 8, 9), context.ActivePosition);
    }

    [Fact]
    public void TransformCommand_UndoRestoresExactSavedSourceAndRedoRestoresEdit()
    {
        using var context = new SceneContext();
        string savedSource = context.Documents.Current!.SavedSource;
        var oldTransform = context.Documents.Current.Inspection.Entities[0].Transform;
        var newTransform = TransformAt(7, 8, 9);
        var command = new ModifySceneAssetTransformCommand(
            context.Documents,
            s_EditableEntityGuid,
            "Editable Entity",
            oldTransform,
            newTransform);

        context.Commands.Execute(command);
        string editedSource = context.Documents.Current!.WorkingSource;
        Assert.True(context.Documents.Current.IsDirty);
        Assert.NotEqual(savedSource, editedSource);

        context.Commands.Undo();

        Assert.False(context.Documents.Current!.IsDirty);
        Assert.Equal(savedSource, context.Documents.Current.WorkingSource);
        var undoPreview = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(undoPreview.HasValue);
        Assert.True(undoPreview.Value.Success, undoPreview.Value.Diagnostic);
        Assert.Equal(new Vector3(1, 2, 3), context.ActivePosition);

        context.Commands.Redo();

        Assert.True(context.Documents.Current!.IsDirty);
        Assert.Equal(editedSource, context.Documents.Current.WorkingSource);
        var redoPreview = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(redoPreview.HasValue);
        Assert.True(redoPreview.Value.Success, redoPreview.Value.Diagnostic);
        Assert.Equal(new Vector3(7, 8, 9), context.ActivePosition);
    }

    [Fact]
    public void ExternalSourceChange_BlocksSaveAndDiscardReloadsDisk()
    {
        using var context = new SceneContext();
        var edit = context.Documents.ApplyEntityTransform(s_EditableEntityGuid, TransformAt(4, 5, 6));
        Assert.True(edit.Success, edit.Diagnostic);

        string externalSource = CreateSceneSource("Externally Changed", 9, 10, 11);
        File.WriteAllText(context.FirstScenePath, externalSource);

        var save = context.Documents.Save();

        Assert.False(save.Success);
        Assert.True(context.Documents.Current!.IsDirty);
        Assert.True(context.Documents.Current.HasExternalChanges);
        Assert.Equal(externalSource, File.ReadAllText(context.FirstScenePath));

        var discard = context.Documents.DiscardChanges();
        Assert.True(discard.Success, discard.Diagnostic);
        Assert.False(context.Documents.Current!.IsDirty);
        Assert.False(context.Documents.Current.HasExternalChanges);
        Assert.Equal(externalSource, context.Documents.Current.WorkingSource);

        var reload = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(reload.HasValue);
        Assert.True(reload.Value.Success, reload.Value.Diagnostic);
        Assert.Equal(new Vector3(9, 10, 11), context.ActivePosition);
    }

    [Fact]
    public void DirtyDocument_BlocksSceneSwitchUntilChangesAreDiscarded()
    {
        using var context = new SceneContext();
        var edit = context.Documents.ApplyEntityTransform(s_EditableEntityGuid, TransformAt(4, 5, 6));
        Assert.True(edit.Success, edit.Diagnostic);

        var blocked = context.Documents.RequestOpenScene(context.SecondScene);

        Assert.False(blocked.Success);
        Assert.True(blocked.RequiresUserResolution);
        Assert.Equal(context.FirstScene.Guid, context.Documents.Current!.Scene.Guid);

        var discard = context.Documents.DiscardChanges();
        Assert.True(discard.Success, discard.Diagnostic);
        var queued = context.Documents.RequestOpenScene(context.SecondScene);
        Assert.True(queued.Success, queued.Diagnostic);
        Assert.Equal(context.FirstScene.Guid, context.Documents.Current!.Scene.Guid);

        var activation = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(activation.HasValue);
        Assert.True(activation.Value.Success, activation.Value.Diagnostic);
        Assert.Equal(context.SecondScene.Guid, context.Documents.Current!.Scene.Guid);
        Assert.Equal("Second Scene", context.Documents.Current.Name);
        Assert.Equal(new Vector3(-1, -2, -3), context.ActivePosition);
    }

    [Fact]
    public void PendingSceneSwitch_CannotBeOverwrittenByEditOrSave()
    {
        using var context = new SceneContext();
        var queued = context.Documents.RequestOpenScene(context.SecondScene);
        Assert.True(queued.Success, queued.Diagnostic);

        var edit = context.Documents.ApplyEntityTransform(s_EditableEntityGuid, TransformAt(4, 5, 6));
        var save = context.Documents.Save();

        Assert.False(edit.Success);
        Assert.Contains("pending", edit.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(save.Success);
        Assert.Contains("pending", save.Diagnostic, StringComparison.OrdinalIgnoreCase);

        var activation = context.RuntimeScenes.ProcessPendingSceneLoadAtFrameBoundary();
        Assert.True(activation.HasValue);
        Assert.True(activation.Value.Success, activation.Value.Diagnostic);
        Assert.Equal(context.SecondScene.Guid, context.Documents.Current!.Scene.Guid);
        Assert.Equal(new Vector3(-1, -2, -3), context.ActivePosition);
    }

    private static SceneTransformInspection TransformAt(float x, float y, float z)
    {
        return new SceneTransformInspection(
            new Vector3(x, y, z),
            Quaternion.Identity,
            Vector3.One);
    }

    private static string CreateSceneSource(string name, float x, float y, float z)
    {
        return $"""
            Version: 2
            Name: {name}
            ComponentSchemas:
            - TypeId: 1
              Name: Transform
              Version: 1
              Required: true
            Entities:
            - Guid: {s_EditableEntityGuid:D}
              Name: Editable Entity
              Transform:
                Position:
                  X: {x}
                  Y: {y}
                  Z: {z}
            """;
    }

    private sealed class SceneContext : IDisposable
    {
        private readonly string m_Root;

        public SceneContext()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenEditorSceneDocumentTests",
                Guid.NewGuid().ToString("N"));
            AssetsRoot = Path.Combine(m_Root, "Assets");
            Directory.CreateDirectory(AssetsRoot);

            FirstScenePath = Path.Combine(AssetsRoot, "First.arisenscene");
            string secondScenePath = Path.Combine(AssetsRoot, "Second.arisenscene");
            File.WriteAllText(FirstScenePath, CreateSceneSource("First Scene", 1, 2, 3));
            File.WriteAllText(secondScenePath, CreateSceneSource("Second Scene", -1, -2, -3));

            var firstGuid = Guid.Parse("10000000-0000-0000-0000-000000000001");
            var secondGuid = Guid.Parse("20000000-0000-0000-0000-000000000002");
            FirstScene = new AssetRef<SceneSourceAsset>(firstGuid, "Scene", "com.arisen.test");
            SecondScene = new AssetRef<SceneSourceAsset>(secondGuid, "Scene", "com.arisen.test");

            Database = new TestAssetDatabase(
                AssetSourceAccessMode.EditorAuthoring,
                Path.Combine(m_Root, "Cooked"));
            Database.AddAsset(firstGuid, "Scene", FirstScenePath);
            Database.AddAsset(secondGuid, "Scene", secondScenePath);
            ActiveWorld = new EntityManager();
            RuntimeScenes = new RuntimeSceneService(Database, ActiveWorld);
            var initialLoad = RuntimeScenes.LoadScene(FirstScene);
            Assert.True(initialLoad.Success, initialLoad.Diagnostic);

            Commands = new CommandManager();
            Documents = new EditorSceneDocumentService(Database, RuntimeScenes, Commands);
            Assert.NotNull(Documents.Current);
            Assert.True(Documents.Current!.IsEditable);
        }

        public string AssetsRoot { get; }
        public string FirstScenePath { get; }
        public AssetRef<SceneSourceAsset> FirstScene { get; }
        public AssetRef<SceneSourceAsset> SecondScene { get; }
        public TestAssetDatabase Database { get; }
        public RuntimeSceneService RuntimeScenes { get; }
        public CommandManager Commands { get; }
        public EditorSceneDocumentService Documents { get; }
        public EntityManager ActiveWorld { get; }

        public Vector3 ActivePosition =>
            ActiveWorld.GetPool<TransformComponent>().GetRawComponentArray()[0].Position;

        public void Dispose()
        {
            Documents.Dispose();
            try
            {
                if (Directory.Exists(m_Root))
                {
                    Directory.Delete(m_Root, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
