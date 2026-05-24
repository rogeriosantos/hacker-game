using Godot;
using HackerGame.Autoload;
using HackerGame.Resources;

namespace HackerGame.UI;

/// <summary>
/// Drives the player through a LevelResource's quests in order, then the boss.
/// Attached to main.tscn. Owns the "what should the player be doing right now"
/// decision: on _Ready it picks up where the save left off; on QuestCompleted
/// it advances; on BossCompleted it unlocks the next level (if there is one).
/// Emits its own SequenceChanged signal so the HUD can refresh.
/// </summary>
public partial class QuestSequencer : Node
{
    [Export] public LevelResource? StartingLevel { get; set; }

    [Signal] public delegate void SequenceChangedEventHandler();

    public LevelResource? CurrentLevel { get; private set; }
    public QuestResource? CurrentQuest { get; private set; }
    public BossResource? CurrentBoss { get; private set; }
    public bool ShowingLevelComplete { get; private set; }

    private QuestManager _questManager = default!;
    private GameState _state = default!;

    public override void _Ready()
    {
        _questManager = GetNode<QuestManager>("/root/QuestManager");
        _state = GetNode<GameState>("/root/GameState");

        _questManager.QuestCompleted += OnQuestCompleted;

        CurrentLevel = StartingLevel;
        if (CurrentLevel == null)
        {
            GD.PushWarning("[QuestSequencer] StartingLevel not assigned");
            return;
        }

        // Resume: pick the first uncompleted quest. If all done, jump to boss.
        CallDeferred(nameof(StartCurrentLevel));
    }

    public override void _ExitTree()
    {
        if (_questManager != null) _questManager.QuestCompleted -= OnQuestCompleted;
    }

    private async void StartCurrentLevel()
    {
        if (CurrentLevel == null) return;

        QuestResource? nextQuest = null;
        foreach (var q in CurrentLevel.Quests)
        {
            if (q == null) continue;
            if (!_state.CompletedQuests.Contains(q.Id))
            {
                nextQuest = q;
                break;
            }
        }

        if (nextQuest != null)
        {
            CurrentQuest = nextQuest;
            CurrentBoss = null;
            await _questManager.LoadQuest(nextQuest);
            EmitSignal(SignalName.SequenceChanged);
            return;
        }

        // All quests done. Boss?
        if (CurrentLevel.Boss != null && !_state.CompletedBosses.Contains(CurrentLevel.Boss.Id))
        {
            CurrentQuest = null;
            CurrentBoss = CurrentLevel.Boss;
            await _questManager.LoadBoss(CurrentLevel.Boss);
            EmitSignal(SignalName.SequenceChanged);
            return;
        }

        // Boss done — level complete. Unlock next level if chained.
        CurrentQuest = null;
        CurrentBoss = null;
        ShowingLevelComplete = true;
        if (CurrentLevel.NextLevel != null)
        {
            _state.UnlockLevel(CurrentLevel.NextLevel.Number);
        }
        await _questManager.UnloadCurrent();
        EmitSignal(SignalName.SequenceChanged);
    }

    private void OnQuestCompleted(string id)
    {
        // Re-evaluate after the active objective satisfies. Defer a frame to
        // let signals fan out before reconfiguring the runner.
        CallDeferred(nameof(StartCurrentLevel));
    }
}
