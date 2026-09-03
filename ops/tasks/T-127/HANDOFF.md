# T-127 handoff

Outcome: `SOURCE_GREEN` with a retained client-witnessed live pass on the prior
candidate. Fresh level-one Nuian `Freshnav14` autonomously completed seven
native starter quests—330, 250, 6198, 2531, 251, 324, and 325—after one autonomy
start and no directed gameplay commands. Quest 250 reached native fox progress
3/3 and auto-completed; quest 2531 supplied the main-story completion.

## Source and release identity

- Source: `10b014f8bc26bd20abdc896ec9d49661365cd1b1`
- Tree: `a2362d4fbff313b76ae785d647d6c772f7c8bf3f`
- Release candidate: `0.2.0-alpha.6`
- Host base: `62e3eb1d87da01194802ac886cd500134facad28`
- Full 1.2 patch: `compatibility/aaemu-1.2-r208022-v4.patch`
- Patch SHA-256: `8e33ed82dcbfc6e9e7aaec46624610b542ea3a113ee834d2450fb2f818b56e14`

## Proof

The retained run exceeded the five-quest target with an active human client
witness. Native logs contain each completion and the signpost auto-completion;
the static decision board retains quest, combat, navigation, and life state.
The bot logged out normally after capture. Observed pre-logout metrics were six
credited kills, zero tick errors, zero runtime overlaps, two recovery nudges,
zero recovery teleports, and one runtime.

The fresh alpha.6 release gate applied v4 to the pinned host, passed installer
validation, built with zero errors, passed 2,000 tests plus four intentional
legacy skips, compiled command scripts with zero errors/warnings, and reported
zero vulnerable packages after the documented optional host baseline.

## Retained failure and correction

The live candidate exposed two bounded detours: intake could briefly reclaim
movement after a lifecycle handoff, and an unstick nudge could replace the
logical report route. The release source holds lifecycle priority across that
handoff, preserves the route during recovery steering, and observes native
auto-completion outside report wait. The release gate also converted the old
single-bot one-kill lifecycle into an explicit test-only opt-in; resident bots
remain persistent by default.

## Runtime and unproven boundary

The retained server remains running without bot 14; no client or runtime was
controlled during release work. The live candidate predates the final source,
so G-001 remains `source-green`, not accepted. Exact-source deployment, a fresh
five-quest repeat, and distinct-PID persistence remain required.

Evidence receipt: `ops/evidence/aaemu12-g001-nuian-five-quest-v1.yaml`.

Exact next action: at the next agreed runtime window, gracefully stop Game then
Login, deploy source `10b014f` with v4, repeat the fresh Nuian gate under an
active client, then verify normal logout and distinct-PID persistence.
