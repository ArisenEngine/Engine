using System.Collections.Generic;
using ArisenKernel.Packages;

namespace ArisenKernel.Lifecycle;

/// <summary>
/// Command-line startup target selection. One workspace hosts several products, so the content a
/// process boots with is chosen by argument rather than by editing the manifest:
/// <c>--startup-scene &lt;packageId&gt;:&lt;guid&gt;</c> and
/// <c>--startup-world &lt;packageId&gt;:&lt;guid&gt;</c>. The arguments name at most one target;
/// without them the manifest decides.
/// </summary>
public readonly record struct StartupTargetOptions(
    ProjectAssetReference? Scene,
    ProjectAssetReference? World)
{
    public const string SceneArgument = "--startup-scene";
    public const string WorldArgument = "--startup-world";

    public static StartupTargetOptions None { get; } = new(null, null);

    public bool Specified => Scene != null || World != null;

    public static StartupTargetOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ProjectAssetReference? scene = null;
        ProjectAssetReference? world = null;
        for (int index = 0; index < args.Count; index++)
        {
            bool isScene = string.Equals(args[index], SceneArgument, StringComparison.OrdinalIgnoreCase);
            bool isWorld = string.Equals(args[index], WorldArgument, StringComparison.OrdinalIgnoreCase);
            if (!isScene && !isWorld)
            {
                continue;
            }

            string argument = isScene ? SceneArgument : WorldArgument;
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"{argument} requires a '<packageId>:<guid>' value.");
            }

            if ((isScene && scene != null) || (isWorld && world != null))
            {
                throw new ArgumentException($"{argument} was specified more than once.");
            }

            ProjectAssetReference reference = ParseReference(argument, args[++index]);
            if (isScene)
            {
                scene = reference;
            }
            else
            {
                world = reference;
            }
        }

        if (scene != null && world != null)
        {
            throw new ArgumentException(
                $"{SceneArgument} and {WorldArgument} name two different startup targets; specify one.");
        }

        return new StartupTargetOptions(scene, world);
    }

    /// <summary>
    /// The target a process actually boots with. An explicit argument wins; when a scene was named,
    /// the manifest world is suppressed, because a caller that named a scene asked for that scene
    /// rather than for the workspace's default world. Without arguments the manifest decides, and a
    /// world keeps winning over a scene in the bootstrap itself.
    /// </summary>
    public ResolvedStartupTarget Resolve(ProjectManifest? manifest)
    {
        ProjectAssetReference? resolvedScene = Valid(Scene ?? manifest?.StartupScene);
        ProjectAssetReference? resolvedWorld =
            Valid(World ?? (Scene != null ? null : manifest?.StartupWorld));
        return new ResolvedStartupTarget(resolvedScene, resolvedWorld);
    }

    /// <summary>
    /// Names the raw selection for host diagnostics. The value is what the caller asked for, not
    /// what the manifest resolved to, so a log always shows whether an override was in play.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>(2);
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
/// The startup target after arguments and manifest are combined. A world still wins over a scene
/// when the bootstrap activates the target, matching the manifest's world-first bootstrap.
/// </summary>
public readonly record struct ResolvedStartupTarget(
    ProjectAssetReference? Scene,
    ProjectAssetReference? World)
{
    public bool HasScene => Scene != null;
    public bool HasWorld => World != null;
    public bool HasTarget => HasScene || HasWorld;

    public string Describe()
    {
        var parts = new List<string>(2);
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
