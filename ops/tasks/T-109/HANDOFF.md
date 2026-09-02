# T-109 handoff

T-109 is **PASS-receipt-integrity** for a retained T-108 **INCOMPLETE**.
Candidate `9a9a84532f321fbe6755bcf4957c41c8503086e5`, sole parent
`3c1329cf6e95a46998f889aa71cdd2673aa7ea4c`, tree
`01079a441274e1f999307d2b978768fe1c23bf72`, and stable patch ID
`8268d979f201ad25ef2e531fff6e5a7d911d8af2` were verified exactly. Its only
two paths were replayed byte-for-byte as commit
`4a8fb0be83cda70113b683d00cd04c2772257a5e`, parent
`d3745799a5c7460739eca3a1ee8a79c3227485c8`, tree
`84cdb8bcc823dc59b721d7938464de0bf599f167`. Replayed blobs are
`b27a7b46967848d10d957adc801fd2679e679196` for the T-108 receipt and
`7853550e9a950ea9f0b38304b039c47284b6c639` for its handoff.

The immutable root
`D:\Codex-Labs\evidence\T-108\one-zone-autonomy-v7` remained read-only and
received no audit output. Manifest SHA-256
`ba96e8171f58bf1c0c4b600d5ae5d1c981aadb865d8e05ccf260e02f2e8a3430`
has exact length 4,408 and covers 31 payloads totaling 86,107 bytes. Terminal
result SHA-256 is
`3bba737efc1f047ec80e63e99f158cd823028c60170a20ee9b940173c2e6144c`.
Every canonical relative path, length, and SHA-256 was recomputed. Inventory
found zero missing, mismatched, duplicate, unsafe, unlisted, or reparse
payloads.

The independent single-precision angular-placement mirror decoded the exact
T-104 transform bit triples and enumerated every combination from distances
`{5,10,...,55}` and ordered angles
`{0,45,90,135,180,-135,-90,-45}`. Counts match exactly: 681,472 total,
finite, and within-range tuples; 427,142 with pairwise fixture separation at
least 20 m; 142 satisfying the literal row-oriented 3 m ownership gate; and
zero satisfying both row gates. The separate diagnostic column interpretation
has 4,386 ownership-valid and 2,430 combined tuples. The 2,430 count belongs
only to that column interpretation and does not weaken or reopen the failed row
gate.

All retained extrema match. Best overall row ownership is choice orders
`[1,7,3]`, distances `[5,5,5]`, angles `[45,-45,135]`, row margin
`4.331053860687011 m`, and only `14.077234576620343 m` minimum pairwise
distance. Best row ownership among pairwise-valid tuples is `[40,57,59]`,
distances `[30,40,40]`, angles `[0,45,135]`, and only
`2.1637271317828493 m` row margin. Maximum minimum pairwise separation is
`96.48496349078026 m` at `[87,74,85]`, distances `[55,50,55]`, angles
`[-45,90,-135]`, with row margin `-4.8120719752989345 m`.

Sealed preflight and cleanup evidence is exact: the unchanged 1,772-byte
BotConfig SHA-256 is
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`;
Login, Game, client, and observers never started; fixture, spawn, and directed
gameplay command counts are zero; there was no database or client access; and
no build or test ran. Final AAEmu/client/observer process, required-listener,
and live-log counts are zero. OS-observed MySQL PIDs remain `6308/8076`.
Reference, installed module, and the accepted 30-entry host overlay are exact
and unchanged, and no forced termination occurred.

All candidate anomalies remain retained: the required stop before
`selftest/result.json`, one bad read-only diagnostic argument, the first
comparison source's compile error before successful create-new v2, and the
first manifest script's ordered-dictionary aggregation error before successful
create-new v2 sealing. Integrator-side history also records the initial unbound
default checkout and the bounded reread after the first combined startup output
exceeded its window. None changed evidence, runtime, Git history, host, config,
database, client, source, tests, ledgers, lease, or workspace registry.

Changed repository paths are exactly the byte-identical T-108 receipt and
handoff plus `ops/evidence/aaemu12-t108-incomplete-integration-v1.yaml` and
this handoff. No global ledger, source, test, tooling, host, config, reference,
external evidence, database, or client path was edited.

T-108 remains **INCOMPLETE**. No live transform plan, selected tuple, fixture
geometry, lifecycle wave, refill, restart, second delay, rebootstrap, or dwell
was attempted or proven. Runtime behavior, scale, soak, packaging, client
gameplay, database behavior, and AAEmu 3.0 remain unproven. T-037 remains
blocked.

Integration action: fast-forward saved branch `integration/aaemu12-world` only
from `d3745799a5c7460739eca3a1ee8a79c3227485c8` through replay commit
`4a8fb0be83cda70113b683d00cd04c2772257a5e` and the single commit containing
this receipt and handoff, without history rewrite. PB-000 may then bind a fresh
row-valid runtime successor with a new lease and v8 root. Never reinterpret
T-108 as runtime acceptance and never reuse or overwrite v7.
