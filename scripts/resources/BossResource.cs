using Godot;
using Godot.Collections;

namespace HackerGame.Resources;

/// <summary>
/// A boss fight: a quest with scoring, no hints by default, and an optional
/// trace timer. Beating it unlocks the next LevelResource.
/// </summary>
[GlobalClass]
public partial class BossResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string Title { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string IntroText { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string AdversaryName { get; set; } = "";

    /// <summary>
    /// Sandbox fixtures, mock modules, and final objective.
    /// </summary>
    [Export] public Godot.Collections.Dictionary<string, string> VfsFixtures { get; set; } = new();
    [Export] public string[] MockModulePaths { get; set; } = System.Array.Empty<string>();
    [Export] public ObjectiveResource? Objective { get; set; }

    /// <summary>
    /// Seconds before the boss "traces" the player and the run fails.
    /// Set to 0 to disable the timer (boss is then purely command-count scored).
    /// </summary>
    [Export] public int TraceTimerSeconds { get; set; } = 0;

    [Export] public int BaseXp { get; set; } = 200;
}
