using Godot;
using HackerGame.Resources;

namespace HackerGame.UI;

/// <summary>
/// Boss-intro cinematic overlay. Fade in -> ASCII skull -> typewriter brief ->
/// flashing "PRESS ENTER" prompt -> dismiss on ENTER. Auto-dismisses after
/// <see cref="AutoDismissSeconds"/> too so a player can never get stuck.
/// Instantiated by QuestSequencer when transitioning into a boss.
/// </summary>
public partial class BossIntro : CanvasLayer
{
    [Export] public NodePath SkullLabelPath { get; set; } = "";
    [Export] public NodePath AdversaryLabelPath { get; set; } = "";
    [Export] public NodePath TitleLabelPath { get; set; } = "";
    [Export] public NodePath BriefLabelPath { get; set; } = "";
    [Export] public NodePath PromptLabelPath { get; set; } = "";
    [Export] public NodePath BackgroundPath { get; set; } = "";

    [Export] public float TypeSpeedCharsPerSec { get; set; } = 60f;
    [Export] public float AutoDismissSeconds { get; set; } = 14f;

    private const string Skull =
        "          ___              \n" +
        "        /     \\            \n" +
        "       | () () |           \n" +
        "        \\  ^  /            \n" +
        "         |||||             \n" +
        "         |||||             ";

    private Label _skull = default!;
    private Label _adversary = default!;
    private Label _title = default!;
    private Label _brief = default!;
    private Label _prompt = default!;
    private ColorRect _bg = default!;

    private string _fullBrief = "";
    private float _elapsed;
    private bool _dismissed;
    private float _autoTimer;

    public void Configure(BossResource boss)
    {
        _adversary.Text = string.IsNullOrEmpty(boss.AdversaryName) ? "" : $"// adversary: {boss.AdversaryName}";
        _title.Text = boss.Title;
        _fullBrief = boss.IntroText ?? "";
        _brief.Text = "";
    }

    public override void _Ready()
    {
        _skull = GetNode<Label>(SkullLabelPath);
        _adversary = GetNode<Label>(AdversaryLabelPath);
        _title = GetNode<Label>(TitleLabelPath);
        _brief = GetNode<Label>(BriefLabelPath);
        _prompt = GetNode<Label>(PromptLabelPath);
        _bg = GetNode<ColorRect>(BackgroundPath);

        _skull.Text = Skull;
        _prompt.Modulate = new Color(0.7f, 1, 0.7f, 0);
        Layer = 100;
    }

    public override void _Process(double delta)
    {
        if (_dismissed) return;
        _elapsed += (float)delta;
        _autoTimer += (float)delta;

        // Typewriter reveal
        var totalChars = (int)(_elapsed * TypeSpeedCharsPerSec);
        if (totalChars >= _fullBrief.Length)
        {
            _brief.Text = _fullBrief;
            // Pulse the prompt once the brief is fully revealed.
            var pulse = (Mathf.Sin((float)Time.GetTicksMsec() / 250f) + 1f) * 0.5f;
            _prompt.Modulate = new Color(0.7f, 1, 0.7f, 0.4f + pulse * 0.6f);
        }
        else
        {
            _brief.Text = _fullBrief.Substring(0, totalChars);
        }

        if (_autoTimer >= AutoDismissSeconds)
        {
            Dismiss();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_dismissed) return;
        if (@event is InputEventKey key && key.Pressed && (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space))
        {
            Dismiss();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        var tween = CreateTween();
        tween.TweenProperty(_bg, "modulate:a", 0.0, 0.6);
        tween.Parallel().TweenProperty(_skull, "modulate:a", 0.0, 0.6);
        tween.Parallel().TweenProperty(_adversary, "modulate:a", 0.0, 0.6);
        tween.Parallel().TweenProperty(_title, "modulate:a", 0.0, 0.6);
        tween.Parallel().TweenProperty(_brief, "modulate:a", 0.0, 0.6);
        tween.Parallel().TweenProperty(_prompt, "modulate:a", 0.0, 0.6);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }
}
