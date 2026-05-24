using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Godot;

namespace HackerGame.Spikes;

/// <summary>
/// Same 5 stages as spikes/RunspaceSpike/Program.cs, but running inside the
/// Godot .NET host instead of standalone dotnet. Proves the embedded PowerShell
/// SDK works under Godot's assembly load context. Run via:
///   godot-mono --headless --path . spikes/runspace_spike.tscn
/// </summary>
public partial class RunspaceSpikeNode : Node
{
    public override void _Ready()
    {
        GD.Print("=== hacker-game in-Godot runspace spike ===");

        var results = new List<(string Name, bool Ok, string Detail)>
        {
            Stage1_DefaultRunspace(),
            Stage2_GetChildItemReturnsObjects(),
            Stage3_ConstrainedRunspaceBlocksUnlisted(),
            Stage4_MockModuleWinsOnPsModulePath(),
            Stage5_PesterRunsAndReports(),
        };

        GD.Print();
        GD.Print("=== summary ===");
        var allOk = true;
        foreach (var (name, ok, detail) in results)
        {
            GD.Print($"{(ok ? "PASS" : "FAIL")} {name}  {detail}");
            allOk &= ok;
        }
        GD.Print();
        GD.Print(allOk ? "ALL STAGES PASSED (in Godot)" : "ONE OR MORE STAGES FAILED");

        GetTree().Quit(allOk ? 0 : 1);
    }

