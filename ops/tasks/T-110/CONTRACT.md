# T-110 contract: row-valid angular two-wave one-zone autonomy proof v8

Use only the exact committed T-110 binding/lease and registered AAEmu 1.2
workspaces. Require host `62e3eb1d87da01194802ac886cd500134facad28`,
installed module source/tree
`037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
`a1b9302625a65e10dfa9b7e11393a67134f914e8`, compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
Game assembly SHA-256
`2385d16554b1fc9df1c77d45612b39c8fdac20721f20eac31ec9d0c32cc71696`,
client observer blob `0f8b5e2b3a811877648020b715e4737777d4947a`, and autonomy observer blob
`94b82fb18a39e2356d43796b54e3eeea5ad7ee12`.

The fresh root is
`D:\Codex-Labs\evidence\T-110\one-zone-autonomy-v8`. Require it absent,
create it once, and never overwrite, rename, replace, or reuse it. Every
predecessor root, including T-108 v7, is immutable. Preserve original deployed
config bytes and SHA-256. Require continuity SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`,
then change only the nine Director assignments: enabled; zone `221`; IDs
`[20001,20002,20003]`; population `2/3/3`; initial delay `180000` ms;
reconciliation `15000` ms; retry `30000` ms. Preserve configured search radius
`60`. Restore the exact original bytes after final shutdown.

Require the reference clean/detached, installed module clean, exact accepted
30-entry host overlay, zero Game/Login/client processes, free ports
`1234/1237/1239/1250/1280`, and empty live-log locations. Observe current
MySQL process identities through OS metadata only; never connect, query, start,
stop, or modify a database, and require those identities unchanged at cleanup.

Create every evidence-local parser, planner, observer wrapper, runner, and audit
script with create-new semantics before runtime. Hash and retain them. Self-test
the transform parser against the exact invariant grammar and negative
duplicate/missing, localized, non-finite, wrong-world, wrong-instance, and
wrong-zone cases:

`Transform: world=<uint>, instance=<uint>, zone=<uint>, x=<float:R>, y=<float:R>, z=<float:R>, yaw_rad=<float:R>`

## Mandatory row-oriented planner self-test

Self-test the planner against the exact T-104 retained transform bit patterns:

- bot 20001: X `1179957453`, Y `1179643699`, yaw `-1065611208`;
- bot 20002: X `1179966362`, Y `1179652403`, yaw `-1065603658`;
- bot 20003: X `1179971072`, Y `1179656704`, yaw `-1065577192`.

Enumerate distances `{5,10,...,55}` and the exact ordered 24-angle set
`{0,15,30,45,60,75,90,105,120,135,150,165,180,-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15}`
for each bot: `264^3 = 18,399,744` tuples. Mirror the installed command with
single-precision operations exactly. Offset `0` skips rotation; every nonzero
offset computes `MathF.PI / 180f * yawOffsetDegrees`, adds it to source yaw on
the detached clone, then calls `AddDistanceToFront(distance)`. Placement is:

`fixtureX = x + (-distance * MathF.Sin(yaw_rad + offset_rad))`

`fixtureY = y + ( distance * MathF.Cos(yaw_rad + offset_rad))`

For matrix `anchorToFixture[botIndex][fixtureIndex]`, the ownership criterion is
literal and exclusively row-oriented. For every bot row, its own diagonal
fixture must be at least `3.0 m` nearer than both foreign fixture columns. Never
use or accept the T-108 diagnostic column-oriented comparison.

Require every self-test value finite, all own fixtures within `60 m`, every
fixture pair at least `20.0 m` apart, and the literal row criterion above.
Require exactly 125 eligible tuples. Apply the deterministic ordering described
below and require selected choice orders `[28,68,56]`, distances `[10,15,15]`,
angles `[60,-60,120]`, fixture XY float bits
`[1179959989/1179633778,1179951580/1179656575,1179985828/1179652438]`,
minimum pairwise distance `23.728951412717223 m`, and minimum row margin
`3.820467349324412 m`. Independently repeat the complete computation and
require identical count, selected tuple, compact semantic hash, and summary.
Any mismatch is a pre-runtime blocker.

Do not persist all 18,399,744 rows. Define and retain a canonical versioned
binary semantic-hash stream that commits the exact input bits, ordered grids,
every tuple's choice orders, fixture float bits, pairwise double bits, 3x3
matrix double bits, minimum pairwise/margin double bits, and eligibility bit.
Retain its schema/source/hash plus compact counts, extrema, and selected record.
The repeat implementation must independently recompute the same semantic hash
without reading a precomputed row file.

## Runtime observation and Director bootstrap

