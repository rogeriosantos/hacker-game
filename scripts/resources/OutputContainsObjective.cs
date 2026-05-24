using System.Text.RegularExpressions;
using Godot;

namespace HackerGame.Resources;

/// <summary>
/// Passes when the player's last command output contains the configured
/// substring (or matches the configured regex, if <see cref="IsRegex"/> is set).
/// Simplest objective type — use for early quests where the goal is "find
/// and print path X" or "your output should mention secret.key".
/// </summary>
[GlobalClass]
public partial class OutputContainsObjective : ObjectiveResource
{
    [Export(PropertyHint.MultilineText)] public string Needle { get; set; } = "";
    [Export] public bool IsRegex { get; set; } = false;
    [Export] public bool CaseSensitive { get; set; } = false;

    public override Task<ObjectiveVerifyResult> VerifyAsync(ObjectiveContext ctx)
    {
        var haystack = ctx.LastResult?.Stdout ?? "";
        if (string.IsNullOrEmpty(Needle))
        {
            return Task.FromResult(new ObjectiveVerifyResult(false, "OutputContainsObjective.Needle is empty"));
        }

        bool match;
        if (IsRegex)
        {
            var opts = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            match = Regex.IsMatch(haystack, Needle, opts);
        }
        else
        {
            var cmp = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            match = haystack.Contains(Needle, cmp);
        }

        return Task.FromResult(new ObjectiveVerifyResult(match,
            match ? "" : "your output didn't include the expected value yet"));
    }
}