    private static (string, bool, string) Stage1_DefaultRunspace()
    {
        try
        {
            using var rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault());
            rs.Open();
            var version = rs.SessionStateProxy.PSVariable.GetValue("PSVersionTable") as Hashtable
                          ?? new Hashtable();
            return ("1. Open default runspace", true,
                $"PowerShell {version["PSVersion"]?.ToString() ?? "(unknown)"}");
        }
        catch (Exception ex)
        {
            return ("1. Open default runspace", false, ex.Message);
        }
    }

    private static (string, bool, string) Stage2_GetChildItemReturnsObjects()
    {
        try
        {
            using var rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault());
            rs.Open();
            using var ps = PowerShell.Create();
            ps.Runspace = rs;
            ps.AddCommand("Get-ChildItem").AddParameter("Path", System.AppContext.BaseDirectory);
            Collection<PSObject> results = ps.Invoke();
            if (ps.HadErrors)
            {
                return ("2. Get-ChildItem returns PSObjects", false,
                    string.Join("; ", ps.Streams.Error.Select(e => e.ToString())));
            }
            return ("2. Get-ChildItem returns PSObjects", results.Count > 0,
                $"{results.Count} items");
        }
        catch (Exception ex)
        {
            return ("2. Get-ChildItem returns PSObjects", false, ex.Message);
        }
    }

    private static (string, bool, string) Stage3_ConstrainedRunspaceBlocksUnlisted()
    {
        try
        {
            var iss = InitialSessionState.CreateDefault2();
            iss.LanguageMode = PSLanguageMode.FullLanguage;
            iss.Commands.Clear();
            iss.Commands.Add(new SessionStateCmdletEntry("Get-Help",
                typeof(Microsoft.PowerShell.Commands.GetHelpCommand), null));
            iss.Commands.Add(new SessionStateCmdletEntry("Get-ChildItem",
                typeof(Microsoft.PowerShell.Commands.GetChildItemCommand), null));
            iss.Commands.Add(new SessionStateCmdletEntry("Out-Default",
                typeof(Microsoft.PowerShell.Commands.OutDefaultCommand), null));

            using var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();

            using var psGood = PowerShell.Create();
            psGood.Runspace = rs;
            psGood.AddCommand("Get-ChildItem").AddParameter("Path", System.AppContext.BaseDirectory);
            var goodResults = psGood.Invoke();
            var goodWorked = goodResults.Count > 0 && !psGood.HadErrors;

            var badBlocked = false;
            try
            {
                using var psBad = PowerShell.Create();
                psBad.Runspace = rs;
                psBad.AddCommand("Get-Process");
                psBad.Invoke();
                if (psBad.HadErrors)
                {
                    var err = psBad.Streams.Error.FirstOrDefault();
                    badBlocked = (err?.ToString() ?? "").Contains("not recognized",
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (CommandNotFoundException)
            {
                badBlocked = true;
            }

            return ("3. Constrained ISS blocks unlisted cmdlets",
                goodWorked && badBlocked,
                $"allowed-worked={goodWorked}, disallowed-blocked={badBlocked}");
        }
        catch (Exception ex)
        {
            return ("3. Constrained ISS blocks unlisted cmdlets", false, ex.Message);
        }
    }

    private static (string, bool, string) Stage4_MockModuleWinsOnPsModulePath()
    {
        var mockDir = Path.Combine(Path.GetTempPath(), $"hg-mock-{Guid.NewGuid():N}");
        var moduleDir = Path.Combine(mockDir, "MockProcesses");
        try
        {
            Directory.CreateDirectory(moduleDir);
            File.WriteAllText(Path.Combine(moduleDir, "MockProcesses.psm1"), """
                function Get-Process {
                    [PSCustomObject]@{ Id = 1337; Name = 'mock-pwn.exe'; CPU = 999.9 }
                    [PSCustomObject]@{ Id = 1338; Name = 'cartel-ledger.exe'; CPU = 42.0 }
                }
                Export-ModuleMember -Function Get-Process
                """);
            File.WriteAllText(Path.Combine(moduleDir, "MockProcesses.psd1"), """
                @{
                    ModuleVersion = '0.1.0'
                    RootModule = 'MockProcesses.psm1'
                    FunctionsToExport = @('Get-Process')
                    GUID = '11111111-2222-3333-4444-555555555555'
                    Author = 'hacker-game'
                }
                """);

            var iss = InitialSessionState.CreateDefault();
            iss.EnvironmentVariables.Add(new SessionStateVariableEntry(
                "PSModulePath",
                mockDir + Path.PathSeparator + System.Environment.GetEnvironmentVariable("PSModulePath"),
                "scoped to runspace"));
            iss.ImportPSModule(new[] { "MockProcesses" });

            using var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            using var ps = PowerShell.Create();
            ps.Runspace = rs;
            ps.AddCommand("Get-Process");
            var results = ps.Invoke();

            var names = results.Select(o => o.Properties["Name"]?.Value?.ToString() ?? "").ToList();
            var mockWon = results.Count == 2
                && names.Contains("mock-pwn.exe")
                && names.Contains("cartel-ledger.exe");

            return ("4. Mock module wins on PSModulePath", mockWon,
                $"results={results.Count}, names=[{string.Join(", ", names)}]");
        }
        catch (Exception ex)
        {
            return ("4. Mock module wins on PSModulePath", false, ex.Message);
        }
        finally
        {
            try { Directory.Delete(mockDir, true); } catch { /* ignore */ }
        }
    }

    private static (string, bool, string) Stage5_PesterRunsAndReports()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"hg-pester-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(testDir);
            var testFile = Path.Combine(testDir, "Q1.Tests.ps1");
            File.WriteAllText(testFile, """
                Describe 'Q1 — Recon' {
                    It 'passes when 2+2 is 4' {
                        2 + 2 | Should -Be 4
                    }
                    It 'fails when 2+2 is claimed to be 5' {
                        2 + 2 | Should -Be 5
                    }
                }
                """);

            var iss = InitialSessionState.CreateDefault();
            iss.ImportPSModule(new[] { "Pester" });

            using var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            using var ps = PowerShell.Create();
            ps.Runspace = rs;
            ps.AddScript($"Invoke-Pester -Path '{testFile.Replace("'", "''")}' -PassThru -Output None");
            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var firstErr = ps.Streams.Error.FirstOrDefault()?.ToString() ?? "(unknown)";
                return ("5. Pester runs and reports pass/fail", false, $"errors: {firstErr}");
            }

            var run = results.FirstOrDefault();
            if (run is null)
            {
                return ("5. Pester runs and reports pass/fail", false, "no result object");
            }

            var passed = (int?)(run.Properties["PassedCount"]?.Value) ?? -1;
            var failed = (int?)(run.Properties["FailedCount"]?.Value) ?? -1;
            var ok = passed == 1 && failed == 1;
            return ("5. Pester runs and reports pass/fail", ok,
                $"passed={passed}, failed={failed} (expected 1 and 1)");
        }
        catch (Exception ex)
        {
            return ("5. Pester runs and reports pass/fail", false, ex.Message);
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { /* ignore */ }
        }
    }
}
