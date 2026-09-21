using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using System.Numerics;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationWindTests
{
    [Fact]
    public void DefaultWindSitsInsideTheBoundedDomain()
    {
        VegetationWindSettings wind = VegetationWindSettings.Default;

        Assert.True(wind.IsValid);
        Assert.InRange(wind.Strength, 0.0f, VegetationWindSettings.MaximumStrength);
        Assert.InRange(
            wind.GustAmplitude,
            VegetationWindSettings.MinimumGustAmplitude,
            VegetationWindSettings.MaximumGustAmplitude);
        Assert.InRange(
            wind.GustFrequencyHz,
            VegetationWindSettings.MinimumGustFrequencyHz,
            VegetationWindSettings.MaximumGustFrequencyHz);
        Assert.Equal(0.0f, wind.Direction.Y, 6);
        Assert.Equal(1.0f, wind.Direction.Length(), 5);
    }

    [Theory]
    [InlineData(-4.0f, VegetationWindSettings.MinimumStrength)]
    [InlineData(1.0e9f, VegetationWindSettings.MaximumStrength)]
    [InlineData(float.NaN, VegetationWindSettings.MinimumStrength)]
    public void StrengthIsClampedIntoTheBoundedDomain(float requested, float expected)
    {
        VegetationWindSettings wind = VegetationWindSettings.Create(
            new Vector3(1.0f, 0.0f, 0.0f),
            requested,
            gustAmplitude: 0.2f,
            gustFrequencyHz: 0.5f,
            timeSeconds: 0.0f);

        Assert.True(wind.IsValid);
        Assert.Equal(expected, wind.Strength, 5);
    }

    [Fact]
    public void NonPlanarAndNonFiniteDirectionsFallBackToTheDefaultFlow()
    {
        VegetationWindSettings vertical = VegetationWindSettings.Create(
            new Vector3(0.0f, 1.0f, 0.0f),
            strength: 1.0f,
            gustAmplitude: 0.0f,
            gustFrequencyHz: 0.5f,
            timeSeconds: 0.0f);
        VegetationWindSettings nonFinite = VegetationWindSettings.Create(
            new Vector3(float.NaN, 0.0f, float.PositiveInfinity),
            strength: 1.0f,
            gustAmplitude: 0.0f,
            gustFrequencyHz: 0.5f,
            timeSeconds: 0.0f);

        Assert.True(vertical.IsValid);
        Assert.Equal(0.0f, vertical.Direction.Y, 6);
        Assert.Equal(1.0f, vertical.Direction.Length(), 5);
        Assert.True(nonFinite.IsValid);
        Assert.Equal(
            VegetationWindSettings.Default.Direction,
            nonFinite.Direction);
    }

    [Fact]
    public void GustPhasesWrapOnTheCpuForLongSessions()
    {
        VegetationWindSettings wind = VegetationWindSettings.Default.WithTime(86_400.0f);

        (float primary, float secondary) = wind.ResolveGustPhases();

        Assert.InRange(primary, 0.0f, (float)Math.Tau);
        Assert.InRange(secondary, 0.0f, (float)Math.Tau);
        Assert.Equal(primary, wind.ResolveGustPhases().Primary, 6);
    }

    /// <summary>
    /// The gust is a wave over the world, but the vegetation shaders only receive origin-relative
    /// vertex positions, so the CPU folds <c>dot(direction, renderOrigin) * spatialFrequency</c>
    /// into the wrapped angles it uploads. A rebase therefore cannot move or re-phase the wave:
    /// the angle a vertex at a fixed world position feeds into <c>sin</c> is the same before and
    /// after the origin shifts by a whole terrain page. Without that anchor the gust pattern would
    /// be pinned to the streaming origin and an origin rebase would visibly reshuffle the valley
    /// even though nothing in the world moved.
    /// </summary>
    [Fact]
    public void GustPhasesAnchorTheWaveToTheWorldAcrossAnOriginRebase()
    {
        VegetationWindSettings wind = VegetationWindSettings.Default.WithTime(6.0f);
        var world = new WorldPosition(1_538.5, 3.25, -1_024.75);
        var beforeRebase = new WorldPosition(0.0, 0.0, 0.0);
        var afterRebase = new WorldPosition(1_536.0, 0.0, 0.0);

        double beforePrimary = ShaderPhaseArgument(wind, beforeRebase, world, secondary: false);
        double afterPrimary = ShaderPhaseArgument(wind, afterRebase, world, secondary: false);
        double beforeSecondary = ShaderPhaseArgument(wind, beforeRebase, world, secondary: true);
        double afterSecondary = ShaderPhaseArgument(wind, afterRebase, world, secondary: true);

        Assert.InRange(CircularDifference(beforePrimary, afterPrimary), 0.0, 1.0e-4);
        Assert.InRange(CircularDifference(beforeSecondary, afterSecondary), 0.0, 1.0e-4);
    }

    [Fact]
    public void GustPhasesStayBoundedForALargeRenderOrigin()
    {
        (float primary, float secondary) = VegetationWindSettings.Default
            .WithTime(86_400.0f)
            .ResolveGustPhases(new WorldPosition(1_200_000.0, -32.0, 950_000.0));

        Assert.InRange(primary, 0.0f, (float)Math.Tau);
        Assert.InRange(secondary, 0.0f, (float)Math.Tau);
    }

    [Fact]
    public void GustPhasesRequireAFiniteRenderOrigin()
    {
        Assert.Throws<ArgumentException>(
            () => VegetationWindSettings.Default.ResolveGustPhases(
                new WorldPosition(double.NaN, 0.0, 0.0)));
        Assert.Throws<ArgumentException>(
            () => VegetationWindSettings.Default.ResolveGustPhases(
                new WorldPosition(0.0, 0.0, double.PositiveInfinity)));
    }

    [Fact]
    public void BendFractionSaturatesAtOneForEveryValidStiffness()
    {
        Assert.Equal(0.0f, VegetationWindSettings.MaximumBendFraction(0.0f), 6);
        Assert.Equal(0.0f, VegetationWindSettings.MaximumBendFraction(float.NaN), 6);
        Assert.True(VegetationWindSettings.MaximumBendFraction(1.0f) <= 1.0f);
        Assert.True(VegetationWindSettings.MaximumBendFraction(4.0f) <= 1.0f);
        Assert.True(
            VegetationWindSettings.MaximumBendFraction(0.05f) <
            VegetationWindSettings.MaximumBendFraction(0.85f));
    }

    [Fact]
    public void HorizontalDisplacementNeverExceedsTheInstanceHeight()
    {
        const float height = 0.6f;

        float grass = VegetationWindSettings.MaximumHorizontalDisplacement(0.85f, height);

        Assert.InRange(grass, 0.0f, height);
        Assert.Equal(0.0f, VegetationWindSettings.MaximumHorizontalDisplacement(1.0f, -3.0f), 6);
        Assert.Equal(
            height,
            VegetationWindSettings.MaximumHorizontalDisplacement(1.0f, height),
            6);
    }

    [Fact]
    public void ServiceHonoursDeterministicTimeOverrides()
    {
        var service = new VegetationWindService();

        Assert.False(service.HasTimeOverride);
        service.SetTimeSeconds(12.5f);
        Assert.True(service.HasTimeOverride);
        Assert.Equal(12.5f, service.Current.TimeSeconds, 5);

        VegetationWindSettings first = service.Current;
        Assert.Equal(first, service.Current);

        service.ClearTimeOverride();
        Assert.False(service.HasTimeOverride);

        service.Configure(
            new Vector3(1.0f, 0.0f, 0.0f),
            strength: 9.0f,
            gustAmplitude: 0.5f,
            gustFrequencyHz: 0.25f);
        service.SetTimeSeconds(3.0f);
        Assert.Equal(9.0f, service.Current.Strength, 5);

        service.Clear();
        Assert.False(service.HasTimeOverride);
        Assert.Equal(VegetationWindSettings.Default.Strength, service.Current.Strength, 5);
    }

    [Theory]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ServiceRejectsInvalidDeterministicTimes(float seconds)
    {
        var service = new VegetationWindService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetTimeSeconds(seconds));
    }

    [Fact]
    public void WindClockControlPinsThePoseIndependentlyOfTheWallClock()
    {
        var service = new VegetationWindService();
        IVegetationWindClockControl clock = service;

        Assert.False(clock.HasTimeOverride);
        clock.SetTimeSeconds(6.0f);
        VegetationWindSettings pinned = service.Current;
        Assert.Equal(6.0f, pinned.TimeSeconds, 5);

        double wallStart = ArisenKernel.Lifecycle.Time.totalTime;
        Assert.True(
            SpinWait.SpinUntil(
                () => ArisenKernel.Lifecycle.Time.totalTime - wallStart >= 0.002,
                TimeSpan.FromSeconds(5)),
            "The wall clock did not advance while preparing the wind-clock test.");

        Assert.True(clock.HasTimeOverride);
        Assert.Equal(pinned, service.Current);

        clock.ClearTimeOverride();
        Assert.False(clock.HasTimeOverride);
    }

    /// <summary>
    /// Rebuilds the angle the vegetation shaders feed into <c>sin</c> for one vertex: the wrapped
    /// frame phase plus the shader's own origin-relative spatial term. Only the sum is
    /// observable, because the shader wraps nothing itself.
    /// </summary>
    private static double ShaderPhaseArgument(
        in VegetationWindSettings wind,
        WorldPosition renderOrigin,
        WorldPosition world,
        bool secondary)
    {
        (float primary, float secondaryPhase) = wind.ResolveGustPhases(renderOrigin);
        double localSpatial =
            ((world.X - renderOrigin.X) * wind.Direction.X +
                (world.Z - renderOrigin.Z) * wind.Direction.Z) *
            VegetationWindSettings.GustSpatialFrequency;
        return secondary
            ? secondaryPhase +
                (localSpatial * VegetationWindSettings.SecondaryGustSpatialScale)
            : primary + localSpatial;
    }

    private static double CircularDifference(double left, double right)
    {
        double difference = (left - right) % Math.Tau;
        if (difference < 0.0) difference += Math.Tau;
        return Math.Min(difference, Math.Tau - difference);
    }
}
