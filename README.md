# Hacker Game

> An educational game that teaches **real PowerShell** to beginners through a gamified, story-driven RPG built in [Godot](https://godotengine.org/).

Players progress through quests that drill PowerShell cmdlets — `Get-ChildItem`, `Select-String`, `Get-Process`, `Invoke-WebRequest`, `Stop-Process`, and so on — by typing them into an in-game terminal that runs an **embedded, sandboxed PowerShell 7 runspace**. The Matrix-green hacker aesthetic is the *wrapper*; the *content* is a structured PowerShell curriculum organized around the language's verb-noun grammar (`Get-*` recon, `Set-*` authoring, `Invoke-*` execution, `Remove-*` cleanup).

Think of it as **bashcrawl meets Codecademy meets Hacknet**, for PowerShell, with a real shell underneath.

---

## Why this exists

There are excellent ways to learn Bash through play ([bashcrawl](https://gitlab.com/slackermedia/bashcrawl), [OverTheWire](https://overthewire.org/wargames/)) and many fictional "hacker" games with fake shells ([Hacknet](https://store.steampowered.com/app/365450/Hacknet/), [Uplink](https://www.introversion.co.uk/uplink/)), but **no game teaches real PowerShell**. PowerShell is the most powerful shell most Windows developers and sysadmins will ever use, and its verb-noun structure maps unusually well onto an RPG skill tree.

This project fills that gap: every command the player types is **real PowerShell**, evaluated by the real PowerShell engine, returning real `PSObject` pipelines and real errors that the player can learn from.

---

## Status

**Phase 5 in development.** Four levels shipped and playable end-to-end.

| Tag | Level | Theme | Verb family | Quests | Boss |
|---|---|---|---|---|---|
| `v0.1.0-mvp` | 1 — Recon | First shell, filesystem, help system | `Get-*` | 4 | First Breach |
| `v0.2.0-level2` | 2 — Reading Minds | Registry traversal (mocked) | `Get-*` / `Test-*` | 4 | Registry Whisper |
| `v0.3.0-level3` | 3 — Process Control | Processes & services (mocked) | `Get-*` / `Stop-*` | 4 | Silence the Defender |
| `v0.4.0-level4` | 4 — Reach Out | Networking (mocked endpoints) | `Invoke-*` / `Test-*` | 4 | Port Cartography |
| _in progress_ | 5 — Authoring | Writing state, scheduled tasks (mocked) | `Set-*` / `New-*` | 4 | _(TBD)_ |

**Totals so far:** 16 quests · 4 bosses · ~26 unlockable cmdlets · 3 mock-module biomes · full save/load · cross-platform (macOS / Windows / Linux).

Long-term plan: 10 levels covering the full verb-family curriculum, ending in a capstone scenario that integrates everything.

---

## How the sandbox works (and why this is safe)

This is **a teaching tool, not a hacking tool**. Several design choices enforce that:

### Real PowerShell, constrained runspace

Each quest spins up an embedded PowerShell 7 runspace via `Microsoft.PowerShell.SDK` with an **explicit allow-list of cmdlets** — built from `InitialSessionState.CreateDefault2()` with only the cmdlets the player has unlocked through the curriculum. A Level 1 player literally cannot invoke `Stop-Process` because that cmdlet does not exist in their runspace. No parser tricks, no string matching — the runtime enforces the boundary.

### Mock modules for anything sensitive

Quests that touch processes, services, the registry, the network, or scheduled tasks **do not touch the real OS**. They load purpose-built mock modules (`MockProcesses.psm1`, `MockServices.psm1`, `MockNetwork.psm1`, etc.) that shadow the real cmdlets via the runspace's `PSModulePath`. The fake "Sentinel.EDR" process the player kills in Level 3 lives in an in-memory hashtable; the real OS process table is untouched. The fake HTTP endpoints in Level 4 return canned responses from a Hashtable; no real network call is ever made.

This also makes the game **fully cross-platform** — no Windows-only registry, no real network state, no real process table — and **reproducible**, so quest verification is deterministic.

### Per-quest filesystem sandbox

The filesystem cmdlets (`Get-ChildItem`, `Set-Content`, `Remove-Item`, etc.) operate against a **per-quest temp directory** pre-seeded with fixture files declared in the quest resource. The runspace's location is set to that directory on load and the directory is deleted on quest exit. The player cannot navigate to or modify the real filesystem.

### Verification via Pester

Quest objectives are verified by running [Pester](https://pester.dev/) tests against the runspace state — the [PSKoans](https://github.com/vexx32/PSKoans) pattern. This means **multiple valid solutions** always pass; the player isn't guessing the author's exact one-liner.

### Errors teach, never brick

`ErrorActionPreference = Continue` in every runspace — invalid commands print red text in the terminal and the player tries again. Quests are retryable. There is no permadeath, no save corruption.

---

## What "hacker fantasy" means here

The narrative wrapper uses spy/heist/cyberpunk vocabulary because that's what makes typing `Stop-Process` feel exciting instead of like a sysadmin task. The targets in the game are **fictional adversaries** (the "Obsidian" syndicate, etc.) hosted on **fictional infrastructure** with **mocked everything**. There is no content about real targets, no offensive tooling, no actual exploitation techniques, no "how to" for any real-world attack. The skill being taught — and the only thing that transfers out of the game — is **PowerShell syntax and idiom**, the same skills taught by Microsoft Learn, [PSKoans](https://github.com/vexx32/PSKoans), and every PowerShell book on the market.

---

## Stack

- **Engine:** Godot 4.6.3 (Mono / .NET edition)
- **Language:** C# on .NET 10
- **Shell:** PowerShell 7 embedded in-process via [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK/)
- **Verification:** [Pester](https://pester.dev/) tests evaluated inside the same runspace
- **Quest data:** Godot custom Resources (`.tres`) — editor-authored, type-safe
- **Save data:** JSON in `user://save.json`
- **Targets:** macOS, Windows, Linux

---

## Running

Prereqs (macOS via Homebrew):

```sh
brew install dotnet powershell
brew install --cask godot-mono
```

Open in the editor:

```sh
godot-mono --editor --path .
```

Headless walkthrough test (verifies all shipped levels end-to-end):

```sh
godot-mono --headless --path . --script tools/walkthrough_test.cs
```

---

## Repository layout

```
hacker-game/
├── scenes/              # Godot scenes (Main, Terminal, HUD, BossIntro)
├── scripts/
│   ├── autoload/        # PowerShellRunner, QuestManager, GameState
│   ├── ui/              # Terminal, HUD, MatrixRain
│   └── resources/       # QuestResource, ObjectiveResource, etc.
├── shaders/             # matrix_rain, crt, glitch
├── content/levels/      # .tres quest definitions, one folder per level
├── mock-modules/        # Cross-platform PowerShell modules that fake OS state
└── assets/              # Fonts, SFX, music
```

---

## Roadmap

- Level 5 — Authoring (`Set-*` / `New-*`, scheduled-task mocks) — _in progress_
- Level 6 — Scripting (`if` / `foreach` / functions / params)
- Level 7 — Remote ops (`Invoke-Command`, mocked PSSessions)
- Level 8 — Active Directory recon (mocked AD provider)
- Level 9 — Counter-forensics (`Remove-*` across biomes)
- Level 10 — Capstone (full integration, leaderboard-scored)
- World map hub (Hacknet-style clickable targets)
- Arsenal sidebar (unlocked cmdlets with live `Get-Help`)
- Audio pass (synthwave loop, key-click SFX, boss intro music)

---

## License

TBD. The game ships its own mock modules and content; PowerShell 7 is redistributed under its [MIT license](https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt).

---

## Author

Built by [@rogeriosantos](https://github.com/rogeriosantos) as a personal educational project, in parallel with day work on industrial software.