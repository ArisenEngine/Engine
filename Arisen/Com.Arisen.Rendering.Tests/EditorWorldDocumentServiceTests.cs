using ArisenEditor.Core.Services;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Automation;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorWorldDocumentServiceTests : IDisposable
{
    private static readonly Guid s_WorldGuid =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid s_PersistentSceneGuid =
        Guid.Parse("91000000-0000-0000-0000-000000000002");
    private static readonly Guid s_FirstSceneGuid =
        Guid.Parse("91000000-0000-0000-0000-000000000003");
    private static readonly Guid s_SecondSceneGuid =
        Guid.Parse("91000000-0000-0000-0000-000000000004");
    private static readonly Guid s_AnchorGuid =
        Guid.Parse("92000000-0000-0000-0000-000000000001");
    private static readonly Guid s_MoveRootGuid =
        Guid.Parse("92000000-0000-0000-0000-000000000002");
    private static readonly Guid s_MoveChildGuid =
        Guid.Parse("92000000-0000-0000-0000-000000000003");
    private static readonly Guid s_TargetGuid =
        Guid.Parse("92000000-0000-0000-0000-000000000004");
    private const string PackageId = "com.arisen.editor-world-test";

    private readonly string m_Root;

    public EditorWorldDocumentServiceTests()
    {
        m_Root = Path.Combine(
            Path.GetTempPath(),
            "ArisenEditorWorldDocumentTests",
            Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void FirstOpen_PreservesStableUiStateAndEditPinAcrossRuntimeRefresh()
    {
        using var context = new WorldContext(m_Root);
        EditorWorldDocumentState current = context.Documents.Current!;
        Assert.Equal(s_WorldGuid, current.World.Guid);
        Assert.Equal(2, current.Cells.Count);
        Assert.NotEmpty(current.PersistentScene.Inspection.Entities);

        WorldCellId firstCell = current.Cells[0].CellId;
        var selection = new EditorWorldSelectionId(
            current.Cells[0].SceneDocument.Scene.Guid,
            firstCell,
            s_AnchorGuid);
        context.Documents.SetExpanded($"cell:{firstCell}", true);
        context.Documents.SetStableSelection(selection);
        context.Documents.SelectCell(firstCell);
        Assert.True(context.Documents.LoadCellForEditing(firstCell));

        context.Streaming.SetStreamingSource(new WorldPosition(10_000, 0, 10_000));
        context.Streaming.PublishState(firstCell, WorldCellStreamingState.Active, runtimeDesired: false);

        current = context.Documents.Current!;
        EditorWorldCellDocumentState first = current.Cells.Single(cell => cell.CellId == firstCell);
        Assert.True(first.IsEditPinned);
        Assert.True(first.Streaming.Pinned);
        Assert.True(first.Streaming.Desired);
        Assert.False(first.IsRuntimeDesired);
        Assert.Equal(selection, current.Selection);
        Assert.Equal(firstCell, current.SelectedCellId);
        Assert.Contains($"cell:{firstCell}", current.ExpandedNodeIds);

        WorldPosition? focus = null;
        context.Documents.FocusRequested += (_, position) => focus = position;
        Assert.True(context.Documents.FocusCell(firstCell));
        Assert.NotNull(focus);
        Assert.Equal(-50, focus.Value.X);
        Assert.Equal(-50, focus.Value.Z);
    }

    [Fact]
    public void SelectCell_PublishesOnlyWhenSelectionChanges()
    {
        using var context = new WorldContext(m_Root);
        WorldCellId firstCell = context.Documents.Current!.Cells[0].CellId;
        var publicationCount = 0;
        context.Documents.StateChanged += _ => publicationCount++;

        context.Documents.SelectCell(firstCell);
        context.Documents.SelectCell(firstCell);

        Assert.Equal(1, publicationCount);
        Assert.Equal(firstCell, context.Documents.Current!.SelectedCellId);
    }

    [Fact]
    public void FocusCell_DoesNotChangeResidencyOrDirtyDocuments()
    {
        using var context = new WorldContext(m_Root);
        EditorWorldCellDocumentState cell = context.Documents.Current!.Cells[1];
        WorldCellStreamingSnapshot before = cell.Streaming;
        int reloadRequests = context.Streaming.ReloadRequests;

        Assert.True(context.Documents.FocusCell(cell.CellId));

        EditorWorldDocumentState current = context.Documents.Current!;
        EditorWorldCellDocumentState after = current.Cells.Single(item => item.CellId == cell.CellId);
        Assert.False(current.IsDirty);
        Assert.Equal(before.State, after.Streaming.State);
        Assert.Equal(before.Pinned, after.Streaming.Pinned);
        Assert.Equal(before.Desired, after.Streaming.Desired);
        Assert.Equal(reloadRequests, context.Streaming.ReloadRequests);
        Assert.Equal(cell.CellId, current.FocusedCellId);
    }

    [Fact]
    public void CellTransformEditing_RequiresActiveEditResidencyAndClearsSelectionOnUnload()
    {
        using var context = new WorldContext(m_Root);
        EditorWorldCellDocumentState cell = context.Documents.Current!.Cells[0];
        SceneEntityInspection entity = cell.SceneDocument.Inspection.Entities.Single(
            item => item.AuthoringGuid == s_AnchorGuid);
        var editedTransform = entity.Transform with
        {
            Position = entity.Transform.Position + new System.Numerics.Vector3(7.0f, 0.0f, 0.0f)
        };

        EditorWorldDocumentResult unloadedEdit = context.Documents.ApplyCellEntityTransform(
            cell.CellId,
            entity.AuthoringGuid,
            editedTransform);

        Assert.False(unloadedEdit.Success);
        Assert.Contains("active and pinned", unloadedEdit.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(context.Documents.Current!.Cells[0].IsEditResident);
        Assert.Null(context.Streaming.PreviewSources[cell.CellId]);

        Assert.True(context.Documents.LoadCellForEditing(cell.CellId));
        Assert.False(context.Documents.Current!.Cells[0].IsEditResident);
        context.Streaming.PublishState(cell.CellId, WorldCellStreamingState.Active, runtimeDesired: false);
        Assert.True(context.Documents.Current!.Cells[0].IsEditResident);

        EditorWorldDocumentResult activeEdit = context.Documents.ApplyCellEntityTransform(
            cell.CellId,
            entity.AuthoringGuid,
            editedTransform);

        Assert.True(activeEdit.Success, activeEdit.Diagnostic);
        Assert.True(context.Documents.Current!.Cells[0].IsDirty);
        Assert.NotNull(context.Streaming.PreviewSources[cell.CellId]);

        context.Documents.SetStableSelection(new EditorWorldSelectionId(
            cell.SceneDocument.Scene.Guid,
            cell.CellId,
            entity.AuthoringGuid));
        Assert.True(context.Documents.UnloadCellForEditing(cell.CellId));
        Assert.False(context.Documents.Current!.Cells[0].IsEditResident);
        Assert.Null(context.Documents.Current!.Selection);

        EditorWorldDocumentResult staleEdit = context.Documents.ApplyCellEntityTransform(
            cell.CellId,
            entity.AuthoringGuid,
            editedTransform);
        Assert.False(staleEdit.Success);
    }

    [Fact]
    public void CellWorkingSource_PreviewsWithoutWritingAndDetectsExternalConflict()
    {
        using var context = new WorldContext(m_Root);
        EditorWorldCellDocumentState cell = context.Documents.Current!.Cells[0];
        string saved = File.ReadAllText(cell.SceneDocument.SourcePath);
        string edited = saved.Replace("X: 0", "X: 12", StringComparison.Ordinal);

        EditorWorldDocumentResult applied = context.Documents.ApplyCellWorkingSource(
            cell.CellId,
            edited);

        Assert.True(applied.Success, applied.Diagnostic);
        Assert.Equal(saved, File.ReadAllText(cell.SceneDocument.SourcePath));
        Assert.True(context.Documents.Current!.Cells[0].IsDirty);
        Assert.Equal(edited, context.Streaming.PreviewSources[cell.CellId]!.SourceText);
        Assert.True(context.Streaming.ReloadRequests > 0);

        File.WriteAllText(cell.SceneDocument.SourcePath, saved.Replace("Anchor", "External Anchor"));
        EditorWorldDocumentResult blocked = context.Documents.SaveCell(cell.CellId);

        Assert.False(blocked.Success);
        Assert.True(context.Documents.Current!.Cells[0].HasExternalChanges);
        Assert.Contains("changed on disk", blocked.Diagnostic, StringComparison.OrdinalIgnoreCase);

        EditorWorldDocumentResult discarded = context.Documents.DiscardCellChanges(cell.CellId);
        Assert.True(discarded.Success, discarded.Diagnostic);
        Assert.False(context.Documents.Current!.Cells[0].IsDirty);
        Assert.Null(context.Streaming.PreviewSources[cell.CellId]);
    }

    [Fact]
    public void MoveEntitySubtree_IsUndoableAndRequiresTransactionalSaveAll()
    {
        using var context = new WorldContext(m_Root);
        EditorWorldDocumentState initial = context.Documents.Current!;
        EditorWorldCellDocumentState source = initial.Cells.Single(
            cell => cell.SceneDocument.Scene.Guid == s_FirstSceneGuid);
        EditorWorldCellDocumentState target = initial.Cells.Single(
            cell => cell.SceneDocument.Scene.Guid == s_SecondSceneGuid);
        string sourceDisk = File.ReadAllText(source.SceneDocument.SourcePath);
        string targetDisk = File.ReadAllText(target.SceneDocument.SourcePath);

        EditorWorldDocumentResult moved = context.Documents.MoveEntityToCell(
            source.CellId,
            target.CellId,
            s_MoveRootGuid);

        Assert.True(moved.Success, moved.Diagnostic);
        EditorWorldDocumentState staged = context.Documents.Current!;
        Assert.DoesNotContain(
            staged.Cells.Single(cell => cell.CellId == source.CellId).SceneDocument.Inspection.Entities,
            entity => entity.AuthoringGuid == s_MoveRootGuid || entity.AuthoringGuid == s_MoveChildGuid);
        Assert.Contains(
            staged.Cells.Single(cell => cell.CellId == target.CellId).SceneDocument.Inspection.Entities,
            entity => entity.AuthoringGuid == s_MoveRootGuid);
        Assert.Contains(
            staged.Cells.Single(cell => cell.CellId == target.CellId).SceneDocument.Inspection.Entities,
            entity => entity.AuthoringGuid == s_MoveChildGuid);
        Assert.Equal(sourceDisk, File.ReadAllText(source.SceneDocument.SourcePath));
        Assert.Equal(targetDisk, File.ReadAllText(target.SceneDocument.SourcePath));

        EditorWorldDocumentResult partialSave = context.Documents.SaveCell(source.CellId);
        Assert.False(partialSave.Success);
        Assert.True(partialSave.RequiresUserResolution);

        context.Commands.Undo();
        Assert.Contains(
            context.Documents.Current!.Cells.Single(cell => cell.CellId == source.CellId)
                .SceneDocument.Inspection.Entities,
            entity => entity.AuthoringGuid == s_MoveRootGuid);
        context.Commands.Redo();

        EditorWorldDocumentResult saveAll = context.Documents.SaveAll();
        Assert.True(saveAll.Success, saveAll.Diagnostic);
        Assert.False(context.Documents.Current!.IsDirty);
        Assert.DoesNotContain(s_MoveRootGuid.ToString("D"), File.ReadAllText(source.SceneDocument.SourcePath));
        Assert.Contains(s_MoveRootGuid.ToString("D"), File.ReadAllText(target.SceneDocument.SourcePath));
        Assert.Empty(Directory.GetFiles(context.AssetsRoot, "*.tmp", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(context.AssetsRoot, "*.bak", SearchOption.AllDirectories));

        WorldDescriptorLoadResult validation = WorldDescriptorLoader.LoadSource(
            context.Database,
            context.WorldRef);
        Assert.True(validation.Success, validation.Diagnostic);
    }

    [Fact]
    public void MoveEntity_RejectsCrossCellHierarchyBeforeChangingEitherDocument()
    {
        using var context = new WorldContext(m_Root);
        EditorWorldDocumentState current = context.Documents.Current!;
        EditorWorldCellDocumentState source = current.Cells.Single(
            cell => cell.SceneDocument.Scene.Guid == s_FirstSceneGuid);
        EditorWorldCellDocumentState target = current.Cells.Single(
            cell => cell.SceneDocument.Scene.Guid == s_SecondSceneGuid);
        string oldSource = source.SceneDocument.WorkingSource;
        string oldTarget = target.SceneDocument.WorkingSource;

        EditorWorldDocumentResult result = context.Documents.MoveEntityToCell(
            source.CellId,
            target.CellId,
            s_MoveChildGuid);

        Assert.False(result.Success);
        Assert.Contains("Detach", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(oldSource, context.Documents.Current!.Cells.Single(
            cell => cell.CellId == source.CellId).SceneDocument.WorkingSource);
        Assert.Equal(oldTarget, context.Documents.Current!.Cells.Single(
            cell => cell.CellId == target.CellId).SceneDocument.WorkingSource);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_Root)) Directory.Delete(m_Root, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private sealed class WorldContext : IDisposable
    {
        public WorldContext(string root)
        {
            AssetsRoot = Path.Combine(root, "Assets");
            Directory.CreateDirectory(AssetsRoot);
            string worldPath = Path.Combine(AssetsRoot, "Test.arisenworld");
            string persistentPath = Path.Combine(AssetsRoot, "Persistent.arisenscene");
            string firstPath = Path.Combine(AssetsRoot, "CellA.arisenscene");
            string secondPath = Path.Combine(AssetsRoot, "CellB.arisenscene");
            File.WriteAllText(persistentPath, SceneSource(
                "Persistent",
                [(Guid.Parse("92000000-0000-0000-0000-000000000010"), "Camera", Guid.Empty, 0)]));
            File.WriteAllText(firstPath, SceneSource(
                "Cell A",
                [
                    (s_AnchorGuid, "Anchor", Guid.Empty, 0),
                    (s_MoveRootGuid, "Move Root", Guid.Empty, 5),
                    (s_MoveChildGuid, "Move Child", s_MoveRootGuid, 6)
                ]));
            File.WriteAllText(secondPath, SceneSource(
                "Cell B",
                [(s_TargetGuid, "Target", Guid.Empty, 100)]));
            File.WriteAllText(worldPath, WorldSource());

            Database = new TestAssetDatabase(
                AssetSourceAccessMode.EditorAuthoring,
                Path.Combine(root, "Cooked"));
            Database.AddAsset(s_WorldGuid, "World", worldPath, PackageId);
            Database.AddAsset(s_PersistentSceneGuid, "Scene", persistentPath, PackageId);
            Database.AddAsset(s_FirstSceneGuid, "Scene", firstPath, PackageId);
            Database.AddAsset(s_SecondSceneGuid, "Scene", secondPath, PackageId);
            WorldRef = new AssetRef<WorldSourceAsset>(s_WorldGuid, "World", PackageId);
            Streaming = new FakeWorldStreamingService(Database);
            RuntimeWorldLoadResult loaded = Streaming.LoadWorld(WorldRef);
            Assert.True(loaded.Success, loaded.Diagnostic);
            Commands = new CommandManager();
            Documents = new EditorWorldDocumentService(Database, Streaming, Commands);
            Assert.NotNull(Documents.Current);
        }

        public string AssetsRoot { get; }
        public TestAssetDatabase Database { get; }
        public AssetRef<WorldSourceAsset> WorldRef { get; }
        public FakeWorldStreamingService Streaming { get; }
        public CommandManager Commands { get; }
        public EditorWorldDocumentService Documents { get; }

        public void Dispose()
        {
            Documents.Dispose();
        }
    }

    private sealed class FakeWorldStreamingService : IRuntimeWorldStreamingService
    {
        private readonly IAssetDatabase m_Database;
        private readonly Dictionary<WorldCellId, WorldCellStreamingSnapshot> m_Cells = new();

        public FakeWorldStreamingService(IAssetDatabase database)
        {
            m_Database = database;
        }

        public WorldDescriptor? ActiveWorld { get; private set; }
        public AssetRef<WorldSourceAsset>? ActiveWorldAsset { get; private set; }
        public WorldStreamingBudgets Budgets { get; private set; } = WorldStreamingBudgets.Default;
        public Dictionary<WorldCellId, SceneSourceSnapshot?> PreviewSources { get; } = new();
        public int ReloadRequests { get; private set; }
        public WorldPosition? StreamingSource { get; private set; }

        public event Action<WorldCellStreamingSnapshot>? CellStateChanged;
        public event Action<AssetRef<WorldSourceAsset>?>? ActiveWorldChanged;

        public bool TryConfigureBudgets(WorldStreamingBudgets budgets, out string diagnostic)
        {
            Budgets = budgets;
            diagnostic = string.Empty;
            return true;
        }

        public RuntimeWorldLoadResult LoadWorld(AssetRef<WorldSourceAsset> world)
        {
            WorldDescriptorLoadResult loaded = WorldDescriptorLoader.LoadSource(m_Database, world);
            if (!loaded.Success || loaded.Descriptor == null)
            {
                return new RuntimeWorldLoadResult(false, world.Guid, 0, loaded.Diagnostic);
            }
            ActiveWorld = loaded.Descriptor;
            ActiveWorldAsset = world;
            m_Cells.Clear();
            foreach (WorldCellDescriptor cell in loaded.Descriptor.Cells)
            {
                m_Cells[cell.Id] = Snapshot(cell.Id);
                PreviewSources[cell.Id] = null;
            }
            ActiveWorldChanged?.Invoke(world);
            return new RuntimeWorldLoadResult(true, world.Guid, m_Cells.Count, string.Empty);
        }

        public void SetStreamingSource(WorldPosition position) => StreamingSource = position;
        public void ClearStreamingSource() => StreamingSource = null;

        public bool PinCell(WorldCellId cellId)
        {
            if (!m_Cells.TryGetValue(cellId, out WorldCellStreamingSnapshot? snapshot)) return false;
            WorldCellDesiredSource sources = snapshot.DesiredSources | WorldCellDesiredSource.EditPin;
            m_Cells[cellId] = snapshot with
            {
                Desired = true,
                DesiredSources = sources,
                Pinned = true
            };
            CellStateChanged?.Invoke(m_Cells[cellId]);
            return true;
        }

        public bool UnpinCell(WorldCellId cellId)
        {
            if (!m_Cells.TryGetValue(cellId, out WorldCellStreamingSnapshot? snapshot)) return false;
            WorldCellDesiredSource sources =
                snapshot.DesiredSources & ~WorldCellDesiredSource.EditPin;
            m_Cells[cellId] = snapshot with
            {
                Desired = sources != WorldCellDesiredSource.None,
                DesiredSources = sources,
                Pinned = false
            };
            CellStateChanged?.Invoke(m_Cells[cellId]);
            return true;
        }

        public bool SetCellPreviewSource(WorldCellId cellId, SceneSourceSnapshot? snapshot)
        {
            if (!m_Cells.ContainsKey(cellId)) return false;
            PreviewSources[cellId] = snapshot;
            return RequestCellReload(cellId);
        }

        public bool RequestCellReload(WorldCellId cellId)
        {
            if (!m_Cells.TryGetValue(cellId, out WorldCellStreamingSnapshot? snapshot)) return false;
            ReloadRequests++;
            m_Cells[cellId] = snapshot with { ReloadRequested = true };
            CellStateChanged?.Invoke(m_Cells[cellId]);
            return true;
        }

        public bool RetryCell(WorldCellId cellId) => m_Cells.ContainsKey(cellId);
        public IReadOnlyList<WorldCellStreamingSnapshot> GetCells() => m_Cells.Values.OrderBy(cell => cell.CellId).ToArray();
        public IReadOnlyList<WorldStreamingDiagnostic> GetDiagnostics() => Array.Empty<WorldStreamingDiagnostic>();
        public WorldStreamingMetrics GetMetrics() => new(
            0,
            m_Cells.Values.Count(cell => cell.State == WorldCellStreamingState.Active),
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        public void PublishState(
            WorldCellId cellId,
            WorldCellStreamingState state,
            bool runtimeDesired)
        {
            WorldCellDesiredSource sources = runtimeDesired
                ? m_Cells[cellId].DesiredSources | WorldCellDesiredSource.Runtime
                : m_Cells[cellId].DesiredSources & ~WorldCellDesiredSource.Runtime;
            WorldCellStreamingSnapshot snapshot = m_Cells[cellId] with
            {
                State = state,
                Desired = sources != WorldCellDesiredSource.None,
                DesiredSources = sources,
                TransitionSequence = m_Cells[cellId].TransitionSequence + 1
            };
            m_Cells[cellId] = snapshot;
            CellStateChanged?.Invoke(snapshot);
        }

        private static WorldCellStreamingSnapshot Snapshot(WorldCellId id) =>
            new(
                id,
                WorldCellStreamingState.Unloaded,
                0,
                0,
                false,
                WorldCellDesiredSource.None,
                false,
                false,
                RuntimeSceneInstanceId.Invalid,
                0,
                0,
                0,
                string.Empty);
    }

    private static string SceneSource(
        string name,
        IReadOnlyList<(Guid Guid, string Name, Guid Parent, int X)> entities)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Version: 2");
        builder.AppendLine($"Name: {name}");
        builder.AppendLine("ComponentSchemas:");
        builder.AppendLine("- TypeId: 1");
        builder.AppendLine("  Name: Transform");
        builder.AppendLine("  Version: 1");
        builder.AppendLine("  Required: true");
        builder.AppendLine("Entities:");
        foreach ((Guid guid, string entityName, Guid parent, int x) in entities)
        {
            builder.AppendLine($"- Guid: {guid:D}");
            builder.AppendLine($"  Name: {entityName}");
            if (parent != Guid.Empty)
            {
                builder.AppendLine("  Parent:");
                builder.AppendLine($"    EntityGuid: {parent:D}");
            }
            builder.AppendLine("  Transform:");
            builder.AppendLine($"    Position: {{ X: {x}, Y: 0, Z: 0 }}");
        }
        return builder.ToString();
    }

    private static string WorldSource() => $$"""
        Version: 1
        WorldGuid: {{s_WorldGuid:D}}
        Name: Editor World
        PersistentScene:
          Guid: {{s_PersistentSceneGuid:D}}
          PackageId: {{PackageId}}
        Partition:
          Origin: { X: -100, Y: -10, Z: -100 }
          CellSize: { X: 100, Y: 20, Z: 100 }
          LoadRadius: 0
          UnloadHysteresis: 0
          MaxActiveCells: 4
        Policy:
          UnresolvedReferences: KeepUnresolved
          UnloadedTargets: ClearAndLateResolve
          DependencyCycles: Reject
        Layers:
        - Id: surface
          Priority: 0
        Cells:
        - Coordinate: { X: 0, Y: 0, Z: 0 }
          Layer: surface
          Scene:
            Guid: {{s_FirstSceneGuid:D}}
            PackageId: {{PackageId}}
          Bounds:
            Min: { X: -100, Y: -10, Z: -100 }
            Max: { X: 0, Y: 10, Z: 0 }
          EstimatedCpuBytes: 1024
          EstimatedGpuBytes: 2048
        - Coordinate: { X: 1, Y: 0, Z: 0 }
          Layer: surface
          Scene:
            Guid: {{s_SecondSceneGuid:D}}
            PackageId: {{PackageId}}
          Bounds:
            Min: { X: 0, Y: -10, Z: -100 }
            Max: { X: 100, Y: 10, Z: 0 }
          EstimatedCpuBytes: 1024
          EstimatedGpuBytes: 2048
        """;
}
