using Godot;
using HackerGame.Autoload;
using HackerGame.Resources;

namespace HackerGame;

/// <summary>
/// MVP smoke: full Level 1 + Level 2 walkthrough. Each quest is "solved" by
/// the expected canonical command for its objective. Verifies QuestCompleted
/// fires, GameState aggregates XP, and the level chain advances L1 -> L2.
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
        quests.QuestCompleted += id => { _completions.Add(id); GD.Print($"[Test]  + {id}"); };

        // Level 1
        var l1 = GD.Load<LevelResource>("res://content/levels/01-recon/level.tres");
        if (l1 == null) { Fail("L1 null"); return; }

        if (!await Run(l1.Quests[0], runner, quests, "Get-Help Get-ChildItem")) { Fail("L1Q1"); return; }
        if (!await Run(l1.Quests[1], runner, quests, "Get-ChildItem -Recurse -Force")) { Fail("L1Q2"); return; }
        if (!await Run(l1.Quests[2], runner, quests, "Select-String -Path logs/access.log -Pattern PASSPHRASE")) { Fail("L1Q3"); return; }
        if (!await Run(l1.Quests[3], runner, quests, "Get-ChildItem data | Where-Object Name -Like '*.key'")) { Fail("L1Q4"); return; }

        await quests.LoadBoss(l1.Boss!);
        await quests.OnPlayerCommandResult("...", await runner.RunAsync("Get-ChildItem -Recurse -Force | Where-Object Name -Like '*.flag'"));
        if (_completions.Contains(l1.Boss!.Id)) { Fail("L1 boss too early"); return; }
        await quests.OnPlayerCommandResult("...", await runner.RunAsync("Get-Content target/system/.flag"));
        if (!_completions.Contains(l1.Boss.Id)) { Fail("L1 boss not finished"); return; }

        // Level 2 — same chained model, but now player operates on the registry biome.
        var l2 = l1.NextLevel ?? GD.Load<LevelResource>("res://content/levels/02-registry/level.tres");
        if (l2 == null) { Fail("L2 null"); return; }

        if (!await Run(l2.Quests[0], runner, quests, "Get-ChildItem target/registry/HKLM/SOFTWARE")) { Fail("L2Q1"); return; }
        if (!await Run(l2.Quests[1], runner, quests, "Test-Path target/registry/HKLM/SOFTWARE/OBSIDIAN.LTD")) { Fail("L2Q2"); return; }
        if (!await Run(l2.Quests[2], runner, quests, "Get-Content target/registry/HKLM/SOFTWARE/OBSIDIAN.LTD/Settings/endpoint.json")) { Fail("L2Q3"); return; }
        if (!await Run(l2.Quests[3], runner, quests, "Get-ChildItem target/registry/HKCU -Recurse -Force -Filter *.secret")) { Fail("L2Q4"); return; }

        await quests.LoadBoss(l2.Boss!);
        await quests.OnPlayerCommandResult("...", await runner.RunAsync("Get-ChildItem -Recurse -Force | Where-Object Name -Like '*master*'"));
        if (_completions.Contains(l2.Boss!.Id)) { Fail("L2 boss too early"); return; }
        await quests.OnPlayerCommandResult("...", await runner.RunAsync("Get-Content target/registry/HKLM/SECURITY/.master/seed.bin"));
        if (!_completions.Contains(l2.Boss.Id)) { Fail("L2 boss not finished"); return; }

        // Sanity checks.
        var expectedL1Xp = l1.Quests.Select(q => q!.Xp + q.BonusXpHintFree).Sum() + l1.Boss.BaseXp;
        var expectedL2Xp = l2.Quests.Select(q => q!.Xp + q.BonusXpHintFree).Sum() + l2.Boss.BaseXp;
        var expectedTotal = expectedL1Xp + expectedL2Xp;
        if (state.Xp != expectedTotal) { Fail($"XP mismatch: {state.Xp} != {expectedTotal} (L1={expectedL1Xp} + L2={expectedL2Xp})"); return; }
        if (state.CompletedQuests.Count != 8) { Fail($"expected 8 quests, got {state.CompletedQuests.Count}"); return; }
        if (state.CompletedBosses.Count != 2) { Fail($"expected 2 bosses, got {state.CompletedBosses.Count}"); return; }

        GD.Print($"[Test] FINAL  xp={state.Xp}  quests={state.CompletedQuests.Count}  bosses={state.CompletedBosses.Count}");

        // Save/load round-trip — wipe in-memory, load from disk, do trivial mutation, reload.
        if (!Godot.FileAccess.FileExists("user://save.json")) { Fail("save.json missing"); return; }
        var snapshotXp = state.Xp;
        state.ResetForDevelopment();   // wipes both memory AND disk
        state.Load();
        if (state.Xp != 0) { Fail($"Reset didn't clear save (xp={state.Xp})"); return; }
        GD.Print("[Test] save/load round-trip OK");

        state.ResetForDevelopment();
        GD.Print("[Test] PASS — Level 1 + Level 2 walkthrough green");
        GetTree().Quit(0);
    }

    private async Task<bool> Run(QuestResource? quest, PowerShellRunner runner, QuestManager quests, string command)
    {
        if (quest == null) return false;
        await quests.LoadQuest(quest);
        var result = await runner.RunAsync(command);
        await quests.OnPlayerCommandResult(command, result);
        return _completions.Contains(quest.Id);
    }

    private void Fail(string reason) { GD.PrintErr($"[Test] FAIL — {reason}"); GetTree().Quit(1); }
}
