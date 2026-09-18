using ArisenEngine.Rendering;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationShadowDrawPartitionTests
{
    [Fact]
    public void EmptyRangesProduceNoWorkItems()
    {
        var ranges = new DirectionalShadowCascadeDrawRangeSet(
            4,
            totalDrawCount: 0,
            droppedDrawCount: 0,
            new DirectionalShadowCascadeDrawRange(0, 0),
            new DirectionalShadowCascadeDrawRange(0, 0),
            new DirectionalShadowCascadeDrawRange(0, 0),
            new DirectionalShadowCascadeDrawRange(0, 0));

        Assert.Equal(0, VegetationShadowDrawWorkPartition.GetWorkItemCount(ranges));
        Assert.False(
            VegetationShadowDrawWorkPartition.TryGetRange(
                ranges,
                0,
                out VegetationShadowDrawRange range));
        Assert.True(range.IsEmpty);
    }

    [Fact]
    public void SmallDrawCountsStayInsideOneBoundedWorkItem()
    {
        var ranges = new DirectionalShadowCascadeDrawRangeSet(
            4,
            totalDrawCount: 4,
            droppedDrawCount: 0,
            new DirectionalShadowCascadeDrawRange(0, 1),
            new DirectionalShadowCascadeDrawRange(1, 1),
            new DirectionalShadowCascadeDrawRange(2, 1),
            new DirectionalShadowCascadeDrawRange(3, 1));

        Assert.Equal(4, VegetationShadowDrawWorkPartition.GetWorkItemCount(ranges));
        for (int workItemIndex = 0; workItemIndex < 4; workItemIndex++)
        {
            Assert.True(
                VegetationShadowDrawWorkPartition.TryGetRange(
                    ranges,
                    workItemIndex,
                    out VegetationShadowDrawRange range));
            Assert.Equal(workItemIndex, range.CascadeIndex);
            Assert.Equal(workItemIndex, range.Start);
            Assert.Equal(1, range.Count);
        }

        Assert.False(
            VegetationShadowDrawWorkPartition.TryGetRange(
                ranges,
                4,
                out _));
    }

    [Fact]
    public void CascadesPartitionIndependentlyInAscendingSubmissionOrder()
    {
        var ranges = new DirectionalShadowCascadeDrawRangeSet(
            4,
            totalDrawCount: 1_030,
            droppedDrawCount: 0,
            new DirectionalShadowCascadeDrawRange(0, 300),
            new DirectionalShadowCascadeDrawRange(300, 512),
            new DirectionalShadowCascadeDrawRange(812, 0),
            new DirectionalShadowCascadeDrawRange(812, 218));

        int expectedWorkItemCount =
            Divide(300) + Divide(512) + Divide(0) + Divide(218);
        Assert.Equal(expectedWorkItemCount, VegetationShadowDrawWorkPartition.GetWorkItemCount(ranges));

        var recorded = new List<(int CascadeIndex, int DrawIndex)>();
        for (int workItemIndex = 0; workItemIndex < expectedWorkItemCount; workItemIndex++)
        {
            Assert.True(
                VegetationShadowDrawWorkPartition.TryGetRange(
                    ranges,
                    workItemIndex,
                    out VegetationShadowDrawRange range));
            Assert.InRange(
                range.Count,
                1,
                VegetationShadowDrawWorkPartition.MaximumDrawsPerWorkItem);

            DirectionalShadowCascadeDrawRange cascade = ranges.GetRange(range.CascadeIndex);
            Assert.InRange(range.Start, cascade.Start, cascade.End - 1);
            Assert.InRange(range.End, cascade.Start + 1, cascade.End);
            for (int drawIndex = range.Start; drawIndex < range.End; drawIndex++)
            {
                recorded.Add((range.CascadeIndex, drawIndex));
            }
        }

        Assert.Equal(1_030, recorded.Count);
        var expected = new List<(int CascadeIndex, int DrawIndex)>(1_030);
        for (int cascadeIndex = 0; cascadeIndex < ranges.Count; cascadeIndex++)
        {
            DirectionalShadowCascadeDrawRange cascade = ranges.GetRange(cascadeIndex);
            for (int drawIndex = cascade.Start; drawIndex < cascade.End; drawIndex++)
            {
                expected.Add((cascadeIndex, drawIndex));
            }
        }

        Assert.Equal(expected, recorded);
    }

    [Fact]
    public void WorkItemIndexesBeyondThePartitionAreRejected()
    {
        var ranges = new DirectionalShadowCascadeDrawRangeSet(
            2,
            totalDrawCount: 700,
            droppedDrawCount: 3,
            new DirectionalShadowCascadeDrawRange(0, 400),
            new DirectionalShadowCascadeDrawRange(400, 300),
            new DirectionalShadowCascadeDrawRange(700, 0),
            new DirectionalShadowCascadeDrawRange(700, 0));

        int workItemCount = VegetationShadowDrawWorkPartition.GetWorkItemCount(ranges);
        Assert.Equal(Divide(400) + Divide(300), workItemCount);
        Assert.False(
            VegetationShadowDrawWorkPartition.TryGetRange(
                ranges,
                workItemCount,
                out _));
        Assert.False(
            VegetationShadowDrawWorkPartition.TryGetRange(
                ranges,
                -1,
                out _));
    }

    private static int Divide(int drawCount) =>
        (drawCount + VegetationShadowDrawWorkPartition.MaximumDrawsPerWorkItem - 1) /
        VegetationShadowDrawWorkPartition.MaximumDrawsPerWorkItem;
}
