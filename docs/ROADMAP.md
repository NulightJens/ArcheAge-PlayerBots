# Roadmap

The goal is believable, useful, lightweight party members and world population. ArcheAge-native systems remain the baseline unless a measured gap requires more logic.

## 0.1 release candidate: foundation

Status: released as `0.1.0-rc.2`; accepted fixes continue under Unreleased.

- Connectionless persistent-character lifecycle.
- Data-driven archetypes and rotations.
- Native-party authorization and role controls.
- Melee, archer, caster, and healer range anchoring.
- Death, invalid-target, kill-credit, and follow recovery.
- Human GM commands and runtime diagnostics.
- Exact population metrics and isolated scale harness.
- Physical execution at 100, 500, and 1,000 bots.

## 0.2: believable combat

- Completed: explicit attack, follow, state, and party work wakes brains shed by inactive-duel cleanup without keeping Idle brains active. A four-role physical regression passed with all roles casting, zero tick errors, and zero runtime overlaps.
- Completed: stationary and close-range Primeval checkpoints. Endless Arrows supplied 33 of 45 hostile-target casts in the four-role sample and 11 of 15 damaging shots after a close escape. The close trial used Snare, Backdrop, and one tangent-dominant destination to settle at 19.182 m; moving-hostile pursuit remains open.
- Completed: a sampled 16.985 m Darkrunner approach held short-range filler and Triple Slash, allowed only legal gap closers during approach, and did not execute Whirlwind Slash until 3.936 m. Boundary regressions reject Whirlwind Slash beyond 5 m and Sunder Earth beyond 9 m.
- Completed: Daggerspell approached from 26.507 m, held an 18.970 m Fireball anchor, and used Fireball for 8 of 11 damaging casts (72.7%). A data-only 4-second shared throttle prevents asynchronous plot-cooldown burst rows from starving the default Fireball lane; the physical pass had no cast failures, tick errors, runtime overlaps, or path requests.
- Completed: with native follow and attack orders both active, Cleric selected a 76.24%-health party member at 35.061 m, retained the human owner as its follow target, moved to a 24.047 m Antithesis standoff, healed the recipient to 100%, and resumed owner-relative movement. The instrumented pass had seven successful casts, zero failures, zero tick errors, zero runtime overlaps, and zero path requests.
- Completed: Primeval's moving-hostile gate. A level-50 melee fixture moved 18.13 m while Primeval reported movement in 10 of 24 retained samples; Endless Arrows starts overlapped the moving segment. The companion 30-second sample produced 16 Endless Arrows in 22 offensive casts (72.73%). The isolated repeat had 12/12 successful casts, a 0.7 ms bot-host p95, zero scans, zero path requests, zero skipped ticks, zero overlaps, and zero tick errors.
- Completed: the continuously moving-owner Cleric gate. The leader moved 34.66 m over 10.038 seconds while the Cleric preserved the follow target in all 37 samples, moved 19.29 m toward a 53.23%-health party member, completed Antithesis before the movement window ended, crossed the 85% support threshold, and resumed leader-directed movement. The four-runtime sample had one successful cast, a 1.2 ms bot-host p95, and no cast failure, path request, skipped tick, runtime overlap, or tick error.
- Instrument stealth search, loss, release, and reacquisition.
- Run mortal-combat cohorts at 1, 5, 10, 25, 50, and 100 bots with matched Idle controls.

Mortal fixtures must be staged away from the human observer and unrelated respawning hostiles. The moving-hostile cleanup exposed this harness requirement when the observer and one melee bot died outside the isolated Primeval measurement; both recovered, but that cleanup is not an accepted death-free cohort.

## 0.3: navigation and sustainability

- Establish an ArcheAge-compatible navigation boundary instead of direct coordinate traversal.
- Add obstacle diagnostics before claiming wall or terrain behavior.
- Measure natural mana exhaustion, rest, recovery, and re-entry without administrator healing.
- Revisit jump only when packets produce correct animation and geometry interaction.

## 0.4: population operations

- Approve a whole-server reserve and latency budget from an empty-server baseline.
- Run the highest budget-qualified cohort for 30 minutes plus normal-logout recovery.
- Introduce a one-zone Population Director with explicit activity and density limits.
- Expand only after zone-level resource and player-experience acceptance.

## ArcheAge 3.0

Blocked on provenance-recorded 3.0.4.2 assets, serializer compatibility, login smoke testing, and a one-bot lifecycle proof. The 1.2 module does not claim 3.0 support through configuration alone.

## Shelved experiments

- Pairwise collision repulsion moved the target and caused excessive bumping.
- Melee orbit/ring behavior covered only a narrow boss arc, left bots disengaged, and added continuous cost.
- Bot jump motion rendered as vertical sliding rather than a normal player jump.

These are documented findings, not enabled dependencies.
