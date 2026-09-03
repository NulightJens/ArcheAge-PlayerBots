# ArcheAge 3.0 acceptance runbook

This runbook promotes the `3.0.4.2 r336598` track by evidence, not by configuration. Keep it isolated from a 1.2 server: use a separate checkout, new versioned Login/Game schemas, distinct ports, and a matching client directory.

## Current gate state

| Gate | State | Acceptance evidence |
|---|---|---|
| Asset identity | Passed | Client version, `game_pak`, both SQLite databases, SHA-256 provenance, SQLite integrity |
| Host adapter | Passed | Active alpha-v4 patch and zero-error builds on both pinned hosts |
| Server startup | Passed | Login/Game registration, module schema, unique ports, loopback status and `@system` metrics |
| Client login/serializer | Passed once | Native account and character creation, character select, and world entry completed without serializer or DLL error |
| One-bot lifecycle | Passed once | Rendered spawn/follow/combat, clean server restart, normal logout/re-add, zero overlap/error |
| Class and equipment | Passed once | `/setclass` restored three Darkrunner trees/23 skills; `/kit` auto-equipped a grade-5 weapon and logout/re-add retained it |
| Four-role behavior | Open | Darkrunner, Primeval, caster, and Cleric perform legal movement/casts/recovery using 3.0 templates |
| Scale and recovery | Open | 0/10/50/100 cohorts, approved 3.0 budget, normal logout, 60-second recovery, clean shutdown |

Server-start validation is deliberately below runtime support.

Alpha.6 quest intake, completion, destination indexing, and transfer-road routing remain disabled on 3.0 until equivalent host APIs are available.

## Isolated startup

1. Clone PlayerBots at `modules/archeage-playerbots` in a descendant of AAEmu base `8c1c943bb2309eefffb9da2aa99a408d0acbb095`.
2. Run the installer with `-Track AAEmu30 -AllowExperimental`.
3. Create new versioned Login and Game schemas. Do not point this track at a 1.2 or live database.
4. Configure Login, Game, Stream, and Web API ports that do not overlap an active server. Bind the Web API to loopback.
5. Stage the matching databases under `AAEmu.Game/Data`, configure the matching `game_pak`, and run `Test-AAEmu30Assets.ps1`.
6. Start Login, then Game. Require successful Game registration and ready listeners before launching the client.
7. Run `Test-AAEmu30Runtime.ps1` with the actual loopback Web API port and expected runtime count zero.

An accepted startup must retain the asset result, build/test result, port ownership, startup duration, warnings/errors, process memory, metrics document, and graceful shutdown line.

## Client and bot gates

Use one matching client and one new GM character first. A login pass requires the launcher to target this track's Login client port; reaching an unrelated 1.2 listener is test contamination.

For the one-bot gate:

1. Create or prepare one persistent character through the client.
2. Capture `/botmetrics reset`, then `/addbot <id>` and `/botstate <id> grind`.
3. Verify world presence, movement, legal cast ranges, persistence, and normal `/removebot <id>` logout.
4. Restart cleanly and repeat. Require zero `tickErrors`, `runtimeOverlaps`, `spawnFailures`, and `despawnFailures`.

For the four-role gate, explicitly observe skills whose 3.0 target semantics differ from 1.2: Dissonance `11943`, Health Lift `11991`, Shadow Step `12075`, Aranzeb's Boon `16004`, and Infuse `16783`. The role anchors remain Triple Slash `18131` at 4 m, Endless Arrows `14835` at 20 m, Flamebolt `10752` at 20 m, and Antithesis `10534` at 25 m. Runtime skill templates—not copied 1.2 constants—remain authoritative.

## Resource and promotion rule

The retained empty 3.0 Game process used roughly 5.9 GB private memory, materially above the 1.2 baseline. Establish a separate whole-server reserve before scale testing. Do not inherit the 1.2 capacity result.

Promote 3.0 from `server-start-validated` only after every open gate passes twice from a clean start and populated graceful shutdown reports zero remaining bots and runtimes. Record failures as failures; a corrected rerun does not erase the earlier evidence.
