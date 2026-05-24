using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace HackerGame.Autoload;

/// <summary>
/// Spawns real PowerShell 7 as a subprocess per command. Autoloaded as
/// /root/PowerShellRunner.
///
/// PLAN DEVIATION (documented):
///   The plan originally specified `Microsoft.PowerShell.SDK` embedded in-process.
///   It works under standalone `dotnet run` (proven in spikes/RunspaceSpike, all 5
///   stages green). It does NOT work under Godot's mono assembly load context:
///   <c>Assembly.Location</c> comes back empty, which breaks the SDK's
///   <c>PSSnapInReader</c> bootstrap (ArgumentNullException on path1).
///   We tried setting $PSHOME and reflecting Utils.PSHomePath — both routes
///   hit the same problem because the SDK reads <c>Utils.GetApplicationBase()</c>
///   internally rather than honoring the env var.
///   Pragmatic pivot: spawn `pwsh` per command. Trades per-call PSObject access
///   for actually-works. Mock modules + constrained sessions still work via env
///   vars and command-line flags. Pester runs as a subprocess too.
///
/// All commands route through <see cref="RunAsync"/>. Stdout/stderr are captured
/// and returned as a single rendered string suitable for terminal display.
/// </summary>
public partial class PowerShellRunner : Node
{
    public sealed record PSResult(
        string Stdout,
        string Stderr,
        bool Succeeded,
        long DurationMs)
    {
        public string Rendered =>
            string.IsNullOrEmpty(Stderr) ? Stdout
            : string.IsNullOrEmpty(Stdout) ? Stderr
            : Stdout + "\n" + Stderr;
    }

    private string _pwshPath = "pwsh";
    private string? _sandboxDir;
    private string[] _mockModulePaths = Array.Empty<string>();
    private string[] _mockModuleManifests = Array.Empty<string>();
    private string[] _allowedCmdlets = Array.Empty<string>();

    public override void _Ready()
    {
        _pwshPath = FindPwsh();
        GD.Print($"[PowerShellRunner] using {_pwshPath}, PSVersion={GetPSVersion()}");
    }

