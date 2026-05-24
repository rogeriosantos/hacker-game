using Godot;
using Godot.Collections;

namespace HackerGame.Resources;

/// <summary>
/// Passes when every sub-objective passes in order. Each step must be satisfied
/// before the next one is checked. Use for bosses: "(1) recover the token, (2)
/// extract the payload, (3) report the hash" — all three under a single
/// trace-timer.
/// </summary>
[GlobalClass]
public partial class MultiStepObjective : ObjectiveResource
{
    [Export] public Array<ObjectiveResource> Steps { get; set; } = new();

    private int _currentStep;

    public int CurrentStep => _currentStep;
    public int TotalSteps => Steps?.Count ?? 0;

    public void Reset() => _currentStep = 0;

    public override async Task<ObjectiveVerifyResult> VerifyAsync(ObjectiveContext ctx)
    {
        if (Steps == null || Steps.Count == 0)
        {
            return new ObjectiveVerifyResult(false, "MultiStepObjective.Steps is empty");
        }

        while (_currentStep < Steps.Count)
        {
            var step = Steps[_currentStep];
            if (step == null)
            {
                return new ObjectiveVerifyResult(false, $"step {_currentStep} is null");
            }
            var stepResult = await step.VerifyAsync(ctx);
            if (!stepResult.Satisfied)
            {
                return new ObjectiveVerifyResult(false,
                    $"step {_currentStep + 1}/{Steps.Count}: {stepResult.Detail}");
            }
            _currentStep++;
        }
        return new ObjectiveVerifyResult(true, $"all {Steps.Count} steps cleared");
    }
}
