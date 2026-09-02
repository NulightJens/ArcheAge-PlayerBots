# T-112 contract: parser- and scheduler-safe row-valid autonomy proof v9

Use only PB-000's exact committed T-112 thread/worktree binding and AAEmu 1.2
runtime lease. Require host `62e3eb1d87da01194802ac886cd500134facad28`,
installed module source/tree
`037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
`a1b9302625a65e10dfa9b7e11393a67134f914e8`, compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
Game assembly SHA-256
`2385d16554b1fc9df1c77d45612b39c8fdac20721f20eac31ec9d0c32cc71696`,
client observer blob `0f8b5e2b3a811877648020b715e4737777d4947a`, and autonomy observer
blob `94b82fb18a39e2356d43796b54e3eeea5ad7ee12`. Do not build or reinstall.

The fresh root is
`D:\Codex-Labs\evidence\T-112\one-zone-autonomy-v9`. Require it absent,
create it once, and never overwrite, rename, replace, reuse, or write into a
predecessor root. Preserve original deployed config bytes and require SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Change only the nine Director assignments: enabled; zone `221`; IDs
`[20001,20002,20003]`; population `2/3/3`; initial delay `180000` ms;
reconciliation `15000` ms; retry `30000` ms. Preserve search radius `60` and
restore the exact original bytes after final shutdown.

Require the reference clean/detached, installed module clean, the accepted
30-entry host overlay, zero Game/Login/client processes, free required ports,
and empty live-log locations. Observe existing MySQL process identities through
OS metadata only; never connect, query, start, stop, or modify a database.
Require those identities unchanged at cleanup. The ArcheAge client is forbidden.

Create every evidence-local parser, planner, observer wrapper, runner, and audit
script with create-new semantics before runtime, then hash and designate active
versions. Superseded drafts remain sealed and explicit. Do not accept an
untested regex or a planner that can monopolize the observer process.

## Mandatory parser and planner preflight

Self-test the fixture-response parser against the exact immutable 359-byte
T-110 response with SHA-256
`32474d410485ba1fe987a80a0ed3b41b86f3729ee4fe3b30263f593e4de844d3`.
Require exactly one match containing object `44979`, anchor `20001`,
zone/instance `221/0`, finite coordinates `13616.7/13298.1/28.5`, named grade
`Weak`, and zero errors. The invariant grade grammar is
`[A-Za-z][A-Za-z0-9_]*`; numeric-only, empty, localized, duplicate, missing,
wrong-anchor, wrong-zone, wrong-instance, zero-object, non-finite, and trailing
error cases must fail. Runtime parsing must use the exact tested grammar and
must still stop without retry or later commands on any mismatch.

Self-test transform parsing against the invariant grammar and all T-110
negative cases:

`Transform: world=<uint>, instance=<uint>, zone=<uint>, x=<float:R>, y=<float:R>, z=<float:R>, yaw_rad=<float:R>`

Use the exact T-104 retained transform bits:

- bot 20001: X `1179957453`, Y `1179643699`, yaw `-1065611208`;
- bot 20002: X `1179966362`, Y `1179652403`, yaw `-1065603658`;
- bot 20003: X `1179971072`, Y `1179656704`, yaw `-1065577192`.

Enumerate distances `{5,10,...,55}` and ordered angles
`{0,15,30,45,60,75,90,105,120,135,150,165,180,-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15}`
for each bot: exactly `264^3 = 18,399,744` tuples. Mirror the installed command
with single-precision operations: offset zero skips rotation; each nonzero
offset computes `MathF.PI / 180f * yawOffsetDegrees`, adds it to source yaw on
the detached clone, then calls `AddDistanceToFront(distance)`. Placement is:

`fixtureX = x + (-distance * MathF.Sin(yaw_rad + offset_rad))`

`fixtureY = y + ( distance * MathF.Cos(yaw_rad + offset_rad))`

For `anchorToFixture[botIndex][fixtureIndex]`, require every bot row's own
diagonal fixture to be at least `3.0 m` nearer than both foreign columns. Never
accept column-oriented comparison. Require finite values, each own fixture
within `60 m`, and every fixture pair at least `20.0 m` apart. Require exactly
125 eligible tuples; choice orders `[28,68,56]`; distances `[10,15,15]`;
angles `[60,-60,120]`; XY bits
`[1179959989/1179633778,1179951580/1179656575,1179985828/1179652438]`;
minimum pairwise `23.728951412717223 m`; minimum row margin
`3.820467349324412 m`; and semantic SHA-256
`38b871081b5c3dc4be700d33f9890c51e10aa3b1c80839b8b916584fa553bfc1`.
Independently repeat the complete computation and require equality. Do not
persist enumeration rows.

Both exhaustive planner implementations must cooperatively yield on the same
fixed, recorded tuple interval no greater than 8,192. Run them sequentially in
separate hidden processes at BelowNormal priority; run client and autonomy
observers at AboveNormal priority with 500 ms targets. Before any runtime,
perform a bounded scheduling rehearsal using the retained transform bits while
an evidence-local 500 ms sentinel runs under the same priority arrangement.
Require both full planners and the exact self-test results while the sentinel
has maximum adjacent gap at most 1,500 ms. Any priority-setting failure,
planner mismatch, or larger gap is a pre-runtime blocker. Record process IDs,
priorities, yield interval, raw sentinel timestamps, and planner timings.

## Runtime observation and delayed Director bootstrap

Launch the exact client-process observer before Login, validate raw and ledger
row zero at zero clients, and retain the same PID through both runs and final
cleanup. Never relaunch. Start loopback Login then Game through the accepted
wrapper. Immediately after API readiness, launch one exact autonomy observer
per bot. Require first samples within 120 seconds, then two raw-verified offline
samples and armed/live boundaries for all three before admission.

For each run require authoritative Director start and admissions
`[20001,20002,20003]`, one per reconciliation tick, in zone/default world and
instance, maximum three, and zero failures, wrong-zone activations, or overlap.
Require first-admission elapsed time in `[180000,195000]` ms and stop without
retry if outside it.

Observers keep 500 ms targets throughout runtime and planner work. Audit each
raw hash and transport timestamp continuously. Before the first fixture command
of a wave, require no adjacent client or autonomy gap above 2,000 ms and no
malformed or transport-error sample. Any gap or error is terminal for the run;
issue no fixture command after detecting one.

## Live fixture planning and post-plan freshness

At a fresh all-three targetless, noncombat, stationary, idle, full-resource
boundary in each of two run-one waves, retain two consecutive raw samples per
identity whose exact transform bits are stable. Require the six latest samples
to span at most 750 ms and be no older than 1,500 ms. Each has exactly one
finite transform with world `0`, zone `221`, common default instance, and the
observer's current online object identity.

Seal those input bits. Run the two complete planners sequentially with the
preflight priority and cooperative-yield policy. Eligibility and deterministic
ordering are identical to the mandatory self-test: maximize minimum pairwise,
then minimum row margin; minimize maximum distance, distance sum, and sum of
absolute offsets; finally lexicographically earliest choice orders. Require at
least one eligible tuple plus identical count, semantic hash, selection, and
summary. Keep full row streams in memory only.

Planning time does not make the stationary input stale by itself. Immediately
after both planners finish, require two new consecutive raw samples per bot,
spanning at most 750 ms and no older than 1,500 ms, with exact object identity,
world, instance, zone, state, resources, and transform float bits unchanged
from the sealed pre-plan inputs. Reaudit all observer gaps through these rows.
If any bit/state changed or any gap exceeds 2,000 ms, stop before fixture
mutation. Begin the command batch within 1,500 ms of the last post-plan
revalidation sample and complete all three responses within 3,500 ms.

Issue exactly once in bot-ID order:
`spawnpassive 10004 <distance> <botId> <yawOffsetDegrees>`. Use invariant grid
integers. Preserve exact request/response bytes. Never retry or substitute, and
issue no later command after an error. Parse each response with the preflighted
named-grade grammar. Require expected anchor, zone 221, common instance,
distinct nonzero object ID, finite coordinates, one valid grade token, and zero
errors. Because responses round XY to one decimal, require actual XY within
`0.11 m` of prediction, actual pairs at least `15.0 m`, and every bot row's
assigned fixture at least `2.0 m` nearer than either foreign fixture.

In each wave, require each identity to select its own assigned object,
independently record `grind/nearby_mortal`, navigate, cast and damage through
native behavior, receive exactly one native credit for that distinct object,
gain positive authoritative progression, complete ordered full-resource
recovery with fixed brain/mover counters, record one debt-free completion, and
normally self-logout. Require Director refill to three after each wave. Across
both waves require exactly six commands, six unique objects, two credits per
identity, and no directed gameplay commands.

After wave two and refill, stop Game then Login gracefully and prove zero
runtime state. Move run-one logs losslessly into the root. Restart under the
same config with distinct Login and Game PIDs. Pre-arm three fresh exact
autonomy observers before admission, reprove the delayed ordered zero-to-three
bootstrap, and retain at least two continuous minutes at target with no
fixtures or gameplay commands.

Finally stop Game then Login gracefully; never force-stop. Prove zero bots,
runtimes, relevant processes, required listeners and live logs; unchanged
observed MySQL identities; exact host/source; exact config restoration; and
once-only cleanup. Stop the client observer through a fresh cooperative
sentinel. Validate every client raw hash and row-chain link, maximum adjacent
gap at most 2,000 ms, zero client/error samples, matching terminal hash, and
first-before-Login/last-after-cleanup coverage. Independently validate every
autonomy raw hash, timestamp, state, and lifecycle derivation.

Seal a complete canonical relative-path/length/SHA-256 manifest with zero
missing, mismatched, duplicate, unsafe, unlisted, or reparse payloads. Commit
only the T-112 receipt and handoff. Report PASS/FAIL/INCOMPLETE with exact
source/runtime identities, parser selftests, scheduling rehearsal, both delay
measurements, transforms and post-plan revalidation, planner results,
predicted/actual matrices, lifecycle/refill proof, restart/dwell, observers,
manifest, cleanup, anomalies, and unproven boundaries. PB-000 alone releases
the lease and dispatches independent integration. T-037 remains blocked until
a T-112 PASS is independently integrated.