Launch the exact Windows PowerShell client observer before Login with 500 ms
samples. Independently validate raw/ledger row zero at zero clients; keep the
same PID alive through both runs and final cleanup; never relaunch. Start
loopback Login then Game through the accepted wrapper. Immediately after API
readiness, launch one exact autonomy observer per bot. Require first samples
within 120 seconds of runtime-start evidence and two raw-verified offline
samples plus armed/live boundaries for every bot before admission.

For each run, require authoritative Director start and ordered admissions
`[20001,20002,20003]`, one per reconciliation tick, zone/default world/default
instance, maximum three, and zero failures, wrong-zone activations, or overlap.
Derive first-admission elapsed time from authoritative timestamps and require
the deployed delay in `[180000,195000]` ms on both runs. Stop without retry if
outside the window.

## Live fixture planning and one-shot ownership proof

At a fresh all-three targetless, noncombat, stationary, idle, full-resource
boundary in each of the two run-one waves, retain two consecutive raw samples
per identity whose exact transform values are stable. Require the six latest
samples to span at most 750 ms and be no older than 1500 ms. Each must contain
exactly one finite invariant transform with world `0`, zone `221`, a common
default instance, and the same online object identity used by its observer.

Before any fixture mutation, enumerate all 18,399,744 distance/angle tuples
from those exact live float bits using the mandatory operations, ordered grids,
canonical semantic-hash stream, and literal row criterion. Eligibility requires
every value finite, every fixture pair at least `20.0 m` apart, each own fixture
within `60 m`, and for each bot row its own fixture at least `3.0 m` nearer than
either foreign fixture. Require at least one eligible tuple.

Choose exactly one tuple deterministically: maximize minimum pairwise distance;
then maximize minimum row-oriented own-versus-foreign margin; then minimize
maximum distance, distance sum, and sum of absolute yaw offsets; finally choose
the lexicographically earliest three choice-order integers. Evaluate every
tuple and seal the immutable input bits, count, semantic hash, extrema, and
selected record in memory before mutation. A second independent complete
recomputation from the sealed input bits must match count, hash, and selection
before the wave can pass. If none is eligible or either computation disagrees,
stop without issuing a fixture command. Do not persist the full row stream.

Begin the command batch within 2000 ms of the last qualifying raw and complete
all three local responses within 3500 ms. Issue exactly once in bot-ID order:
`spawnpassive 10004 <distance> <botId> <yawOffsetDegrees>`. Use invariant
integer spellings from the grid. Preserve exact request/response bytes. Never
retry, substitute, or issue a later command after an error. Require each
response to report its expected anchor, zone 221, common instance, distinct
nonzero object ID, and finite coordinates. Because responses round XY to one
decimal, require actual XY within `0.11 m` of prediction, actual pairwise
distances at least `15.0 m`, and for each bot row its assigned fixture at least
`2.0 m` nearer than either foreign fixture. Stop on first mismatch without
further commands.

In each wave, require each identity to select its own assigned object,
independently record `grind/nearby_mortal`, navigate, cast and damage through
native behavior, receive exactly one native credit for that distinct object,
gain positive authoritative progression, complete ordered full-resource
recovery with fixed brain/mover counters, record one debt-free completion, and
normally self-logout. Require exact Director refill to three after every wave
before the next safe boundary. Across both waves require exactly six fixture
commands, six unique fixture objects, two credits per identity, and no directed
gameplay commands.

After wave two and refill, stop Game then Login gracefully and prove zero
runtime state. Move run-one logs losslessly into the fresh root. Restart under
the same deployed config with distinct Login and Game PIDs. Pre-arm three fresh
exact observers before admission, reprove the 180-second delay and ordered
zero-to-three bootstrap, and retain at least two continuous minutes at target
with no fixtures or gameplay commands.

Finally stop Game then Login gracefully; never force-stop. Prove zero bots,
runtimes, relevant processes, required listeners and live logs; unchanged
observed MySQL identities; exact host/source; exact config restoration; and
once-only cleanup. Stop the client observer through a fresh cooperative
sentinel. Validate every raw hash and row-chain link, maximum adjacent gap at
most 2000 ms, zero client/error samples, matching terminal hash, and
first-before-Login/last-after-cleanup coverage. Validate every autonomy raw
hash and lifecycle derivation independently.

Seal a complete relative-path/length/SHA-256 manifest with zero missing,
mismatched, duplicate, unsafe, unlisted, or reparse payloads. Commit only the
T-110 receipt and handoff. Report PASS/FAIL/INCOMPLETE with exact source/runtime
identities, both delay measurements, raw transforms, per-wave planner counts
and semantic hashes, selected tuples, predicted/actual matrices, per-wave
object/lifecycle/refill proof, restart/dwell, observers, manifest, cleanup,
retained anomalies, and unproven boundaries. PB-000 alone releases the lease
and dispatches independent evidence integration. T-037 remains blocked until a
T-110 PASS is independently integrated.
