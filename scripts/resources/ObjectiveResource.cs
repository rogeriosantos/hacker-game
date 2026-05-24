using Godot;
using HackerGame.Autoload;

namespace HackerGame.Resources;

/// <summary>
/// Result of an objective check. <see cref="Satisfied"/> = quest passes.
/// <see cref="Detail"/> is shown to the player to teach (e.g., "your output
/// contains the path but the file is empty — check Get-Content").
/// </summary>
public readonly record struct ObjectiveVerifyResult(bool Satisfied, string Detail = "");

/// <summary>
/// Context passed to every objective check.
/// </summary>
public sealed record ObjectiveContext(
    string PlayerCommand,
    PowerShellRunner.PSResult? LastResult,
    string SandboxDir,
    PowerShellRunner Runner);

/// <summary>
/// Abstract base for quest objectives. Each concrete subclass verifies a
/// different kind of condition — output match, file side-effect, Pester pass.
/// All are <see cref="GlobalClassAttribute"/> so they appear in Godot's
/// "New Resource" menu in the inspector.
/// </summary>
[GlobalClass]
public partial class ObjectiveResource : Resource
{
    /// <summary>
    /// Override in subclasses. Default impl returns false so a misconfigured
    /// quest (objective field empty) can never auto-pass.
    /// </summary>
    public virtual Task<ObjectiveVerifyResult> VerifyAsync(ObjectiveContext ctx)
        => Task.FromResult(new ObjectiveVerifyResult(false, "no objective configured"));
}
