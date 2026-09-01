# T-067 contract

## Outcome

The existing bounded lifecycle controller records one deterministic read-only
progression/resource baseline when `ActivityRequested` is accepted and one
completion snapshot immediately before an accepted `LogoutRequested` event.
The exact before/after values and deltas remain visible in structured logs
after self-logout and in `/botdebug` while the runtime is present.

## Required behavior

- Extend the existing `BotLifeController` view/model; do not introduce a
  parallel lifecycle, persistence store, database query, or gameplay action.
- At accepted autonomous activation, capture level, exact character experience,
  HP/max HP, MP/max MP, occupied bag slots, total bag item units, and a stable
  deterministic inventory summary/fingerprint from the already loaded bot.
- Immediately before the controller submits an accepted logout transition,
  capture the same fields and calculate exact signed deltas. Capturing must be
  read-only and must not delay, reject, or otherwise change lifecycle behavior.
- Emit one structured key/value progression/resource record containing bot ID,
  activity/reason, both timestamps, before/after values, signed deltas, and the
  two inventory summaries. It must be written before the normal logout callback
  so retained runtime logs remain authoritative after the bot is gone.
- Extend `/botdebug` with the same baseline/completion/delta values. Before
  completion, show an explicit pending state rather than inventing an after
  value. Keep existing lifecycle, combat, movement, and metrics output intact.
- Fail closed for null inventory or partially unavailable item data with
  explicit `unavailable` fields; never throw from the host tick because an
  observation cannot be captured. Use invariant, deterministic ordering and
  formatting and do not expose credentials or unrelated item metadata.
- Duplicate ticks and logout callback failure may not duplicate or mutate the
  captured baseline/completion. Re-registering a persistent identity must reset
  both snapshots and permit a fresh observation cycle.
- Add focused deterministic tests for exact snapshot/delta math, stable
  inventory ordering/fingerprint, missing inventory behavior, pending output,
  pre-logout capture ordering, duplicate suppression, callback failure, and
  clean re-add state. Extend `/botdebug` tests for operator-visible values.
- Build/run directly affected AAEmu 1.2 tests in an isolated approved boundary,
  commit only declared scope, and state the exact integration action.

## Non-goals

Awarding XP; generating or looting items; changing inventory; healing or
regenerating HP/MP; delaying logout; retaining a new offline in-memory cache;
database access; runtime proof; activity choice; navigation/combat changes;
Activity Director; scale, soak, release packaging, or AAEmu 3.0.
