namespace Com.Arisen.Rendering.Tests;

// Pins the authored MistfallValley region at the scale the runtime gates assert: one 1 km terrain
// root whose eight-by-eight 128 m tiles are split across sixteen cells, and the cell-scoped
// vegetation bakes that read from it. RegionalWorldAssetTests pins the same content from the
// assets themselves; the script contract tests read the numbers from here so a gate, a script, and
// the authored world cannot drift apart silently.
internal static class MistfallValleyRegionFixture
{
    public const int CellsPerAxis = 4;
    public const int TilesPerCellAxis = 2;
    public const int TilesPerAxis = CellsPerAxis * TilesPerCellAxis;
    public const int CellCount = CellsPerAxis * CellsPerAxis;
    public const int TileCount = TilesPerAxis * TilesPerAxis;

    // Every cell bakes a grass, a shrub, and a tree cluster; the rock recipe admits no candidate
    // on the shipped raster, so it publishes none.
    public const int BakedSpeciesPerCell = 3;
    public const int ClusterCount = CellCount * BakedSpeciesPerCell;

    // Page counts follow each cell's own terrain weights: fourteen grass pages, five shrub pages,
    // and two tree pages per cell.
    public const int GrassPagesPerCell = 14;
    public const int ShrubPagesPerCell = 5;
    public const int TreePagesPerCell = 2;
    public const int InstancePagesPerCell =
        GrassPagesPerCell + ShrubPagesPerCell + TreePagesPerCell;
    public const int InstancePageCount = CellCount * InstancePagesPerCell;

    public const int ShippedSpeciesCount = 4;
    public const int SharedBiomeCount = 1;
    public const int VegetationRuntimeArtifactCount =
        SharedBiomeCount + ShippedSpeciesCount + ClusterCount + InstancePageCount;

    // The opaque pass and the shadow pass each publish a vertex and a fragment stage.
    public const int VegetationShaderStageCount = 4;
    public const int VegetationDeploymentFileCount =
        VegetationRuntimeArtifactCount + VegetationShaderStageCount;
}
