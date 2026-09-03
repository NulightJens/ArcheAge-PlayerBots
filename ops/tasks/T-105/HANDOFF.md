# T-105 handoff

T-105 is **PASS-receipt-integrity** for a retained T-104 **FAIL**. Candidate
`3b0627f857c8e573cf67450b585e284f3f8ac0e9`, sole parent
`dde605d10912e8f282ce263680b70d1f02133249`, tree
`41319cfb1568da0219d5a5d2445a8902c9e9eea7`, and stable patch ID
`0ee155feca30e139f0325bdb29c0f9e926504302` were verified exactly. Its only
two paths were replayed byte-for-byte as commit
`37e43842dd737d492c7bb54984083b520be4f359`, parent
`8ae84e0e5ad769022f898c8a7b62b1623b1fca89`, tree
`1aecc7181e99e00783ec685e6b042e1416080288`. Replayed blobs are
`e4eb9ac4350ebfb55ea4fd306fc02d681e7f58cf` for the T-104 receipt and
`8151817918c4b60ca567d0767e8cb029cfba59fa` for its handoff.

The immutable root
`D:\Codex-Labs\evidence\T-104\one-zone-autonomy-v6` remained read-only and
received no audit output. Manifest SHA-256
`cbe9026f8e27ac0503521370a84be286b01ca38935cc8e3da4552e40bc31f7a0`
has exact length 2,753,724 and covers 17,528 payloads totaling 40,239,358
bytes. Every relative path, length, and SHA-256 was recomputed. Inventory found
zero missing, mismatched, duplicate, unsafe, unlisted, or reparse payloads.

The client observer independently replays 1,345 raw snapshots and 1,345 chain
rows with zero client or error samples, zero sequence/snapshot/hash mismatch,
maximum adjacent gap `802.382 ms`, first sample
`2026-09-01T22:55:58.3786962Z`, last sample
`2026-09-01T23:08:33.7223742Z`, and terminal chain
`6ad451b3e43d52a18f684e94862b4c13fa07482edee599addd2fead6dd826a64`.
The first sample precedes runtime, the last follows config restoration, and the
zero-byte sentinel plus summary prove cooperative exit.

All 5,357 autonomy raw responses were independently parsed and cross-linked to
their derived and transport records, with zero duplicate or unreferenced raws,
zero bad length/hash references, and zero error messages. Bots 20001/20002/20003
respectively contain 1,794/1,794/1,793 derived and transport samples and
1,786/1,786/1,785 raw responses. Their stable online object IDs are
`44939`, `7797`, and `37939`. Exact armed/live boundaries and two raw-verified
offline samples per bot precede first admission; latest second offline sample
`2026-09-01T22:58:57.9934530Z` precedes admission
`2026-09-01T23:00:16.1370210Z`.

Authoritative Director timestamps independently derive delay `180042.318 ms`,
ordered admissions `[20001,20002,20003]`, and reconciliation gaps
`15021.655` and `15051.770 ms`. There were three attempts and successes, zero
failures, wrong-zone admissions, in-flight overlap, or population breach.

All six qualifying transform raws parse independently as world/instance/zone
`0/0/221`, with exact finite single-precision values and bit patterns. Their
span is `530.307 ms`; the runner's separate second clock sample records latest
age `183.176 ms`. Latest raw hashes are
`cd340f80a292c78271185a75abe5f4daad4552819b85d88accdbe3549ed92336`,
`183589014af89ed5f1b0b907186d6653aca01810a8f7ef8da5bfe7593107427a`,
and `cf321108df9d1ec5d080180b54a0e0a41d0cd94d2d55748ecf4582e5cbbc0cf5`.

The independent single-precision geometry mirror covered every tuple in
`{5,10,...,55}^3` and cross-checked all 1,331 sealed enumeration rows with zero
mismatch. Enumeration length is 826,258 and SHA-256 is
`42e65f21c8d5dce3980f0dd55bc9c98b10405bdfc4ce81b994aaa358b7b7d788`.
All 1,331 tuples were finite and within 60 m; 87 reached 20 m pairwise
separation; zero reached 3 m ownership margin. Best margin remains
`-1.2237695385452456 m` at `[5,5,10]`. There is no selected tuple or plan.

Command and lifecycle records are consistent: zero fixture request/response,
zero fixture or gameplay command, zero spawn, zero world mutation, no retry,
no later command, no wave-one lifecycle, no wave two, no run two, and no
restart. T-104 therefore remains **FAIL**, and T-037 remains blocked.

Game then Login stopped gracefully without forced termination. Evidence proves
one Director stop, three despawns, one zero-bot cleanup marker, cooperative
observer exits, zero final runtime/client/task/observer processes, zero required
listeners and live logs, unchanged MySQL identities `6308` and `8076` under
OS-only observation, unchanged source/module/host overlay, and exact config
restoration to SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
All three retained logs match their lossless-move lengths and hashes. The native
`PhysicsManager` disposed-provider warning remains retained.

All seven candidate procedural anomalies remain in the integration receipt:
the fail-closed root-creation probe, two shell-policy-rejected inline observer
launch attempts, early nested-payload parser generations, post-start corrected
wrapper provenance, corrected config-hash transcription, corrected read-only
final audit probes, and the native shutdown warning. Integrator-side anomalies
are also retained: bounded rereads after startup-output truncation, unavailable
Python followed by the in-memory PowerShell verifier, corrected JSON timestamp
coercion, and corrected PowerShell token spacing. None changed evidence,
runtime, Git history, host, config, database, client, source, tests, ledgers, or
lease.

Changed repository paths are exactly the byte-identical T-104 receipt and
handoff plus `ops/evidence/aaemu12-t104-fail-integration-v1.yaml` and this
handoff. No build, test, runtime, observer, database, client, source, test,
host, config, reference, global-ledger, lease, or workspace-registry action was
performed.

Unproven boundaries remain both lifecycle/refill waves; selection, navigation,
combat, credit, progression, recovery, logout; actual fixture geometry;
distinct-PID restart; second delay; rebootstrap; two-minute dwell; scale, soak,
packaging, client gameplay, database behavior, and AAEmu 3.0.

Integration action: fast-forward saved branch `integration/aaemu12-world` only
from `8ae84e0e5ad769022f898c8a7b62b1623b1fca89` through the replay commit and
the single commit containing this receipt and handoff, without history rewrite.
PB-000 may then prepare a bounded angular fixture-placement source correction
that retains forward-only behavior by default. Never reinterpret T-104 as PASS
or reuse v6; keep T-037 blocked until a fresh successor passes and is
independently integrated.
