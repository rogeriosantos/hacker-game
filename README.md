# Hacker Game (codename)

A gamified PowerShell trainer wrapped in a Matrix-green hacker-vs-the-world RPG fantasy. Built with Godot 4.6 .NET + C#, with real PowerShell 7 embedded in-process via `Microsoft.PowerShell.SDK`.

**Status:** scaffolding (Phase E1).

## Stack

- Godot 4.6.3 (Mono / .NET edition)
- .NET 10 SDK, C#
- `Microsoft.PowerShell.SDK` (in-process PowerShell 7 runspace)
- Pester (quest verification)
- Targets macOS, Windows, Linux

## Plan

Full plan: `/Users/roger/.claude/plans/now-inside-this-folder-unified-cocoa.md`

## Running

Prereqs (macOS / Homebrew):

```sh
brew install dotnet powershell
brew install --cask godot-mono
```

Open in the editor:

```sh
godot-mono --editor --path .
```

Headless smoke (after Phase E1 spike lands):

```sh
godot-mono --headless --path . --import
```
