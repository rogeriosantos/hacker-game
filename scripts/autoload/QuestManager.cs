using Godot;
using HackerGame.Resources;

namespace HackerGame.Autoload;

/// <summary>
/// Manages the active quest. Autoloaded as /root/QuestManager.
/// Owns the per-quest sandbox directory, configures PowerShellRunner, and runs
/// the objective check after every player command (via OnPlayerCommandResult).
/// </summary>
public partial class QuestManager : Node
{
    [Signal] public delegate void QuestLoadedEventHandler(string questId);
    [Signal] public delegate void QuestCompletedEventHandler(string questId);
    [Signal] public delegate void QuestFailedEventHandler(string questId, string reason);
    [Signal] public delegate void HintRevealedEventHandler(string questId, int tier, string text);

    public QuestResource? ActiveQuest { get; private set; }
    public BossResource? ActiveBoss { get; private set; }
    public string? ActiveSandboxDir { get; private set; }
    public int CommandsThisQuest { get; private set; }
    public int HintsTakenThisQuest { get; private set; }

    private PowerShellRunner _runner = default!;
    private GameState _state = default!;

    public override void _Ready()
    {
        _runner = GetNode<PowerShellRunner>("/root/PowerShellRunner");
        _state = GetNode<GameState>("/root/GameState");
    }

    public override void _ExitTree()
    {
        TryCleanupSandbox();
    }

    public bool IsActive => ActiveQuest != null || ActiveBoss != null;
    public bool IsBoss => ActiveBoss != null;

    // -- quest lifecycle --

    public async Task LoadQuest(QuestResource quest)
    {
        if (quest == null) return;
        await UnloadCurrent();

        ActiveQuest = quest;
        ActiveBoss = null;
        CommandsThisQuest = 0;
        HintsTakenThisQuest = 0;

        var sandbox = CreateSandbox($"quest-{quest.Id}");
        WriteFixtures(sandbox, quest.VfsFixtures);
        ResolveAndConfigureRunner(sandbox, quest.AllowedCmdlets, quest.MockModulePaths);
        ActiveSandboxDir = sandbox;

        // Reset multi-step state machines so retries start fresh.
        if (quest.Objective is MultiStepObjective multi) multi.Reset();

        EmitSignal(SignalName.QuestLoaded, quest.Id);
        await Task.CompletedTask;
    }

    public async Task LoadBoss(BossResource boss)
    {
        if (boss == null) return;
        await UnloadCurrent();

        ActiveBoss = boss;
        ActiveQuest = null;
        CommandsThisQuest = 0;
        HintsTakenThisQuest = 0;

        var sandbox = CreateSandbox($"boss-{boss.Id}");
        WriteFixtures(sandbox, boss.VfsFixtures);
        ResolveAndConfigureRunner(sandbox, System.Array.Empty<string>(), boss.MockModulePaths);
        ActiveSandboxDir = sandbox;

        if (boss.Objective is MultiStepObjective multi) multi.Reset();

        EmitSignal(SignalName.QuestLoaded, boss.Id);
    }

    /// <summary>
    /// Called by TerminalController after every command executes. Increments
    /// the command counter, runs the objective check, emits QuestCompleted on
    /// success.
    /// </summary>
    public async Task OnPlayerCommandResult(string command, PowerShellRunner.PSResult result)
    {
        if (!IsActive) return;
        CommandsThisQuest++;

        var objective = ActiveQuest?.Objective ?? ActiveBoss?.Objective;
        if (objective == null) return;

        var ctx = new ObjectiveContext(
            PlayerCommand: command,
            LastResult: result,
            SandboxDir: ActiveSandboxDir ?? "",
            Runner: _runner);

        var verify = await objective.VerifyAsync(ctx);
        if (!verify.Satisfied) return;

        if (ActiveQuest is { } q)
        {
            _state.MarkQuestComplete(q.Id, HintsTakenThisQuest > 0, q.Xp, q.BonusXpHintFree);
            _state.UnlockCmdlets(q.CmdletsUnlockedOnCompletion);
            EmitSignal(SignalName.QuestCompleted, q.Id);
        }
        else if (ActiveBoss is { } b)
        {
            _state.MarkBossComplete(b.Id, b.BaseXp);
            EmitSignal(SignalName.QuestCompleted, b.Id);
        }
    }

    public HintTier? TakeNextHint()
    {
        var hints = ActiveQuest?.Hints;
        if (hints == null || hints.Count == 0) return null;
        if (HintsTakenThisQuest >= hints.Count) return null;
        var tier = hints[HintsTakenThisQuest];
        HintsTakenThisQuest++;
        if (ActiveQuest != null) _state.NoteHintTaken(ActiveQuest.Id);
        EmitSignal(SignalName.HintRevealed, ActiveQuest?.Id ?? "", HintsTakenThisQuest - 1, tier?.Text ?? "");
        return tier;
    }

    public async Task UnloadCurrent()
    {
        TryCleanupSandbox();
        ActiveQuest = null;
        ActiveBoss = null;
        ActiveSandboxDir = null;
        _runner.ResetToDefault();
        await Task.CompletedTask;
    }

    // -- helpers --

    private void TryCleanupSandbox()
    {
        if (string.IsNullOrEmpty(ActiveSandboxDir)) return;
        try
        {
            if (Directory.Exists(ActiveSandboxDir))
            {
                Directory.Delete(ActiveSandboxDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[QuestManager] sandbox cleanup failed: {ex.Message}");
        }
    }

    private static string CreateSandbox(string label)
    {
        var safe = string.Concat(label.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'));
        var dir = Path.Combine(Path.GetTempPath(), $"hacker-game-{safe}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFixtures(string sandboxDir, Godot.Collections.Dictionary<string, string>? fixtures)
    {
        if (fixtures == null) return;
        foreach (var kv in fixtures)
        {
            var rel = kv.Key.ToString() ?? "";
            var content = kv.Value.ToString() ?? "";
            if (string.IsNullOrEmpty(rel)) continue;
            var full = Path.IsPathRooted(rel) ? rel : Path.Combine(sandboxDir, rel);
            var parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            File.WriteAllText(full, content);
        }
    }

    private void ResolveAndConfigureRunner(string sandboxDir, string[] allowed, string[] mockPaths)
    {
        // Convert sandbox-relative mock paths to absolute.
        var resolved = (mockPaths ?? System.Array.Empty<string>())
            .Select(p => Path.IsPathRooted(p) ? p : Path.Combine(sandboxDir, p))
            .Where(Directory.Exists)
            .ToArray();
        _runner.ConfigureForQuest(sandboxDir, allowed, resolved);
    }
}
