using System.Numerics;

namespace ArisenKernel.Contracts;

/// <summary>
/// The world-space point an interactive runtime viewer should follow. A package that owns a
/// player-controlled entity publishes it - the physics package publishes its character, for
/// example - and the application shell's camera moves behind it instead of flying free. The
/// contract keeps the two sides decoupled: the shell never learns which package owns the player,
/// and no package depends on the shell.
/// </summary>
public interface IRuntimeViewTarget
{
    /// <summary>True while there is a player-controlled entity to follow.</summary>
    bool IsActive { get; }

    /// <summary>
    /// The world-space point to follow, usually the player's chest height. False when no target is
    /// available, in which case the viewer stays where it is.
    /// </summary>
    bool TryGetTarget(out Vector3 worldPosition);
}
