# ArcheAge PlayerBots Changelog

Player-facing patch notes come first; developer packaging detail follows.

## Unreleased

### Lifecycle and control

- Fixed an inactive-duel lifecycle hole that could leave a bot in Combat state with no scheduled brain, presenting physically as only half of a party fighting.
- Explicit attack, follow, non-idle state, free-active, and authorized native-party commands now reattach a shed brain on demand while preserving the low-cost detached Idle path.
- Added regressions for direct command wake-up, native-party command wake-up, and runtime reuse after reactivation.
- Physically reproduced two detached brains, then verified all four roles reactivated and cast against one passive target: 150/150 successful cast attempts, zero tick errors, zero runtime overlaps, and a 1.557 ms maximum bot-host tick.
- In that physical sample, Endless Arrows was 33 of 45 Primeval hostile-target casts (73.3%).
- Passed a 16.985 m Darkrunner approach sample: short-range filler and Triple Slash were held, while Tiger Strike, Overwhelm, Shadowsmite, Charge, and Whirlwind Slash succeeded only inside their legal ranges. Automated effect-radius boundaries retain 5.00/5.01 m coverage for Whirlwind Slash and 9.00/9.01 m for Sunder Earth.
- Passed a 3.042 m Primeval close-target sample: Snare and Backdrop fired legally, a single tangent-dominant escape destination produced 17.445 m of lateral travel versus 4.573 m outward, and the bot settled at 19.182 m. Endless Arrows then supplied 11 of 15 damaging shots (73.3%) with zero cast failures, tick errors, runtime overlaps, or path requests.

### Next combat pass

- Validate Primeval's accepted range and lateral-escape behavior against a moving hostile target.
- Let Cleric reposition toward injured party members while combat and follow goals compete.
- Add observable stealth loss, search, release, and reacquisition states.

## 0.1.0-rc.2 - 2026-08-29

### Release automation

- Made the hosted Windows test step run from the AAEmu root so .NET discovers AAEmu's Microsoft Testing Platform configuration.
- Replaced hard-coded Unix test-artifact paths with the operating system's temporary directory.
- Added the command-script compiler and vulnerable-package audit to the hosted release gate.
- No PlayerBots runtime behavior changed from `0.1.0-rc.1`.

## 0.1.0-rc.1 - 2026-08-29

### Standalone release

- Extracted PlayerBots into its own repository and history; AAEmu is now an explicit host dependency rather than bundled project content.
- Added PowerShell and Bash installers with compatibility, dirty-tree, patch, and migration conflict checks.
- Added conditional MSBuild imports so module source and tests remain physically owned by this repository while compiling with AAEmu.
- Added a machine-readable compatibility manifest and CI recipe for a clean AAEmu install/build/test.
- Preserved the former combined repository as integration history; it is not the module distribution format.

### Patch highlights

- Added connectionless PlayerBots backed by real, persistent AAEmu characters.
- Added Darkrunner, Primeval, Daggerspell, Cleric, Abolisher, Reaper, and Templar profiles with data-driven rotations.
- Added native-party control with follow, stay, attack, passive, tank, healer, and attacker roles.
- Added `/bot`, `/bots`, and `/bothelp` plus human GM commands for lifecycle, movement, combat, diagnostics, and resource measurement.

### Combat and movement

- Anchored melee, archer, caster, and healer behavior around archetype-appropriate ranges.
- Made Endless Arrows the Primeval's dominant filler; a retained controlled sample produced 16 of 21 offensive casts (76.19%).
- Removed legacy autoattack fallback from tested data rotations so ranged characters do not enter melee merely to autoattack.
- Added healer-recipient selection and movement toward Antithesis range.
- Added target-death recovery, invalid-target release, kill goals, authoritative kill credit, and return-to-follow behavior.
- Added stable low-cost variation to native-party follow positions without per-tick random sampling.
- Retained ArcheAge's native collision and direct-pursuit behavior. Pairwise repulsion and boss-orbit movement are not enabled.

### Operations and verification

- Added action history, blackboard, strategy, rotation, archetype, state, and live metrics commands.
- Added an isolated 0/10/50/100 measurement harness and graceful-shutdown evidence contracts.
- Added activity governing, scan TTL caches, timing histograms, process resource sampling, and recovery metrics.
- Physically exercised 100, 500, and 1,000 simultaneous bots with normal logout cleanup.
- Verified native-party relog/rebind, leader logout/relogin, kick/rejoin authorization, moving-owner combat priority, Cleric healing after relog, and post-combat regrouping.

### Known limitations

- Supported target is ArcheAge 1.2 `r208022`; 3.0 is a separate compatibility track.
- Direct movement is not navmesh navigation.
- Jump packets and vertical motion exist, but the 1.2 client does not display a convincing bot jump.
- No maximum supported population is claimed until a whole-server budget is approved.
