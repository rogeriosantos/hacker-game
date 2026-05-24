using Godot;
using Godot.Collections;

namespace HackerGame.Resources;

/// <summary>
/// One quest. Drives a single scripted scenario: a sandbox is seeded with
/// <see cref="VfsFixtures"/>, <see cref="MockModules"/> are imported, the
/// player types real PowerShell, and <see cref="Objective"/> verifies after
/// every command. On completion: award <see cref="Xp"/> (+
/// <see cref="BonusXpHintFree"/> if no hints were taken) and unlock
/// <see cref="CmdletsUnlockedOnCompletion"/>.
/// </summary>
[GlobalClass]
public partial class QuestResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string Title { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Narrative { get; set; } = "";

    /// <summary>
    /// Files to drop into the sandbox dir before the quest begins.
    /// Keys are relative paths (e.g. "home/bob/.env"), values are file contents.
    /// </summary>
    [Export] public Godot.Collections.Dictionary<string, string> VfsFixtures { get; set; } = new();

    /// <summary>
    /// Cmdlets the quest "officially" teaches. Surfaced to the player in the HUD
    /// and used by the quest validator. Not currently enforced by the runner —
    /// the player can call anything pwsh can resolve, and the objective gates
    /// progression.
    /// </summary>
    [Export] public string[] AllowedCmdlets { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Absolute or sandbox-relative paths to directories containing PowerShell
    /// modules to import for the quest. These get prepended to PSModulePath so
    /// they win over real OS cmdlets (e.g., a mock Get-Process for Level 3).
    /// </summary>
    [Export] public string[] MockModulePaths { get; set; } = System.Array.Empty<string>();

    [Export] public ObjectiveResource? Objective { get; set; }

    [Export] public Array<HintTier> Hints { get; set; } = new();

    [Export] public string[] CmdletsUnlockedOnCompletion { get; set; } = System.Array.Empty<string>();

    [Export] public int Xp { get; set; } = 50;
    [Export] public int BonusXpHintFree { get; set; } = 25;
}
