using Godot;

namespace HackerGame.Resources;

/// <summary>
/// Passes when a Pester `.Tests.ps1` file (relative to the quest folder or
/// sandbox) reports 0 failed tests. The PSKoans pattern: the author writes
/// a test, the player writes any pipeline that makes it pass. Solves the
/// "guess the author's one-liner" anti-pattern flagged in the landscape
/// research.
/// </summary>
[GlobalClass]
public partial class PesterTestObjective : ObjectiveResource
{
    [Export(PropertyHint.MultilineText)] public string TestFilePath { get; set; } = "";

    public override async Task<ObjectiveVerifyResult> VerifyAsync(ObjectiveContext ctx)
    {
        if (string.IsNullOrEmpty(TestFilePath))
        {
            return new ObjectiveVerifyResult(false, "PesterTestObjective.TestFilePath is empty");
        }

        var full = System.IO.Path.IsPathRooted(TestFilePath)
            ? TestFilePath
            : System.IO.Path.Combine(ctx.SandboxDir, TestFilePath);
        if (!System.IO.File.Exists(full))
        {
            return new ObjectiveVerifyResult(false, $"test file not found: {TestFilePath}");
        }

        // Run Pester via the same PowerShellRunner. Output a single JSON object
        // with pass/fail counts so we can parse it without depending on the
        // PSObject pipeline.
        var script =
            "Import-Module Pester -ErrorAction Stop\n" +
            $"$result = Invoke-Pester -Path '{full.Replace("'", "''")}' -PassThru -Output None\n" +
            "[PSCustomObject]@{ Passed = $result.PassedCount; Failed = $result.FailedCount; Total = $result.TotalCount } | ConvertTo-Json -Compress\n";

        var run = await ctx.Runner.RunAsync(script, timeoutMs: 15000);
        if (!run.Succeeded || string.IsNullOrWhiteSpace(run.Stdout))
        {
            return new ObjectiveVerifyResult(false, $"pester failed to run: {run.Stderr.Trim()}");
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(run.Stdout.Trim());
            var root = doc.RootElement;
            var passed = root.GetProperty("Passed").GetInt32();
            var failed = root.GetProperty("Failed").GetInt32();
            if (failed == 0 && passed > 0)
            {
                return new ObjectiveVerifyResult(true, $"all {passed} tests passed");
            }
            return new ObjectiveVerifyResult(false, $"{passed} passed, {failed} failed");
        }
        catch (Exception ex)
        {
            return new ObjectiveVerifyResult(false, $"could not parse pester result: {ex.Message}");
        }
    }
}
