using Godot;
using HackerGame.Autoload;

namespace HackerGame;

/// <summary>
/// End-to-end smoke for the PowerShellRunner autoload: ask for the PowerShell
/// version and confirm a non-empty answer comes back. Exits with code 0 on success.
/// </summary>
public partial class MinimalTest : Node
{
    public override async void _Ready()
    {
        GD.Print("[MinimalTest] starting");
        var runner = GetNode<PowerShellRunner>("/root/PowerShellRunner");

        var result = await runner.RunAsync("$PSVersionTable.PSVersion.ToString()");
        var ok = result.Succeeded && !string.IsNullOrWhiteSpace(result.Stdout);
        GD.Print($"[MinimalTest] succeeded={result.Succeeded}, ms={result.DurationMs}");
        GD.Print($"[MinimalTest] stdout: {result.Stdout.Trim()}");
        if (!string.IsNullOrEmpty(result.Stderr))
        {
            GD.Print($"[MinimalTest] stderr: {result.Stderr.Trim()}");
        }

        var help = await runner.RunAsync("Get-Help -Name Get-ChildItem -Category Cmdlet | Select-Object -ExpandProperty Synopsis");
        var helpOk = help.Succeeded && !string.IsNullOrWhiteSpace(help.Stdout);
        GD.Print($"[MinimalTest] Get-Help OK={helpOk}, ms={help.DurationMs}");
        GD.Print($"[MinimalTest] Get-Help synopsis: {help.Stdout.Trim().Substring(0, System.Math.Min(120, help.Stdout.Trim().Length))}");

        GD.Print(ok && helpOk ? "[MinimalTest] PASS" : "[MinimalTest] FAIL");
        GetTree().Quit(ok && helpOk ? 0 : 1);
    }
}
