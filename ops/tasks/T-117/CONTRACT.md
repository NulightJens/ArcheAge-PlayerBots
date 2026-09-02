# T-117 contract: per-runtime two-wave one-zone autonomy proof v10

Use only PB-000's exact committed T-117 thread/worktree binding and AAEmu 1.2
runtime lease. Require host `62e3eb1d87da01194802ac886cd500134facad28`,
installed module source/tree
`39f748fb3904584b50e1dabc0cfb0b3045793165` /
`7a9b2c3296bb5aee03c0016a4a7a72bb4c75073d`, compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
Game assembly SHA-256
`0b770b543b167f7ae57fcb8eb9ad18357e75a5241b0b170a9b1fa4ac05f564f6`,
client observer blob `0f8b5e2b3a811877648020b715e4737777d4947a`, and schema-v2 autonomy
observer blob `173a2de8f8844f16be463ab0779670f0fe264198`. Do not build or reinstall.

The fresh root is `D:\Codex-Labs\evidence\T-117\one-zone-autonomy-v10`.
Require it absent, create it once after the exact lease, and never overwrite,
rename, replace, reuse, or write into a predecessor root. Preserve original
deployed config bytes and require SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Change only the nine Director assignments: enabled; zone `221`; IDs
`[20001,20002,20003]`; population `2/3/3`; initial delay `180000` ms;
reconciliation `15000` ms; retry `30000` ms. Preserve search radius `60` and
restore exact original bytes after final shutdown.

Require the reference clean/detached, installed module clean, accepted 30-entry
host overlay, zero Game/Login/client processes, free required ports, and empty
live-log locations. Observe existing MySQL process identities only through OS
metadata; never connect, query, start, stop, or modify a database. The ArcheAge
client is forbidden. Create every evidence-local wrapper/parser/planner/runner
and audit with create-new semantics; designate versions by hash and retain all
superseded drafts explicitly.

## Pre-runtime parser, planner, and scheduling gates

Run the exact schema-v2 observer qualification and require 91 assertions with
no route, command, parameter, type, AST, or retained-fixture violation. Online
botdebug samples must contain exactly one invariant record:

`Runtime metrics: brain_steps=<nonnegative Int64>, mover_steps=<nonnegative Int64>, errors=<nonnegative Int64>`

The production parser exposes it as `runtime_metrics`; missing, duplicate, or
malformed records must fail closed with the T-114 diagnostics, and offline rows
must retain null runtime metrics. Runtime online rows require nonnegative parsed
brain/mover/errors and errors must remain zero. Keep aggregate `host_metrics`
separate; shared host brain/mover totals are never a per-bot fixed-counter gate.

Self-test the fixture parser against the exact immutable 359-byte T-110 response
SHA-256 `32474d410485ba1fe987a80a0ed3b41b86f3729ee4fe3b30263f593e4de844d3`.
Require object `44979`, anchor `20001`, zone/instance `221/0`, coordinates
`13616.7/13298.1/28.5`, grade `Weak`, and zero errors. Use grade grammar
`[A-Za-z][A-Za-z0-9_]*`; reject numeric-only, empty, localized, duplicate,
missing, wrong-anchor/zone/instance, zero-object, non-finite, and trailing-error
families. Runtime parsing must use the same exact grammar.

Repeat the T-112 transform and literal-row planner selftest without persisted
enumeration rows. Use the exact retained transform bits, distances
`{5,10,...,55}`, and ordered 24-angle grid from `0` through `-15`. Each of two
independent planners must enumerate `18,399,744` tuples, yield every `8192`,
find `125` eligible, select orders `[28,68,56]`, distances `[10,15,15]`, angles
`[60,-60,120]`, minimum pairwise `23.728951412717223 m`, minimum row margin
`3.820467349324412 m`, and semantic SHA-256
`38b871081b5c3dc4be700d33f9890c51e10aa3b1c80839b8b916584fa553bfc1`.
Run planners sequentially in hidden BelowNormal processes while a 500 ms
AboveNormal sentinel records exact `0..N` order and maximum adjacent gap at
most `1500 ms`. Any identity, count, choice, hash, priority, yield, or sentinel
failure is pre-runtime terminal.

## Runtime bootstrap, planning, and two lifecycle waves

Launch the exact client-process observer before Login, prove raw/ledger row zero
at zero clients, and retain the same PID through both runs and final cleanup.
Start loopback Login then Game using the accepted wrappers. Immediately after
API readiness, launch one exact schema-v2 autonomy observer per bot at
AboveNormal priority. Require first samples within 120 seconds, then two
raw-verified offline samples and armed/live boundaries for all identities before
admission. For each run require Director start, admissions `[20001,20002,20003]`
one per reconciliation tick, maximum three, default world/instance zone 221,
zero failures/wrong-zone/overlap, first admission in `[180000,195000] ms`, and
gaps consistent with the 15000 ms reconciliation interval.

