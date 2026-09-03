# T-067 handoff

T-067 is complete as a bounded source-only correction. The existing lifecycle
controller now preserves one immutable activation baseline and one immutable
pre-logout completion snapshot for progression, health, mana, and loaded bag
contents. It emits the exact signed deltas before the normal logout callback
and exposes the same values through `/botdebug` while the runtime remains
present.

## Source identity and changed files

- This task started from dispatch-envelope commit
  `301d5ec1001d762b32fbca132eaee06aa8dd44b3`. The Control Tower confirmed that
  its declared integrated source base is
  `07e230ab959c4147318d3b5228dee492ff18c4fb` and that the intervening commit is
  control metadata only.
- The AAEmu 1.2 reference remained clean at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Validation used `compatibility/aaemu-1.2-r208022-v3.patch`, SHA-256
  `0285C8C21133EC00ABBD8F5DE925C56DC2D87F7D8595FEE89A6063B3FF02CA3E`.
- The implementation commit is the exact T-067 task head reported with this
  handoff.
- Changed files are `src/AAEmu.Game/Bots/Life/BotLifeController.cs`,
  `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`,
  `tests/AAEmu.UnitTests/Bots/Life/BotLifeControllerTests.cs`,
  `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`,
  `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`, and this
  handoff.

## Behavior and proof

An accepted `ActivityRequested` transition captures UTC time, level, exact
character experience, HP/max HP, MP/max MP, occupied bag slots, total bag item
units, a stable slot/template/count summary, and its lowercase SHA-256
fingerprint. Immediately before an accepted `LogoutRequested` transition, the
controller captures the same fields, calculates signed numeric deltas, and
writes one `ev=life_progression` key/value record before the existing callback
path can run. Bag entries are invariantly ordered by slot, template, and count.
Null inventory, null bag/items, invalid or duplicate slots, null item data,
invalid template/count data, concurrent observation failure, and individual
resource getter failure produce explicit `unavailable` fields without throwing
from the host tick or changing lifecycle behavior.

The snapshots and delta are immutable across duplicate ticks and callback
failure. Re-registering a persistent identity clears all three and permits a
fresh cycle. `/botdebug` preserves its existing movement, combat, lifecycle,
and metrics output, adds baseline/completion/delta lines, and reports explicit
`pending` completion/delta state before logout acceptance.

Validation used the retained isolated source/build copy at
`C:\Users\jensh\.codex\build-evidence\T-067-v1\host`; no registered or deployed
host was edited. `Install-PlayerBots.ps1 -Track AAEmu12 -CheckOnly` reported the
supported track installed. `dotnet build AAEmu.slnx --no-incremental` completed
with 0 errors and 77 retained package/analyzer warnings. The focused AAEmu 1.2
selection passed 169, failed 0, skipped 0 across the controller, lifecycle
state/profile, host behavior, kill credit, combat task/state, bot manager, and
command suites. The 11 exact contract-critical IDs also passed independently.
The full AAEmu 1.2 suite was not run because repository policy reserves it for
the integration wave.

No final failure remains. The first exact snapshot test exposed only that the
bare host `CharacterMock` cannot calculate max resources without formula data;
the test now uses a deterministic max-resource mock and the superseding build
and all focused runs are green.

## Runtime state and unproven boundaries

No AAEmu Game/Login process, MySQL service, client, listener, runtime lease,
deployed integration host, database, retained evidence root, or AAEmu 3.0
checkout was started, controlled, or changed. Runtime proof remains explicitly
pending.

Native post-kill XP/item/resource deltas, recovery state, successful persisted
self-logout, restart persistence, and a fresh immutable one-bot runtime receipt
remain unproven until integration and a newly versioned physical task. T-065
remains `INCOMPLETE`; this source task does not reuse or supersede its evidence
root.

## Exact integration action

Integrator: cherry-pick the exact T-067 task head reported by the worker onto
`integration/aaemu12-world`, install/build it against the approved AAEmu 1.2 v3
compatibility seam, and run the full AAEmu 1.2 suite once for this integration
wave. After that passes, dispatch a fresh immutable runtime-proof version to
capture activation and pre-logout records for exact native progression,
inventory, HP, and MP deltas plus recovery, persisted self-logout, and clean
restart. Do not reuse the T-065 evidence root or change unrelated task
dispositions during this integration.
