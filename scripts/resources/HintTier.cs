using Godot;

namespace HackerGame.Resources;

public enum HintLevel
{
    Nudge = 0,
    Approach = 1,
    Walkthrough = 2,
}

/// <summary>
/// A single hint within a quest. Tiered (picoCTF-style) — the player reveals
/// hints progressively and only loses the "no-hint" bonus, never base XP.
/// </summary>
[GlobalClass]
public partial class HintTier : Resource
{
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
    [Export] public HintLevel Level { get; set; } = HintLevel.Nudge;
}
