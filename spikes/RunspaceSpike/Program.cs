using System.Collections;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace HackerGame.Spikes;

/// <summary>
/// Phase E1 Step 2-3 spike. Proves the load-bearing tech for the entire game:
///   (1) Open an embedded PowerShell 7 runspace via Microsoft.PowerShell.SDK
///   (2) Execute a default cmdlet (Get-ChildItem) and capture PSObjects
///   (3) Build a CONSTRAINED runspace exposing only a whitelist of cmdlets;
///       prove that disallowed cmdlets fail with a teach-not-brick error
///   (4) Load a mock module via PSModulePath; prove the mock wins over the real cmdlet
///   (5) Invoke Pester against an in-memory .Tests.ps1 and read the pass/fail result
/// If all five pass, the rest of the plan is feasible.
/// </summary>
internal static class Program
{
    private const string Pass = "[32mPASS[0m";
    private const string Fail = "[31mFAIL[0m";

    private static int Main()
    {
        Console.WriteLine("=== hacker-game runspace spike ===");
        Console.WriteLine();

        var results = new List<(string Name, bool Ok, string Detail)>
        {
            Stage1_DefaultRunspace(),
            Stage2_GetChildItemReturnsObjects(),
            Stage3_ConstrainedRunspaceBlocksUnlisted(),
            Stage4_MockModuleWinsOnPsModulePath(),
            Stage5_PesterRunsAndReports(),
        };

        Console.WriteLine();
        Console.WriteLine("=== summary ===");
        foreach (var (name, ok, detail) in results)
        {
            Console.WriteLine($"{(ok ? Pass : Fail)} {name}  {detail}");
        }

        var allOk = results.All(r => r.Ok);
        Console.WriteLine();
        Console.WriteLine(allOk ? "ALL STAGES PASSED" : "ONE OR MORE STAGES FAILED");
        return allOk ? 0 : 1;
    }

    private static (string, bool, string) Stage1_DefaultRunspace()
    {
        try
        {
            using var rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault());
            rs.Open();
            var version = rs.SessionStateProxy.PSVariable.GetValue("PSVersionTable") as Hashtable
                          ?? new Hashtable();
            var psVersion = version["PSVersion"]?.ToString() ?? "(unknown)";
            return ("1. Open default runspace", true, $"PowerShell {psVersion}");
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
            ps.AddCommand("Get-ChildItem").AddParameter("Path", AppContext.BaseDirectory);
            Collection<PSObject> results = ps.Invoke();
            if (ps.HadErrors)
            {
                return ("2. Get-ChildItem returns PSObjects", false,
                    string.Join("; ", ps.Streams.Error.Select(e => e.ToString())));
            }
            return ("2. Get-ChildItem returns PSObjects", results.Count > 0,
                $"{results.Count} items in {AppContext.BaseDirectory}");
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
            // Only allow Get-Help and Get-ChildItem
            iss.Commands.Clear();
            iss.Commands.Add(new SessionStateCmdletEntry("Get-Help",
                typeof(Microsoft.PowerShell.Commands.GetHelpCommand), null));
            iss.Commands.Add(new SessionStateCmdletEntry("Get-ChildItem",
                typeof(Microsoft.PowerShell.Commands.GetChildItemCommand), null));
            iss.Commands.Add(new SessionStateCmdletEntry("Out-Default",
                typeof(Microsoft.PowerShell.Commands.OutDefaultCommand), null));

            using var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();

            // Get-ChildItem should work
            using var psGood = PowerShell.Create();
            psGood.Runspace = rs;
            psGood.AddCommand("Get-ChildItem").AddParameter("Path", AppContext.BaseDirectory);
            var goodResults = psGood.Invoke();
            var goodWorked = goodResults.Count > 0 && !psGood.HadErrors;

            // Get-Process should be blocked. The runspace throws CommandNotFoundException
            // when it can't resolve a cmdlet — that IS the success condition here (and
            // exactly the "teach-not-brick" error a player would see in the terminal).
            var badBlocked = false;
            string badMessage = "(no error thrown)";
            try
            {
                using var psBad = PowerShell.Create();
                psBad.Runspace = rs;
                psBad.AddCommand("Get-Process");
                psBad.Invoke();
                if (psBad.HadErrors)
                {
                    var err = psBad.Streams.Error.FirstOrDefault();
                    badMessage = err?.ToString() ?? "(empty error stream)";
                    badBlocked = badMessage.Contains("not recognized", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (CommandNotFoundException ex)
            {
                badBlocked = true;
                badMessage = ex.GetType().Name;
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
            // Prepend the mock module folder to PSModulePath for this runspace only
            iss.EnvironmentVariables.Add(new SessionStateVariableEntry(
                "PSModulePath",
                mockDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PSModulePath"),
                "scoped to runspace"));
            iss.ImportPSModule(new[] { "MockProcesses" });

            using var rs = RunspaceFactory.CreateRunspace(iss);
            rs.Open();
            using var ps = PowerShell.Create();
            ps.Runspace = rs;
            ps.AddCommand("Get-Process");
            var results = ps.Invoke();

            // The mock should return exactly 2 fake processes; the real Get-Process returns many real ones.
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

            // Expect exactly 1 passed and 1 failed from our crafted test file
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
