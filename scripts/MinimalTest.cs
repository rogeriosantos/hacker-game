using Godot;
using HackerGame.Autoload;
using HackerGame.Resources;

namespace HackerGame;

/// <summary>
/// End-to-end integration test: load Q1, run a satisfying command, verify the
/// objective fires QuestCompleted. Exits 0 on full green.
/// </summary>
public partial class MinimalTest : Node
{
    public override async void _Ready()
    {
        var runner = GetNode<PowerShellRunner>("/root/PowerShellRunner");
        var quests = GetNode<QuestManager>("/root/QuestManager");
        var state = GetNode<GameState>("/root/GameState");

        state.ResetForDevelopment();

        var q1 = GD.Load<QuestResource>("res://content/levels/01-recon/q1_get_help.tres");
        if (q1 == null) { GD.PrintErr("[Test] could not load Q1"); GetTree().Quit(1); return; }
        GD.Print($"[Test] loaded Q1: {q1.Id} '{q1.Title}'");

        var completed = false;
        quests.QuestCompleted += id => { GD.Print($"[Test] QuestCompleted fired: {id}"); completed = true; };

        await quests.LoadQuest(q1);
        GD.Print($"[Test] quest active, sandbox={quests.ActiveSandboxDir}");

        // Q1 wants the player to discover Get-Help. Running `Get-Help Get-ChildItem`
        // emits help text that contains "Get-ChildItem" — satisfies OutputContainsObjective.
        var result = await runner.RunAsync("Get-Help Get-ChildItem");
        GD.Print($"[Test] Get-Help ran, stdout starts with: {result.Stdout.Substring(0, System.Math.Min(80, result.Stdout.Length)).Replace("\n", " | ")}");

        await quests.OnPlayerCommandResult("Get-Help Get-ChildItem", result);

        if (completed && state.CompletedQuests.Contains(q1.Id) && state.Xp >= q1.Xp)
        {
            GD.Print($"[Test] PASS — Q1 completed, XP={state.Xp}");
            GetTree().Quit(0);
        }
        else
        {
            GD.Print($"[Test] FAIL — completed={completed} state.HasQ1={state.CompletedQuests.Contains(q1.Id)} xp={state.Xp}");
            GetTree().Quit(1);
        }
    }
}
