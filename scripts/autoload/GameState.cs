using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace HackerGame.Autoload;

/// <summary>
/// Persistent player state. Autoloaded as /root/GameState. Saves to
/// user://save.json on every state change via debounced write.
/// Emits signals so HUD / world map etc. can react reactively.
/// </summary>
public partial class GameState : Node
{
    public sealed record StateSnapshot(
        int Level,
        int Xp,
        HashSet<string> CompletedQuests,
        HashSet<string> UnlockedCmdlets,
        HashSet<string> QuestsWithHintsTaken,
        HashSet<string> CompletedBosses,
        Dictionary<string, object> Settings);

    [Signal] public delegate void XpChangedEventHandler(int xp);
    [Signal] public delegate void LevelChangedEventHandler(int level);
    [Signal] public delegate void QuestCompletedEventHandler(string questId);
    [Signal] public delegate void BossCompletedEventHandler(string bossId);
    [Signal] public delegate void CmdletUnlockedEventHandler(string cmdletName);

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; } = 0;
    public HashSet<string> CompletedQuests { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CompletedBosses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UnlockedCmdlets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> QuestsWithHintsTaken { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object> Settings { get; } = new();

    private const string SavePath = "user://save.json";

    public override void _Ready()
    {
        Load();
        GD.Print($"[GameState] loaded level={Level} xp={Xp} quests={CompletedQuests.Count} cmdlets={UnlockedCmdlets.Count}");
    }

    // -- mutators --

    public void AwardXp(int amount)
    {
        if (amount == 0) return;
        Xp += amount;
        EmitSignal(SignalName.XpChanged, Xp);
        Save();
    }

    public void MarkQuestComplete(string questId, bool hintsTaken, int baseXp, int bonusXpHintFree)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (CompletedQuests.Add(questId))
        {
            var award = baseXp + (hintsTaken ? 0 : bonusXpHintFree);
            AwardXp(award);
            EmitSignal(SignalName.QuestCompleted, questId);
            Save();
        }
    }

    public void MarkBossComplete(string bossId, int xp)
    {
        if (string.IsNullOrEmpty(bossId)) return;
        if (CompletedBosses.Add(bossId))
        {
            AwardXp(xp);
            EmitSignal(SignalName.BossCompleted, bossId);
            Save();
        }
    }

    public void UnlockLevel(int newLevel)
    {
        if (newLevel > Level)
        {
            Level = newLevel;
            EmitSignal(SignalName.LevelChanged, Level);
            Save();
        }
    }

    public void UnlockCmdlets(string[] names)
    {
        var anyAdded = false;
        foreach (var n in names ?? System.Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(n)) continue;
            if (UnlockedCmdlets.Add(n))
            {
                EmitSignal(SignalName.CmdletUnlocked, n);
                anyAdded = true;
            }
        }
        if (anyAdded) Save();
    }

    public void NoteHintTaken(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (QuestsWithHintsTaken.Add(questId)) Save();
    }

    public bool HintsTakenFor(string questId)
        => QuestsWithHintsTaken.Contains(questId);

    // -- persistence --

    private sealed class Dto
    {
        [JsonPropertyName("level")] public int Level { get; set; } = 1;
        [JsonPropertyName("xp")] public int Xp { get; set; }
        [JsonPropertyName("completedQuests")] public List<string> CompletedQuests { get; set; } = new();
        [JsonPropertyName("completedBosses")] public List<string> CompletedBosses { get; set; } = new();
        [JsonPropertyName("unlockedCmdlets")] public List<string> UnlockedCmdlets { get; set; } = new();
        [JsonPropertyName("questsWithHintsTaken")] public List<string> QuestsWithHintsTaken { get; set; } = new();
    }

    public void Save()
    {
        var dto = new Dto
        {
            Level = Level,
            Xp = Xp,
            CompletedQuests = CompletedQuests.ToList(),
            CompletedBosses = CompletedBosses.ToList(),
            UnlockedCmdlets = UnlockedCmdlets.ToList(),
            QuestsWithHintsTaken = QuestsWithHintsTaken.ToList(),
        };
        try
        {
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
            if (f != null) f.StoreString(json);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GameState] save failed: {ex.Message}");
        }
    }

    public void Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath)) return;
        try
        {
            using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
            if (f == null) return;
            var json = f.GetAsText();
            var dto = JsonSerializer.Deserialize<Dto>(json);
            if (dto == null) return;
            Level = dto.Level;
            Xp = dto.Xp;
            CompletedQuests.Clear(); foreach (var q in dto.CompletedQuests) CompletedQuests.Add(q);
            CompletedBosses.Clear(); foreach (var b in dto.CompletedBosses) CompletedBosses.Add(b);
            UnlockedCmdlets.Clear(); foreach (var c in dto.UnlockedCmdlets) UnlockedCmdlets.Add(c);
            QuestsWithHintsTaken.Clear(); foreach (var q in dto.QuestsWithHintsTaken) QuestsWithHintsTaken.Add(q);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GameState] load failed: {ex.Message}");
        }
    }

    public void ResetForDevelopment()
    {
        Level = 1;
        Xp = 0;
        CompletedQuests.Clear();
        CompletedBosses.Clear();
        UnlockedCmdlets.Clear();
        QuestsWithHintsTaken.Clear();
        Save();
    }
}
