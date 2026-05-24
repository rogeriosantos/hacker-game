using Godot;
using HackerGame.Autoload;
using HackerGame.Resources;

namespace HackerGame.UI;

/// <summary>
/// HUD overlay: level/XP/current quest/narrative/hints. Listens to GameState
/// signals + QuestSequencer.SequenceChanged to refresh.
/// Wired in main.tscn — assumes a specific subtree (see [Export] paths).
/// </summary>
public partial class HudController : Control
{
    [Export] public NodePath LevelLabelPath { get; set; } = "";
    [Export] public NodePath XpLabelPath { get; set; } = "";
    [Export] public NodePath QuestTitleLabelPath { get; set; } = "";
    [Export] public NodePath QuestNarrativeLabelPath { get; set; } = "";
    [Export] public NodePath HintButtonPath { get; set; } = "";
    [Export] public NodePath HintTextLabelPath { get; set; } = "";
    [Export] public NodePath StatusLabelPath { get; set; } = "";
    [Export] public NodePath SequencerPath { get; set; } = "";

    private Label _level = default!;
    private Label _xp = default!;
    private Label _questTitle = default!;
    private Label _questNarrative = default!;
    private Button _hintButton = default!;
    private Label _hintText = default!;
    private Label _status = default!;
    private QuestSequencer _sequencer = default!;

    private GameState _state = default!;
    private QuestManager _questManager = default!;

    public override void _Ready()
    {
        _level = GetNode<Label>(LevelLabelPath);
        _xp = GetNode<Label>(XpLabelPath);
        _questTitle = GetNode<Label>(QuestTitleLabelPath);
        _questNarrative = GetNode<Label>(QuestNarrativeLabelPath);
        _hintButton = GetNode<Button>(HintButtonPath);
        _hintText = GetNode<Label>(HintTextLabelPath);
        _status = GetNode<Label>(StatusLabelPath);
        _sequencer = GetNode<QuestSequencer>(SequencerPath);

        _state = GetNode<GameState>("/root/GameState");
        _questManager = GetNode<QuestManager>("/root/QuestManager");

        _state.XpChanged += _ => Refresh();
        _state.LevelChanged += _ => Refresh();
        _state.QuestCompleted += id => FlashStatus($"quest {id} cleared", green: true);
        _state.BossCompleted += id => FlashStatus($"BOSS {id} DOWN", green: true);
        _state.CmdletUnlocked += name => FlashStatus($"unlocked cmdlet: {name}", green: true);
        _sequencer.SequenceChanged += Refresh;

        _hintButton.Pressed += OnHintPressed;
        Refresh();
    }

    private void Refresh()
    {
        _level.Text = $"LEVEL {_state.Level}  ·  {_sequencer.CurrentLevel?.Title ?? ""}";
        _xp.Text = $"XP  {_state.Xp}";

        if (_sequencer.CurrentQuest is { } q)
        {
            _questTitle.Text = q.Title;
            _questNarrative.Text = q.Narrative;
            _hintButton.Visible = q.Hints != null && q.Hints.Count > 0;
            _hintButton.Disabled = _questManager.HintsTakenThisQuest >= (q.Hints?.Count ?? 0);
            _hintText.Visible = _questManager.HintsTakenThisQuest > 0;
            if (_hintText.Visible && q.Hints != null && q.Hints.Count > 0)
            {
                _hintText.Text = q.Hints[_questManager.HintsTakenThisQuest - 1]?.Text ?? "";
            }
        }
        else if (_sequencer.CurrentBoss is { } b)
        {
            _questTitle.Text = $"BOSS · {b.Title}";
            _questNarrative.Text = b.IntroText;
            _hintButton.Visible = false;
            _hintText.Visible = false;
        }
        else if (_sequencer.ShowingLevelComplete)
        {
            _questTitle.Text = "LEVEL COMPLETE";
            _questNarrative.Text = "Next level unlocked. (More content coming.)";
            _hintButton.Visible = false;
            _hintText.Visible = false;
        }
        else
        {
            _questTitle.Text = "";
            _questNarrative.Text = "";
            _hintButton.Visible = false;
            _hintText.Visible = false;
        }
    }

    private void OnHintPressed()
    {
        var hint = _questManager.TakeNextHint();
        if (hint != null)
        {
            _hintText.Visible = true;
            _hintText.Text = hint.Text;
            _hintButton.Disabled = _questManager.HintsTakenThisQuest >= (_sequencer.CurrentQuest?.Hints?.Count ?? 0);
        }
    }

    private void FlashStatus(string text, bool green)
    {
        _status.Modulate = green ? new Color(0.6f, 1.0f, 0.6f) : new Color(1.0f, 0.5f, 0.5f);
        _status.Text = text;
        GetTree().CreateTimer(3.0).Timeout += () =>
        {
            if (IsInstanceValid(_status)) _status.Text = "";
        };
    }
}
