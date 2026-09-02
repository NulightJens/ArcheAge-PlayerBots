# T-108 contract: angular two-wave one-zone autonomy proof v7

Use only the exact committed T-108 binding/lease and registered AAEmu 1.2
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
`D:\Codex-Labs\evidence\T-108\one-zone-autonomy-v7`. Require it absent,
create it once, and never overwrite, rename, replace, or reuse it. Every
predecessor root is immutable. Preserve original deployed config bytes and
SHA-256. Require continuity SHA-256
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

Self-test the planner against the exact T-104 retained transform bit patterns.
It must enumerate all `88^3 = 681472` tuples and find exactly 2,430 eligible
tuples under the gates below. Any parser/planner self-test mismatch is a
pre-runtime blocker.

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

## Fixture preflight and deterministic angular ownership

At a fresh all-three targetless, noncombat, stationary, idle, full-resource
boundary in each of the two run-one waves, retain two consecutive raw samples
per identity whose exact transform values are stable. Require the six latest
samples to span at most 750 ms and be no older than 1500 ms. Each must contain
exactly one finite invariant transform with world `0`, zone `221`, a common
default instance, and the same online object identity used by its observer.

Before any fixture mutation, enumerate every distance in `{5,10,...,55}` and
relative yaw in the exact ordered set
`{0,45,90,135,180,-135,-90,-45}` for each bot. Mirror the installed command
with single-precision operations exactly. Offset `0` skips rotation; nonzero
offsets compute `yawOffsetDegrees.DegToRad()` as
`MathF.PI / 180f * yawOffsetDegrees`, rotate only the detached clone, then call
`AddDistanceToFront(distance)`. The resulting horizontal formula is:

`fixtureX = x + (-distance * MathF.Sin(yaw_rad + offset_rad))`

`fixtureY = y + ( distance * MathF.Cos(yaw_rad + offset_rad))`

For every one of the 681,472 tuples, retain all three `(distance,angle)`
choices, predicted coordinates, three pairwise fixture distances, and the 3x3
anchor-to-fixture distance matrix. Eligibility requires every value finite,
every fixture pair at least `20.0 m` apart, each own fixture within `60 m`, and
each own fixture at least `3.0 m` closer to its bot than either foreign fixture.
Choose exactly one tuple deterministically: maximize minimum pairwise distance;
then maximize minimum own-versus-foreign margin; then minimize maximum
distance, distance sum, and sum of absolute yaw offsets; finally choose the
lexicographically earliest six-value sequence using the declared distance and
angle enumeration orders. Evaluate every tuple and seal the immutable input,
selection, count, and hash in memory before mutation. The complete enumeration
may be streamed to its create-new evidence file immediately after the one-shot
batch from those captured input bits, but it must independently recompute to
the same count, selected tuple, and hash before the wave can pass. If no tuple
is eligible, stop without issuing a fixture command.

Begin the command batch within 2000 ms of the last qualifying raw and complete
all three local responses within 3500 ms. Issue exactly once in bot-ID order:
`spawnpassive 10004 <distance> <botId> <yawOffsetDegrees>`. Use invariant
integer spellings from the chosen set. Preserve exact request/response bytes.
Never retry, substitute, or issue a later command after an error. Require each
response to report its expected anchor, zone 221, common instance, distinct
nonzero object ID, and finite coordinates. Because responses round XY to one
decimal, require actual XY within `0.11 m` of prediction, actual pairwise
distances at least `15.0 m`, and each assigned fixture at least `2.0 m` nearer
to its bot than either foreign fixture. Stop on first mismatch without further
commands.

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
T-108 receipt and handoff. Report PASS/FAIL/INCOMPLETE with exact source/runtime
identities, both delay measurements, raw transforms, 681,472-row planner hash
and selected tuples, predicted/actual matrices, per-wave object/lifecycle/refill
proof, restart/dwell, observers, manifest, cleanup, retained anomalies, and
unproven boundaries. PB-000 alone releases the lease and dispatches independent
evidence integration; T-037 remains blocked until a T-108 PASS is independently
integrated.
