# T-105 contract: independently integrate retained T-104 FAIL

Act only as the evidence Integrator for T-104 candidate
`3b0627f857c8e573cf67450b585e284f3f8ac0e9`, sole parent
`dde605d10912e8f282ce263680b70d1f02133249`, tree
`41319cfb1568da0219d5a5d2445a8902c9e9eea7`, and stable patch ID
`0ee155feca30e139f0325bdb29c0f9e926504302`. Start only from PB-000's
exact committed T-105 binding. Require exactly two candidate paths and blobs:

- `e4eb9ac4350ebfb55ea4fd306fc02d681e7f58cf` T-104 receipt;
- `8151817918c4b60ca567d0767e8cb029cfba59fa` T-104 handoff.

Replay those blobs byte-identically onto the committed saved lineage. Do not
merge the runtime worker branch, rewrite the evidence, or change source/tests.

Treat `D:\Codex-Labs\evidence\T-104\one-zone-autonomy-v6` as immutable and
read-only. Require manifest SHA-256
`cbe9026f8e27ac0503521370a84be286b01ca38935cc8e3da4552e40bc31f7a0`.
Independently validate every manifest relative path for canonical safety and
uniqueness, rehash all 17,528 listed payloads and 40,239,358 bytes, reject
reparse points, and require zero missing, mismatched, duplicate, unsafe, or
unlisted payloads. Do not write an audit file into the root.

Independently replay the 1,345-row client chain and all 5,357 autonomy raw
responses from preserved bytes. Recompute first/last coverage, zero client and
error samples, maximum gap, observer pre-arm ordering, object identity, raw
length/hash references, and duplicate/unreferenced counts. Recompute the
Director delay from authoritative timestamps and require `180042.318 ms`,
ordered admissions `[20001,20002,20003]`, expected reconciliation gaps, and
zero failure/wrong-zone/overlap.

Parse the six qualifying raw transform samples independently. Require exact
world/instance/zone `0/0/221`, sample span `530.307 ms`, latest age
`183.176 ms`, finite round-trip coordinates/yaw, and the recorded raw hashes.
Independently mirror pinned single-precision front-offset geometry for every
tuple in `{5,10,...,55}^3`; do not trust the retained planner summary alone.
Require exactly 1,331 finite/in-range tuples, 87 with pairwise separation at
least 20 m, zero with minimum ownership margin at least 3 m, best achievable
margin `-1.2237695385452456 m`, and no selected tuple. Verify the complete
enumeration SHA-256
`42e65f21c8d5dce3980f0dd55bc9c98b10405bdfc4ce81b994aaa358b7b7d788`.

Require zero fixture request/response payloads, zero fixture/gameplay commands,
zero world mutation, no retry or run two, and consistent terminal FAIL records.
Verify preserved shutdown/config/observer cleanup evidence: graceful Game then
Login, exact config restoration, cooperative observer exits, zero required
processes/listeners/live logs, unchanged source/overlay and observed MySQL
identities. Retain the native shutdown warning and every procedural anomaly.

After every independent gate passes, commit only the T-105 integration receipt
and handoff after the exact two-blob replay, then fast-forward saved branch
`integration/aaemu12-world` through exactly those two commits without history
rewrite. Report replay commit/tree, final saved head/tree, manifest and raw
replay proof, independently recomputed geometry, cleanup, retained anomalies,
and unproven boundaries. Verdict remains FAIL; T-037 stays blocked. PB-000 may
then prepare a bounded source task adding deterministic angular fixture
placement while retaining the current forward-only command behavior by
default.
