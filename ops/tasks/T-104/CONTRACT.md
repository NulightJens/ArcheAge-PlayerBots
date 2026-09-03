# T-104 contract: safe two-wave one-zone autonomy proof v6

Use only the exact committed T-104 binding/lease and registered AAEmu 1.2
workspaces. Require host `62e3eb1d87da01194802ac886cd500134facad28`,
installed module source/tree
`bf0ae36fc65eea4f341b936c9f7a961e9d474580` /
`7f33545311c972d7a0ed31fa7f75a016df3571f7`, compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
Game assembly SHA-256
`5a2b9e9a32163b6003c0d4feb05fb68a06086bc23b6a7a6d2bfdc455d07942f2`,
client observer blob `0f8b5e2b3a811877648020b715e4737777d4947a`, and
autonomy observer blob `94b82fb18a39e2356d43796b54e3eeea5ad7ee12`.

The fresh root is
`D:\Codex-Labs\evidence\T-104\one-zone-autonomy-v6`. Require it absent,
create it once, and never overwrite, rename, replace, or reuse it. Predecessor
roots are immutable. Preserve original deployed config bytes and SHA-256.
Require continuity SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`,
then change only the nine Director assignments: enabled; zone `221`; IDs
`[20001,20002,20003]`; population `2/3/3`; initial delay `180000` ms;
reconciliation `15000` ms; retry `30000` ms. Preserve configured search radius
`60`. Restore the exact original bytes after final shutdown.

Require the reference clean/detached, the installed module clean, the expected
30-entry host overlay, zero Game/Login/client processes, free ports
`1234/1237/1239/1250/1280`, and empty live-log locations. Observe current
MySQL process identities through OS process metadata only; never connect,
query, start, stop, or modify a database, and require those identities
unchanged at cleanup.

Create evidence-local transform-parser/planner scripts with create-new
semantics before runtime. Hash and retain them. Self-test the parser against
the exact T-102 invariant sample and negative duplicate/missing, localized,
non-finite, wrong-world, wrong-instance, and wrong-zone cases. It must parse
exactly one line of this grammar from preserved raw botdebug response bytes:

`Transform: world=<uint>, instance=<uint>, zone=<uint>, x=<float:R>, y=<float:R>, z=<float:R>, yaw_rad=<float:R>`

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

## Fixture preflight and deterministic ownership

At a fresh all-three targetless, noncombat, stationary, idle, full-resource
boundary in each of the two run-one waves, retain two consecutive raw samples
per identity whose exact transform values are stable. Require the six latest
samples to span at most 750 ms and be no older than 1000 ms. Each must contain
exactly one finite invariant transform with world `0`, zone `221`, a common
default instance, and the same online object identity used by its observer.

Before any fixture mutation, enumerate offsets `{5,10,...,55}` for each bot.
Mirror pinned `PositionAndRotation.AddDistanceToFront` with single-precision
operations exactly:

`fixtureX = x + (-distance * MathF.Sin(yaw_rad))`

`fixtureY = y + ( distance * MathF.Cos(yaw_rad))`

For every candidate tuple, retain the three predicted coordinates, all three
pairwise fixture distances, and the 3x3 anchor-to-fixture distance matrix.
A tuple is eligible only when every value is finite, every predicted fixture
pair is at least `20.0 m` apart, each own fixture is within the configured
`60 m` scan radius, and for each bot its own fixture is at least `3.0 m`
closer than either foreign fixture. Choose exactly one tuple deterministically:
maximize minimum pairwise fixture distance, then maximize minimum own-versus-
foreign margin, then minimize maximum offset, then minimize offset sum, then
lexicographically minimize `(d20001,d20002,d20003)`. Preserve the complete
enumeration and selected-plan hash. If no eligible tuple exists, stop safely
without issuing any fixture command.

Begin the command batch within 500 ms of the last qualifying raw sample and
complete all three local responses within 1500 ms. Issue exactly once, in bot
ID order, `spawnpassive 10004 <selected-distance> <botId>`. Preserve exact
request/response bytes. Never retry, substitute, or issue a later command
after an error. Require each response to report its expected anchor, zone 221,
common instance, a distinct nonzero object ID, and finite coordinates. Because
the response rounds XY to one decimal, require each actual XY within `0.11 m`
of prediction, actual pairwise distances at least `15.0 m`, and each bot's
assigned fixture still at least `2.0 m` nearer than either foreign fixture.
Stop on the first mismatch without further commands.

In each wave, require each identity to select its own assigned fixture object,
independently record `grind/nearby_mortal`, navigate, cast and damage through
native behavior, receive exactly one native credit for that distinct object,
gain positive authoritative progression, complete ordered full-resource
recovery with fixed brain/mover counters, record one debt-free completion,
and normally self-logout. Require exact Director refill to three after every
wave before the next safe boundary. Across both waves require exactly six
fixture commands, six unique fixture objects, two credits per identity, and no
directed gameplay commands.

After wave two and refill, stop Game then Login gracefully and prove zero
runtime state. Move run-one logs losslessly into the fresh evidence root.
Restart under the same deployed config with distinct Login and Game PIDs.
Pre-arm three fresh exact observers before admission, reprove the 180-second
delay and ordered zero-to-three bootstrap, and retain at least two continuous
minutes at target with no fixtures or gameplay commands.

Finally stop Game then Login gracefully; never force-stop. Prove zero bots,
runtimes, relevant processes, required listeners and live logs; unchanged
observed MySQL process identities; exact host/source; exact config restoration;
and once-only cleanup. Stop the client observer through a fresh cooperative
sentinel. Validate every raw hash and row-chain link, maximum adjacent gap at
most 2000 ms, zero client/error samples, matching terminal hash, and first-
before-Login/last-after-cleanup coverage. Validate all autonomy-observer raw
hashes and lifecycle derivations independently.

Seal a complete relative-path/length/SHA-256 manifest with zero missing,
mismatched, duplicate, unsafe, unlisted, or reparse payloads. Commit only the
T-104 receipt and handoff. Report PASS/FAIL/INCOMPLETE with exact source and
runtime identities, both delay measurements, raw transforms, planner hash and
selected tuples, predicted/actual matrices and tolerances, per-wave object and
lifecycle/refill proof, restart/dwell, observers, manifest, cleanup, retained
anomalies, and unproven boundaries. PB-000 alone releases the lease and
dispatches independent evidence integration; T-037 remains blocked until a
T-104 PASS is independently integrated.
