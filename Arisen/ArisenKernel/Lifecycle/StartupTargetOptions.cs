using System.Collections.Generic;
using ArisenKernel.Packages;

namespace ArisenKernel.Lifecycle;

/// <summary>
/// Command-line startup target selection. One workspace hosts several products, so the content a
/// process boots with is chosen by argument rather than by editing the manifest:
/// <c>--startup-scene &lt;packageId&gt;:&lt;guid&gt;</c> and
/// <c>--startup-world &lt;packageId&gt;:&lt;guid&gt;</c> name an asset, and
/// <c>--startup-sample &lt;id&gt;</c> names a catalogue entry. The arguments select at most one
/// target; without them the profile, then the manifest, decides.
/// </summary>
public readonly record struct StartupTargetOptions(
    ProjectAssetReference? Scene,
    ProjectAssetReference? World,
    string? Sample)
{
    public const string SceneArgument = "--startup-scene";
    public const string WorldArgument = "--startup-world";
    public const string SampleArgument = "--startup-sample";

    public static StartupTargetOptions None { get; } = new(null, null, null);

    public bool Specified => Scene != null || World != null || !string.IsNullOrWhiteSpace(Sample);

    public static StartupTargetOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProjectAssetReference? scene = null;
        ProjectAssetReference? world = null;
        string? sample = null;
        for (int index = 0; index < args.Count; index++)
        {
            bool isScene = string.Equals(args[index], SceneArgument, StringComparison.OrdinalIgnoreCase);
            bool isWorld = string.Equals(args[index], WorldArgument, StringComparison.OrdinalIgnoreCase);
            bool isSample = string.Equals(args[index], SampleArgument, StringComparison.OrdinalIgnoreCase);
            if (!isScene && !isWorld && !isSample)
            {
                continue;
            }

            string argument = isScene ? SceneArgument : isWorld ? WorldArgument : SampleArgument;
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException(argument == SampleArgument
                    ? $"{argument} requires a sample id."
                    : $"{argument} requires a '<packageId>:<guid>' value.");
            }

            if ((isScene && scene != null) || (isWorld && world != null) || (isSample && sample != null))
            {
                throw new ArgumentException($"{argument} was specified more than once.");
            }

            if (isScene)
            {
                scene = ParseReference(argument, args[++index]);
            }
            else if (isWorld)
            {
                world = ParseReference(argument, args[++index]);
            }
            else
            {
                sample = ParseSample(argument, args[++index]);
            }
        }

        int selected = (scene != null ? 1 : 0) + (world != null ? 1 : 0) + (sample != null ? 1 : 0);
        if (selected > 1)
        {
            throw new ArgumentException(
                $"{SceneArgument}, {WorldArgument}, and {SampleArgument} name different startup " +
                "targets; specify one.");
        }

        return new StartupTargetOptions(scene, world, sample);
    }

    /// <summary>
    /// The target a process actually boots with: an explicit argument wins, then the profile's
    /// declared target, then the workspace manifest. A selection replaces the lower authority
    /// entirely - naming a scene does not merely re-point the manifest's world - because a caller
    /// (or a profile) that names one target asked for exactly that target.
    /// </summary>
    public ResolvedStartupTarget Resolve(ProjectManifest? manifest, ProfileDefinition? profile = null)
    {
        if (Specified)
        {
            return new ResolvedStartupTarget(Valid(Scene), Valid(World), Trimmed(Sample));
        }

        if (profile != null)
        {
            bool sceneDeclared = profile.StartupScene != null;
            bool worldDeclared = profile.StartupWorld != null;
            bool sampleDeclared = !string.IsNullOrWhiteSpace(profile.StartupSample);
            int declared = (sceneDeclared ? 1 : 0) + (worldDeclared ? 1 : 0) + (sampleDeclared ? 1 : 0);
            if (declared > 1)
            {
                throw new InvalidOperationException(
                    "A profile declares at most one startup target; it declares a scene, a world, " +
                    "and/or a sample id.");
            }

            if (declared == 1)
            {
                if (sceneDeclared && !profile.StartupScene!.IsValid)
                {
                    throw new InvalidOperationException(
                        "A profile StartupScene requires both a valid Guid and PackageId.");
                }

                if (worldDeclared && !profile.StartupWorld!.IsValid)
                {
                    throw new InvalidOperationException(
                        "A profile StartupWorld requires both a valid Guid and PackageId.");
                }

                return new ResolvedStartupTarget(
                    Valid(profile.StartupScene),
                    Valid(profile.StartupWorld),
                    Trimmed(profile.StartupSample));
            }
        }

        return new ResolvedStartupTarget(
            Valid(manifest?.StartupScene),
            Valid(manifest?.StartupWorld),
            Sample: null);
    }

    /// <summary>
    /// Names the raw selection for host diagnostics. The value is what the caller asked for, not
    /// what the manifest resolved to, so a log always shows whether an override was in play.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(Sample))
        {
            parts.Add($"sample {Sample}");
        }

        if (Scene is { IsValid: true } scene)
        {
            parts.Add($"scene {scene.PackageId}:{scene.Guid}");
        }

        if (World is { IsValid: true } world)
        {
            parts.Add($"world {world.PackageId}:{world.Guid}");
        }

        return parts.Count == 0 ? "<manifest>" : string.Join(", ", parts);
    }

    private static ProjectAssetReference? Valid(ProjectAssetReference? reference) =>
        reference is { IsValid: true } ? reference : null;

    private static string? Trimmed(string? sample) =>
        string.IsNullOrWhiteSpace(sample) ? null : sample.Trim();

    private static string ParseSample(string argument, string value)
    {
        string sample = value.Trim();
        if (sample.Length == 0)
        {
            throw new ArgumentException($"{argument} expects a catalogue entry id, not whitespace.");
        }

        return sample;
    }

    private static ProjectAssetReference ParseReference(string argument, string value)
    {
        string[] parts = value.Split(':');
        if (parts.Length != 2 ||
            string.IsNullOrWhiteSpace(parts[0]) ||
            !Guid.TryParse(parts[1], out Guid guid) ||
            guid == Guid.Empty)
        {
            throw new ArgumentException($"{argument} expects '<packageId>:<guid>', not '{value}'.");
        }

        return new ProjectAssetReference
        {
            Guid = guid,
            PackageId = parts[0]
        };
    }
}

/// <summary>
/// The startup target after arguments, profile, and manifest are combined. A world still wins over
/// a scene when the bootstrap activates the target, matching the manifest's world-first bootstrap;
/// a sample id resolves through the workspace's sample catalogue into a scene and/or world.
/// </summary>
public readonly record struct ResolvedStartupTarget(
    ProjectAssetReference? Scene,
    ProjectAssetReference? World,
    string? Sample)
{
    public bool HasScene => Scene != null;
    public bool HasWorld => World != null;
    public bool HasSample => !string.IsNullOrWhiteSpace(Sample);
    public bool HasTarget => HasScene || HasWorld || HasSample;

    public string Describe()
    {
        var parts = new List<string>(3);
        if (HasSample)
        {
            parts.Add($"sample {Sample}");
        }

        if (World is { IsValid: true } world)
        {
            parts.Add($"world {world.PackageId}:{world.Guid}");
        }

        if (Scene is { IsValid: true } scene)
        {
            parts.Add($"scene {scene.PackageId}:{scene.Guid}");
        }

        return parts.Count == 0 ? "<none>" : string.Join(", ", parts);
    }
}