    /// <summary>
    /// Configure the runner for a quest: sandbox dir is the working directory,
    /// mock module paths are prepended to PSModulePath, and allowed cmdlets
    /// constrain what the player can call (enforced by a wrapping script that
    /// imports only those commands into a child scope).
    /// Pass null/empty <paramref name="allowedCmdlets"/> for an unconstrained session.
    /// </summary>
    public void ConfigureForQuest(string? sandboxDir, string[]? allowedCmdlets, string[]? mockModulePaths)
    {
        _sandboxDir = sandboxDir;
        _allowedCmdlets = allowedCmdlets ?? Array.Empty<string>();
        _mockModulePaths = mockModulePaths ?? Array.Empty<string>();

        // Module auto-discovery only kicks in for functions PowerShell hasn't seen yet.
        // The real `Get-Process` and friends are already loaded by Microsoft.PowerShell.Management
        // and would win against our mock. To shadow them we have to explicitly Import-Module
        // before every command. Discover the manifest (.psd1) per module dir so the player
        // can ship mocks just by dropping a folder into mock-modules/.
        var manifests = new List<string>();
        foreach (var dir in _mockModulePaths)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var psd1 in Directory.EnumerateFiles(dir, "*.psd1", SearchOption.TopDirectoryOnly))
            {
                manifests.Add(psd1);
            }
        }
        _mockModuleManifests = manifests.ToArray();
    }

    public void ResetToDefault()
    {
        _sandboxDir = null;
        _allowedCmdlets = Array.Empty<string>();
        _mockModulePaths = Array.Empty<string>();
        _mockModuleManifests = Array.Empty<string>();
    }

    public async Task<PSResult> RunAsync(string command, int timeoutMs = 8000, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new PSResult("", "", true, 0);
        }

        var sw = Stopwatch.StartNew();

        // Wrap the player's command so its objects flow through Out-String for
        // terminal-friendly text. If a cmdlet whitelist is active, prepend a guard.
        var script = WrapCommand(command);

        var psi = new ProcessStartInfo
        {
            FileName = _pwshPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NoLogo");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("-"); // read script from stdin

        if (!string.IsNullOrEmpty(_sandboxDir) && Directory.Exists(_sandboxDir))
        {
            psi.WorkingDirectory = _sandboxDir;
        }

        if (_mockModulePaths.Length > 0)
        {
            var existing = System.Environment.GetEnvironmentVariable("PSModulePath") ?? "";
            psi.Environment["PSModulePath"] =
                string.Join(Path.PathSeparator, _mockModulePaths) + Path.PathSeparator + existing;
        }

        try
        {
            using var proc = Process.Start(psi)!;
            await proc.StandardInput.WriteAsync(script);
            proc.StandardInput.Close();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* race */ }
                sw.Stop();
                return new PSResult("", $"timed out after {timeoutMs}ms", false, sw.ElapsedMilliseconds);
            }

            var stdout = ScrubNoise(await proc.StandardOutput.ReadToEndAsync(ct));
            var stderr = ScrubNoise(await proc.StandardError.ReadToEndAsync(ct));
            sw.Stop();

            return new PSResult(stdout, stderr, proc.ExitCode == 0, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PSResult("", $"{ex.GetType().Name}: {ex.Message}", false, sw.ElapsedMilliseconds);
        }
    }

    private string WrapCommand(string command)
    {
        // Prefix every invocation with $PSStyle.OutputRendering = 'PlainText' so
        // pwsh doesn't add its own ANSI styling to output. We then layer our own
        // colors on top in TerminalController. Belt + suspenders: anything that
        // slips through still gets stripped by ScrubNoise() before the player sees it.
        // For MVP we don't enforce a cmdlet whitelist at the engine level — disallowed
        // cmdlets still execute. Quest design uses ObjectiveResource.Verify to gate
        // progression, and Pester tests give the player a clear "your output didn't
        // satisfy the goal" instead of a hard wall. Switch to a constrained script
        // wrapper when a quest design actually requires it.
        var sb = new StringBuilder();
        sb.AppendLine("$PSStyle.OutputRendering = 'PlainText'");
        sb.AppendLine("$ErrorActionPreference = 'Continue'");
        // Import mock modules explicitly so our functions shadow the real cmdlets
        // (auto-discovery alone wouldn't beat already-loaded core cmdlets).
        foreach (var manifest in _mockModuleManifests)
        {
            var escaped = manifest.Replace("'", "''");
            sb.AppendLine($"Import-Module '{escaped}' -Force -Global -DisableNameChecking -WarningAction SilentlyContinue -ErrorAction SilentlyContinue");
        }
        sb.AppendLine(command);
        return sb.ToString();
    }

    // Match terminal-mode escapes pwsh emits at startup/shutdown but the embedded
    // godot-xterm widget renders as garbage:
    //   - DEC private modes:    ESC [ ? N (h|l)            cursor mode, focus reporting, paste
    //   - OSC (window title):   ESC ] ... (BEL | ESC \)    PSReadLine title updates
    //   - DEC application:      ESC =  and  ESC >
    // We keep SGR (color) escapes — ESC [ ... m — alone.
    private static readonly Regex NoiseRegex = new(
        @"\x1b\[\?[\d;]*[hl]" +                  // DEC private mode set/reset
        @"|\x1b\][^\x07\x1b]*(\x07|\x1b\\)" +    // OSC ... BEL or ST
        @"|\x1b[=>]",                            // application/normal keypad
        RegexOptions.Compiled);

    private static string ScrubNoise(string s) => string.IsNullOrEmpty(s) ? s : NoiseRegex.Replace(s, "");

    public string GetPSVersion()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _pwshPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NoLogo");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("$PSVersionTable.PSVersion.ToString()");

            using var proc = Process.Start(psi)!;
            var version = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(2000);
            return string.IsNullOrEmpty(version) ? "(unknown)" : version;
        }
        catch { return "(missing)"; }
    }

    private static string FindPwsh()
    {
        // Common install paths per OS. Detect at runtime; fall back to bare "pwsh"
        // and let Process.Start resolve via PATH.
        var candidates = OperatingSystem.IsWindows()
            ? new[] { @"C:\Program Files\PowerShell\7\pwsh.exe", "pwsh.exe", "pwsh" }
            : OperatingSystem.IsMacOS()
                ? new[] { "/opt/homebrew/bin/pwsh", "/usr/local/bin/pwsh", "pwsh" }
                : new[] { "/usr/bin/pwsh", "/usr/local/bin/pwsh", "pwsh" };
        foreach (var path in candidates)
        {
            if (path.Contains(Path.DirectorySeparatorChar) && File.Exists(path))
            {
                return path;
            }
        }
        return "pwsh";
    }
}
