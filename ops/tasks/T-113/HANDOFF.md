# T-113 handoff

T-113 is **PASS-receipt-integrity** for a retained T-112 **FAIL**. Candidate
`c98498696de64e4f8aa99509e2dbad3d1a6fd1b6`, sole parent
`4277111312b09d549b29b0e3af8c0e4ebcef2168`, tree
`6059e88d2f58c9c1632f2ff2194f47618ef6fb75`, and stable patch ID
`e58936f2c2016aa590e6825748558c37fdf302d2` were verified exactly. Its only
two paths were replayed byte-for-byte as commit
`94879030e015ab15bbfd5e896df6f9f6deb86476`, parent
`28e35c6dd13978ee6bb9d64c8a7926a3b0473286`, tree
`b5f9383a8cd37c10415245ca65d83bc3c84f01fa`. Replayed blobs are
`2d86d87340661d9d758ee366a2712e77c96ca0cd` for the T-112 receipt and
`6e4644bd668902a771d5cace8497dac1bad23f53` for its handoff.

The immutable root
`D:\Codex-Labs\evidence\T-112\one-zone-autonomy-v9` remained read-only and
received no audit output. Manifest SHA-256
`b9a11a357e108a05f203ab5a43cd9f3ea9c83f1573748e20b1209b1352699b01`
has exact length 1,753,792 and canonically covers 8,602 payloads totaling
29,445,543 bytes. Terminal-result SHA-256 is
`a6374a1be0338341681f00ae8c5f58c201e3397d59d6f2479dc05408e87935ef`.
Every relative path, length, and SHA-256 was recomputed. Inventory found zero
missing, mismatched, duplicate, unsafe, unlisted, or reparse payloads.

Independent named-grade parser replay accepted the immutable 359-byte fixture
response as object `44979`, anchor `20001`, zone/instance `221/0`, coordinates
`13616.7/13298.1/28.5`, grade `Weak`, and zero errors. All contracted fixture
and transform negative families were rejected.

Two distinct in-memory planner strategies independently completed all four
full enumerations. The selftest passes each enumerated `18,399,744` tuples,
found `125` eligible, and matched semantic SHA-256
`38b871081b5c3dc4be700d33f9890c51e10aa3b1c80839b8b916584fa553bfc1`.
They selected orders `28/68/56`, distances `10/15/15`, and angles
`+60/-60/+120`, with minimum pairwise `23.728951412717223 m` and literal-row
margin `3.820467349324412 m`. The live passes each enumerated another
`18,399,744`, found `4,491` eligible, and matched semantic SHA-256
`a20d960918bae85143bf81d9387049b17214238bb4708005f94ffb49315aa222`.
They selected orders `259/242/250`, distances `55/55/55`, and angles
`-75/+30/+150`. Every pass yielded at 8,192 tuples and persisted zero rows.

The sealed scheduling sentinel retained the exact `0..67` sequence with
maximum gap `533.132 ms`. Raw Game-log replay gives first admission at
`180068.861 ms` in order `20001 → 20002 → 20003`, with
`15021.616/15044.714 ms` gaps and zero failures, wrong-zone activations, or
in-flight overlap.

All wave-one transform payloads and hashes revalidated. Exactly three commands
were issued once in bot-ID order and returned distinct `Weak` fixtures
`33901/37936/37938`, without retry or wave two. Actual pairwise distances were
`106.74974550042647/102.0266513132784/101.80161122262996 m`; minimum actual
literal-row margin was `5.7400562793318 m`. No parser, geometry, transform,
observer-through-command, or directed-gameplay-command gate failed.

The retained terminal failure is authoritative. Read-only replay of all 43
lifecycle raw references reproduced the original
`fixed-counter-window-invalid:20001:16` error:

- Bot `20001`: 16 pending samples, brain `177/178/179`, mover
  `433/434/435/436`; not fixed.
- Bot `20002`: 11 pending samples, fixed at brain/mover `179/436`.
- Bot `20003`: 13 pending samples, brain `178/179`, mover `435/436`; not fixed.

Exactly one normal logout occurred per identity and Director refills restored
`20001 → 20002 → 20003` with zero failures, but those facts do not repair the
fixed-counter violation. The contract correctly stopped without retry, wave
two, run two, or restart.

All 1,315 client snapshots and chain links validated with zero client/error
samples, maximum gap `728.458 ms`, uninterrupted PID `129400`, no relaunch,
cooperative exit, and terminal chain
`99b33ba8083c20e42913b2437836ba729b2795c982cea6da19ac1d9f2f1095bb`.
Autonomy replay validated `778/779/779` raw responses with zero malformed
samples. It independently retained a second failure: complete-ledger gaps of
`2582.087/2597.298/2569.992 ms`, each above the `2,000 ms` contract during
the graceful-shutdown transport-error tail.

Cleanup replays exactly. Game PID `130968` then Login PID `128924` stopped by
graceful Ctrl+C. Raw logs contain one Director stop, six bot despawns, one
zero-runtime cleanup marker, one Web API stop, and one Login-daemon stop.
Logs moved losslessly. BotConfig restored to the exact 1,772-byte SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Terminal runtime, client, observer, listener, and live-log counts are zero.
MySQL PIDs `6308/8076`, the clean detached reference/module identities, and
the exact 30-entry host overlay remain unchanged. There was no database or
client access, force-stop, or destructive action.

All sealed anomalies remain explicit: versioned pre-runtime wrapper
corrections, recovery of the same live client-observer PID after an
incompatible share read, terminal audit shorthand failures before output, a
no-op first shutdown invocation missing arguments, and all three rejected
shutdown-tail observer gaps. Integrator verification also records the bounded
reread after startup output truncation, rejected read-only PowerShell
invocation slips, the distinction between retained monotonic ages and later
record timestamps, and rejection of a provisional decimal-coordinate matrix
before exact float-bit geometry. None mutated evidence, runtime, config, host,
source, tests, database, client, ledgers, lease, workspace registry, or Git
history.

Changed repository paths are exactly the byte-identical T-112 receipt and
handoff plus `ops/evidence/aaemu12-t112-fail-integration-v1.yaml` and this
handoff. No global ledger, source, test, tooling, host, config, reference,
external evidence, database, or client path was edited. No build, test, Login,
Game, observer, scale, soak, or packaging action ran.

T-112 remains **FAIL**. Wave-one lifecycle acceptance, all of wave two, the
six-object/two-credit aggregate, distinct-PID restart, second delay,
rebootstrap, two-minute dwell, and run-two observer audit are absent. AAEmu
3.0, client gameplay, database behavior, scale, soak, packaging, and successor
behavior remain unproven. T-037 stays blocked.

Integration action: fast-forward saved branch `integration/aaemu12-world`
only from `28e35c6dd13978ee6bb9d64c8a7926a3b0473286` through replay commit
`94879030e015ab15bbfd5e896df6f9f6deb86476` and the single commit containing
this receipt and handoff, without history rewrite. PB-000 may then accept the
retained FAIL and release or reassign the lease under Control Tower authority.
Never reinterpret T-112 as autonomy acceptance and never retry, extend, or
overwrite v9.
