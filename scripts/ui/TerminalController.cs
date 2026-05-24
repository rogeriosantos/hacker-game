using System.Text;
using Godot;
using HackerGame.Autoload;

namespace HackerGame.UI;

// ReSharper disable once UnusedType.Global

/// <summary>
/// Thin line-editor on top of godot-xterm's Terminal node. Captures user
/// keystrokes via the `data_sent` signal (the terminal does not auto-echo —
/// we control all output), maintains a per-line buffer + command history,
/// dispatches submitted lines to PowerShellRunner, and writes responses back.
/// Attached to the Terminal node in scenes/main.tscn.
/// </summary>
public partial class TerminalController : Node
{
    [Export] public string Prompt { get; set; } = "[32mhg[0m [36m>[0m ";
    [Export] public bool ShowBanner { get; set; } = true;
    [Export] public int MaxHistory { get; set; } = 200;

    private const byte Enter      = 0x0d;
    private const byte Backspace1 = 0x7f;
    private const byte Backspace2 = 0x08;
    private const byte Esc        = 0x1b;
    private const byte Ctrl_C     = 0x03;
    private const byte Ctrl_L     = 0x0c;

    private Node _terminal = default!;
    private PowerShellRunner _runner = default!;
    private QuestManager _questManager = default!;
    private readonly StringBuilder _buffer = new();
    private readonly List<string> _history = new();
    private int _historyCursor = -1;
    private bool _busy;

    public override void _Ready()
    {
        _terminal = GetParent();
        _runner = GetNode<PowerShellRunner>("/root/PowerShellRunner");
        _questManager = GetNode<QuestManager>("/root/QuestManager");

        _terminal.Connect("data_sent", new Callable(this, nameof(OnDataSent)));

        if (ShowBanner)
        {
            CallDeferred(nameof(WriteBannerAndPrompt));
        }
        else
        {
            CallDeferred(nameof(WritePromptOnly));
        }
    }

    private void WriteBannerAndPrompt()
    {
        string Banner =
            "\r\n" +
            "[32m  ██╗  ██╗ █████╗  ██████╗██╗  ██╗███████╗██████╗     ██████╗  █████╗ ███╗   ███╗███████╗[0m\r\n" +
            "[32m  ██║  ██║██╔══██╗██╔════╝██║ ██╔╝██╔════╝██╔══██╗   ██╔════╝ ██╔══██╗████╗ ████║██╔════╝[0m\r\n" +
            "[32m  ███████║███████║██║     █████╔╝ █████╗  ██████╔╝   ██║  ███╗███████║██╔████╔██║█████╗  [0m\r\n" +
            "[32m  ██╔══██║██╔══██║██║     ██╔═██╗ ██╔══╝  ██╔══██╗   ██║   ██║██╔══██║██║╚██╔╝██║██╔══╝  [0m\r\n" +
            "[32m  ██║  ██║██║  ██║╚██████╗██║  ██╗███████╗██║  ██║   ╚██████╔╝██║  ██║██║ ╚═╝ ██║███████╗[0m\r\n" +
            "[32m  ╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝    ╚═════╝╚═╝  ╚═╝╚═╝     ╚═╝╚══════╝[0m\r\n" +
            "\r\n" +
            "  [90m// codename hacker-game  //  PowerShell " + _runner.GetPSVersion() + "  //  type[0m " +
            "[36mGet-Help[0m [90mor[0m [36mGet-Command[0m [90mto begin[0m\r\n" +
            "\r\n";
        Write(Banner);
        WritePrompt();
    }

    private void WritePromptOnly()
    {
        Write("\r\n");
        WritePrompt();
    }

    private void OnDataSent(byte[] data)
    {
        if (_busy)
        {
            // While a command is running we ignore keystrokes except Ctrl+C (TODO: cancellation).
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];

            // ESC sequences: arrow keys are ESC [ A/B/C/D
            if (b == Esc && i + 2 < data.Length && data[i + 1] == (byte)'[')
            {
                var code = data[i + 2];
                i += 2;
                if (code == (byte)'A') HistoryPrev();
                else if (code == (byte)'B') HistoryNext();
                // C/D (left/right) ignored for MVP — single-line editing only.
                continue;
            }

            switch (b)
            {
                case Enter:
                    Write("\r\n");
                    _ = SubmitAsync(_buffer.ToString());
                    _buffer.Clear();
                    _historyCursor = -1;
                    break;

                case Backspace1:
                case Backspace2:
                    if (_buffer.Length > 0)
                    {
                        _buffer.Length--;
                        Write("\b \b");
                    }
                    break;

                case Ctrl_C:
                    _buffer.Clear();
                    Write("^C\r\n");
                    WritePrompt();
                    _historyCursor = -1;
                    break;

                case Ctrl_L:
                    Write("\x1b[2J\x1b[H");
                    WritePrompt();
                    RedrawBuffer();
                    break;

                default:
                    if (b >= 0x20 && b < 0x7f)
                    {
                        _buffer.Append((char)b);
                        Write(((char)b).ToString());
                    }
                    break;
            }
        }
    }

    private async Task SubmitAsync(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            WritePrompt();
            return;
        }

        AddToHistory(trimmed);
        _busy = true;
        try
        {
            var result = await _runner.RunAsync(trimmed);
            if (!string.IsNullOrEmpty(result.Rendered))
            {
                // Color errors red; normal output stays default.
                var color = result.Succeeded ? "" : "\x1b[31m";
                var reset = result.Succeeded ? "" : "\x1b[0m";
                Write(color + result.Rendered.Replace("\n", "\r\n") + reset + "\r\n");
            }

            // Dispatch to the active quest, if any. The QuestManager handles
            // objective verification and emits QuestCompleted on satisfy.
            if (_questManager.IsActive)
            {
                await _questManager.OnPlayerCommandResult(trimmed, result);
            }
        }
        finally
        {
            _busy = false;
            WritePrompt();
        }
    }

    private void AddToHistory(string line)
    {
        if (_history.Count == 0 || _history[^1] != line)
        {
            _history.Add(line);
            if (_history.Count > MaxHistory) _history.RemoveAt(0);
        }
    }

    private void HistoryPrev()
    {
        if (_history.Count == 0) return;
        if (_historyCursor == -1) _historyCursor = _history.Count;
        if (_historyCursor > 0) _historyCursor--;
        ReplaceBufferWith(_history[_historyCursor]);
    }

    private void HistoryNext()
    {
        if (_historyCursor == -1) return;
        _historyCursor++;
        if (_historyCursor >= _history.Count)
        {
            _historyCursor = -1;
            ReplaceBufferWith("");
        }
        else
        {
            ReplaceBufferWith(_history[_historyCursor]);
        }
    }

    private void ReplaceBufferWith(string newLine)
    {
        // Erase the current line back to prompt: \r, clear-to-end-of-line, reprint prompt + new line.
        Write("\r\x1b[K");
        WritePrompt();
        _buffer.Clear();
        _buffer.Append(newLine);
        Write(newLine);
    }

    private void RedrawBuffer()
    {
        if (_buffer.Length > 0) Write(_buffer.ToString());
    }

    private void WritePrompt() => Write(Prompt);

    private void Write(string text)
    {
        _terminal.Call("write", text);
    }
}
