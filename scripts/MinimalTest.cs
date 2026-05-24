using Godot;
using HackerGame.Autoload;
using HackerGame.Resources;

namespace HackerGame;

/// <summary>
/// Full Level 1 walkthrough integration test. Exercises the engine end-to-end:
/// load Q1 through Q4 + boss, run the "expected" command for each, verify
/// QuestCompleted fires and GameState updates.
/// </summary>
public partial class MinimalTest : Node
{
    private readonly List<string> _completions = new();

    public override async void _Ready()
    {
        var runner = GetNode<PowerShellRunner>("/root/PowerShellRunner");
        var quests = GetNode<QuestManager>("/root/QuestManager");
        var state = GetNode<GameState>("/root/GameState");

        state.ResetForDevelopment();
        quests.QuestCompleted += id => { _completions.Add(id); GD.Print($"[Test]  + completed: {id}"); };

        var level = GD.Load<LevelResource>("res://content/levels/01-recon/level.tres");
        if (level == null) { Fail("level load null"); return; }

        // Q1: discover Get-Help. Output must contain "Get-ChildItem".
        if (!await Run(level.Quests[0], runner, quests, "Get-Help Get-ChildItem"))
            { Fail("Q1 did not complete"); return; }

        // Q2: find hidden .env. -Recurse -Force discovers /home/bob/.env.
        if (!await Run(level.Quests[1], runner, quests, "Get-ChildItem -Recurse -Force"))
            { Fail("Q2 did not complete"); return; }

        // Q3: select-string for PASSPHRASE.
        if (!await Run(level.Quests[2], runner, quests, "Select-String -Path logs/access.log -Pattern PASSPHRASE"))
            { Fail("Q3 did not complete"); return; }

        // Q4: pipelines — filter for *.key.
        if (!await Run(level.Quests[3], runner, quests, "Get-ChildItem data | Where-Object Name -Like '*.key'"))
            { Fail("Q4 did not complete"); return; }

        // BOSS: stage 1 = find a .flag file
        await quests.LoadBoss(level.Boss!);
        GD.Print($"[Test] BOSS loaded: {level.Boss!.Id} sandbox={quests.ActiveSandboxDir}");

        var s1 = await runner.RunAsync("Get-ChildItem -Recurse -Force | Where-Object Name -Like '*.flag'");
        await quests.OnPlayerCommandResult("...", s1);
        if (_completions.Contains(level.Boss.Id))
            { Fail("Boss completed too early (stage1 alone should not finish multi-step)"); return; }

        var s2 = await runner.RunAsync("Get-Content target/system/.flag");
        await quests.OnPlayerCommandResult("...", s2);

        if (!_completions.Contains(level.Boss.Id))
            { Fail($"Boss did not complete after stage 2 — last stdout: {s2.Stdout.Trim()}"); return; }

        GD.Print($"[Test] FINAL  level={state.Level} xp={state.Xp}");
        GD.Print($"[Test] FINAL  completed quests = {string.Join(", ", state.CompletedQuests)}");
        GD.Print($"[Test] FINAL  completed bosses = {string.Join(", ", state.CompletedBosses)}");
        var expectedXp = level.Quests.Select(q => q!.Xp + q.BonusXpHintFree).Sum() + level.Boss.BaseXp;
        if (state.Xp != expectedXp)
            { Fail($"XP mismatch — expected {expectedXp}, got {state.Xp}"); return; }
        GD.Print("[Test] PASS — full Level 1 walkthrough green");
        GetTree().Quit(0);
    }

    private async Task<bool> Run(QuestResource? quest, PowerShellRunner runner, QuestManager quests, string command)
    {
        if (quest == null) return false;
        await quests.LoadQuest(quest);
        GD.Print($"[Test] running {quest.Id}: {command}");
        var result = await runner.RunAsync(command);
        await quests.OnPlayerCommandResult(command, result);
        return _completions.Contains(quest.Id);
    }

    private void Fail(string reason)
    {
        GD.PrintErr($"[Test] FAIL — {reason}");
        GetTree().Quit(1);
    }
}