Observers target 500 ms. Through each observer's explicit cooperative stop
boundary, validate every raw hash, transport timestamp, row chain, identity,
state, and parser result; require no malformed/transport-error row and no
adjacent gap above `2000 ms`. Never include samples after an observer's stopped
boundary and never keep an autonomy observer polling during Game shutdown.

For each of two run-one waves, wait for all bots targetless, noncombat,
stationary, idle, and full-resource. Seal two consecutive exact transform rows
per identity: common world `0`, zone `221`, instance and current object identity;
six latest rows span at most 750 ms and are no older than 1500 ms. Run both full
planners with the preflight policy. Require identical eligible count, semantic
hash, deterministic selection, predicted geometry, and zero persisted rows.
Immediately after planning require two fresh rows per identity with identical
object/state/resource/transform bits, the same span/age limits, and no observer
gap failure. Begin the three-command batch within 1500 ms and finish responses
within 3500 ms.

Issue exactly once in bot-ID order:
`spawnpassive 10004 <distance> <botId> <yawOffsetDegrees>`. Never retry or
substitute. Require three distinct nonzero objects, named-grade parse, expected
anchor/zone/instance, finite coordinates within 0.11 m of prediction, actual
pairs at least 15 m, and each bot row's assigned fixture at least 2 m nearer
than both foreign fixtures. Stop after any mismatch and issue no later command.

Each identity must independently select its own assigned object, record native
`grind/nearby_mortal`, navigate/cast/damage through production behavior, receive
exactly one native credit for that distinct object, gain positive authoritative
progression, complete ordered full-resource recovery, record one debt-free
completion, normally self-logout, and be refilled by Director. No directed
gameplay command is permitted. Across both waves require exactly six one-shot
commands, six unique objects, two credits and two lifecycle completions per bot.

The recovery fixed-counter gate is per runtime. From the first raw online row
whose life recovery state is `pending` through the last pending row, require at
least three consecutive samples spanning at least 1000 ms, constant
`runtime_metrics.brain_steps`, constant `runtime_metrics.mover_steps`, and
constant zero `runtime_metrics.errors`. Other bots and aggregate host counters
may advance and must not invalidate this per-bot gate. Require a later completed
recovery/full-resource row or exact normal logout transition derived from raw
evidence. Reject missing runtime metrics, a counter change within the pending
window, fewer/shorter samples, any error, or identity discontinuity.

## Cooperative observer stops, restart, dwell, and cleanup

After wave two and refill, capture a fresh successful final online row for each
bot after the final semantic boundary. While Game is still healthy, write one
fresh cooperative stop sentinel per autonomy observer. Each must finish its
current request, validate and chain its final successful row, write a stopped
boundary containing PID/bot/sample index/timestamps/terminal chain, and exit
without transport errors, force, or process replacement. Require stop request
to exit at most 2000 ms, final adjacent gap at most 2000 ms, and all three
observers absent before sending Game Ctrl+C. The absence of post-stop polling is
intentional and not an observation gap. Keep the client observer running.

Stop Game then Login gracefully and prove zero runtime state; move logs
losslessly into the root. Restart under the same config with distinct Login and
Game PIDs. Launch three fresh schema-v2 autonomy observers into fresh run-two
paths immediately after API readiness, pre-arm them before admission, reprove
the delayed ordered bootstrap, and retain at least two continuous minutes at
target with no fixtures or gameplay commands. Then perform the same final-row,
fresh-sentinel, <=2000 ms cooperative observer-stop protocol before gracefully
stopping Game then Login. No autonomy shutdown-tail transport rows are allowed.

Restore exact original config bytes. Prove zero bots, runtimes, relevant
processes, required listeners, autonomy observers, and live logs; unchanged
observed MySQL identities; exact host/reference/installed source; and once-only
cleanup. Only after server cleanup stop the continuous client observer through
a fresh cooperative sentinel; validate all client raw hashes and chain links,
maximum gap 2000 ms, zero client/error samples, and first-before-Login/
last-after-cleanup coverage. Never force-stop a process.

Seal a canonical relative-path/length/SHA-256 manifest with zero missing,
mismatched, duplicate, unsafe, unlisted, or reparse payloads. On any FAIL or
INCOMPLETE, still perform graceful cleanup, exact restoration, complete audit,
and immutable sealing; do not retry a partial wave or reuse the root. Commit
only the T-117 receipt and handoff. Report exact identities, parser/planner/
scheduling results, both delay measurements, selections and geometry, per-bot
runtime counter windows, lifecycle/refill proof, observer stop boundaries,
restart/dwell, client chain, cleanup, manifest, anomalies, and unproven scope.
PB-000 alone releases the lease and dispatches independent evidence integration.
T-037 remains blocked until a T-117 PASS is independently integrated.
