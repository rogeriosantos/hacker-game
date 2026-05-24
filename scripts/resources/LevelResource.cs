using Godot;
using Godot.Collections;

namespace HackerGame.Resources;

/// <summary>
/// A pack of quests + one boss. Completing every quest unlocks the boss.
/// Beating the boss unlocks the next LevelResource (chained via
/// <see cref="NextLevel"/>).
/// </summary>
[GlobalClass]
public partial class LevelResource : Resource
{
    [Export] public int Number { get; set; } = 1;
    [Export] public string Title { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Theme { get; set; } = "";
    [Export] public string VerbFamily { get; set; } = "Get-*";
    [Export] public string Biome { get; set; } = "FileSystem:\\";

    [Export] public Array<QuestResource> Quests { get; set; } = new();
    [Export] public BossResource? Boss { get; set; }

    [Export] public LevelResource? NextLevel { get; set; }
}
