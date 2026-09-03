# T-111 handoff

T-111 is **PASS-receipt-integrity** for a retained T-110 **INCOMPLETE**.
Candidate `1b914085d64b3957b3c394d9b9f75600c91e2a96`, sole parent
`80bb3b9f3dc028e2b1830ef10421eed4dc13f4e1`, tree
`03d3e8fcda1ae196023c9acd412e212770940261`, and stable patch ID
`211ac120413c1c483c1ad9892afa08eecc9af3a9` were verified exactly. Its only
two paths were replayed byte-for-byte as commit
`d4e1bede6647ea8d7d97fed14a4819042cb9c15e`, parent
`1b1a4cf3d43ec530b9889a82f17cc0abb76e2129`, tree
`7d5b6b1c90da1ef79a237c3bf6e9d95e780c4fe5`. Replayed blobs are
`eed5dbb70e84ff2421ada3b54b4085b216526ebc` for the T-110 receipt and
`dd362f075e06119293cb6e20f3eb37107dcb7e83` for its handoff.

The immutable root
`D:\Codex-Labs\evidence\T-110\one-zone-autonomy-v8` remained read-only and
received no audit output. Manifest SHA-256
`b8678f4526f80197a0b659c9f22b660b686b3ed75731c3f57095123aedc943e1`
has exact length 1,249,803 and covers 5,918 payloads totaling 25,649,977 bytes.
Terminal-result SHA-256 is
`bd29e1151367d88bd7df033dea51d88d4bec442ce3cdd54a1631afa5aab38a78`.
Every canonical relative path, length, and SHA-256 was recomputed. Inventory
found zero missing, mismatched, duplicate, unsafe, unlisted, or reparse
payloads.

The separately compiled active primary and repeat planners decoded the exact
live transform bits and independently enumerated all `18,399,744` ordered
tuples. Both found exactly `125` eligible and emitted semantic SHA-256
`38b871081b5c3dc4be700d33f9890c51e10aa3b1c80839b8b916584fa553bfc1`.
They selected choice orders `[28,68,56]`, distances `[10,15,15]`, angles
`[60,-60,120]`, and identical fixture float bits. Minimum pairwise distance is
`23.728951412717223 m`; minimum literal row margin is
`3.820467349324412 m`. No enumeration rows were persisted.

Independent replay of the sealed Game log gives first admission at
`180038.044 ms` in order `20001 → 20002 → 20003`, with
`15049.859/15058.018 ms` gaps and zero failures, wrong-zone rows, or in-flight
overlap. The fourth spawn row is a run-one refill after the single retained
fixture was killed; it is not complete lifecycle or ownership proof.

All 1,039 client snapshots and chain links validated with zero client/error
samples, maximum gap `719.196 ms`, uninterrupted PID `127276`, no relaunch,
cooperative exit, and terminal chain
`b106ad863e3b33b018d2e50041ba8885a842dd24e2a61e71c31d68dc4702dc7b`.
Raw autonomy validations were `524/525/525`, with zero malformed samples.
Independently recomputed maximum gaps were `2575.486`, `2565.165`, and
`2565.218 ms`; each exceeds the contracted `2,000 ms` ceiling.

Exactly one fixture request and one response exist, without retry or later
fixture command. Request/response SHA-256 values are
`bd65dc9952049bf75e4610852cbfaade425e0714bc895769f11bb76766b314da` /
`32474d410485ba1fe987a80a0ed3b41b86f3729ee4fe3b30263f593e4de844d3`.
The 359-byte valid response reports object `44979`, anchor bot `20001`, zone
and instance `221/0`, finite rounded coordinates
`13616.7/13298.1/28.5`, and authoritative named `grade=Weak`. The active local
parser alone required numeric `grade=\d+`, causing
`fixture-response-match-count:0`. The contract correctly stopped the attempt
without retry. This is a harness parser defect, not server/source behavior.

Cleanup replays exactly: Game then Login stopped gracefully, all four bots
despawned, shutdown cleanup reported zero bots/runtimes, Web API and Login
daemon each stopped once, and the three autonomy observers plus the client
observer exited cooperatively. Logs moved losslessly. BotConfig restored to
the exact 1,772-byte SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Terminal process, listener, and live-log counts are zero. MySQL PIDs
`6308/8076`, the clean detached reference/module identities, and the exact
30-entry host overlay are unchanged. There was no database/client access,
force-stop, or destructive action.

All sealed anomalies remain explicit: thirteen superseded pre-runtime tool
drafts and the supplemental recovery/ledger drafts; recovery of the same live
client-observer PID after an incompatible share-mode read; the numeric-grade
parser; all three observer-gap failures; cleanup predicate/empty-byte wrapper
bugs; and the early retained `final.json` FAIL before terminal
`final-post-client-stop.json` PASS. Integrator verification also records the
initial unbound clean checkout, one redundant PowerShell revision-argument
quirk, the bounded reread after startup output truncation, separate planner
compilation after a rejected combined compilation unit, and rejection of a
provisional localized timestamp calculation before exact string replay. None
mutated evidence, runtime, config, host, source, tests, database, client,
ledgers, lease, workspace registry, or Git history.

Changed repository paths are exactly the byte-identical T-110 receipt and
handoff plus `ops/evidence/aaemu12-t110-incomplete-integration-v1.yaml` and
this handoff. No global ledger, source, test, tooling, host, config, reference,
external evidence, database, or client path was edited. No build, test, Login,
Game, observer, v9, scale, soak, or packaging action ran.

T-110 remains **INCOMPLETE**. Commands two/three, actual three-fixture
geometry and ownership, both full lifecycle/recovery/logout/refill waves, six
fixture credits, run-two distinct-PID restart/rebootstrap, and the two-minute
dwell are absent. One incidental first-fixture kill/refill does not satisfy
those gates. Runtime successor behavior, scale, soak, packaging, client
gameplay, database behavior, and AAEmu 3.0 remain unproven. T-037 stays
blocked.

Integration action: fast-forward saved branch `integration/aaemu12-world`
only from `1b1a4cf3d43ec530b9889a82f17cc0abb76e2129` through replay commit
`d4e1bede6647ea8d7d97fed14a4819042cb9c15e` and the single commit containing
this receipt and handoff, without history rewrite. PB-000 may then bind a fresh
parser- and observer-safe v9 successor with a new lease and versioned evidence
root. Never reinterpret T-110 as autonomy acceptance and never reuse, retry,
or overwrite v8.
