using Godot;

namespace HackerGame.Resources;

/// <summary>
/// Passes when a file at the given path (relative to the sandbox dir, or
/// absolute) exists, optionally with required content.
/// Use for persistence-style quests: "after your command, /home/bob/.bashrc
/// must contain the line `evil-payload`".
/// </summary>
[GlobalClass]
public partial class FileExistsObjective : ObjectiveResource
{
    [Export(PropertyHint.MultilineText)] public string Path { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string MustContain { get; set; } = "";

    public override Task<ObjectiveVerifyResult> VerifyAsync(ObjectiveContext ctx)
    {
        if (string.IsNullOrEmpty(Path))
        {
            return Task.FromResult(new ObjectiveVerifyResult(false, "FileExistsObjective.Path is empty"));
        }

        var full = System.IO.Path.IsPathRooted(Path) ? Path : System.IO.Path.Combine(ctx.SandboxDir, Path);
        if (!System.IO.File.Exists(full))
        {
            return Task.FromResult(new ObjectiveVerifyResult(false, $"file not found: {Path}"));
        }

        if (string.IsNullOrEmpty(MustContain))
        {
            return Task.FromResult(new ObjectiveVerifyResult(true));
        }

        try
        {
            var content = System.IO.File.ReadAllText(full);
            var has = content.Contains(MustContain, StringComparison.Ordinal);
            return Task.FromResult(new ObjectiveVerifyResult(has,
                has ? "" : $"file exists but doesn't contain the required text"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ObjectiveVerifyResult(false, ex.Message));
        }
    }
}
