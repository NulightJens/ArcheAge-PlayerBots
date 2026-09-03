# T-112 handoff

Verdict: `FAIL`.

T-112 passed its exact preflight, named-grade parser selftests, cooperative
scheduling rehearsal, delayed ordered bootstrap, live dual-planner wave-one
plan, and the one-shot three-fixture command/geometry gates. It then failed the
required fixed brain/mover counter invariant during ordered full-resource
recovery. The contract prohibited a fixture retry, so no wave two or restart
was attempted.

## Source identity and preflight

The run used binding commit `4277111312b09d549b29b0e3af8c0e4ebcef2168`,
tree `91dcbd8edfc8a496698ac652d646439c3f6234a9`, host commit
`62e3eb1d87da01194802ac886cd500134facad28`, and installed module commit/tree
`037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
`a1b9302625a65e10dfa9b7e11393a67134f914e8`. The reference and module were
clean and detached, the accepted 30-entry host overlay and reverse patch check
were exact, required ports and live logs were clear, and runtime, client, and
observer process counts were zero. OS-only observation recorded MySQL PIDs
`6308/8076`; no database or client access occurred.

The exact nine Director assignments were deployed. BotConfig changed from the
original 1,772-byte SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202` to
configured SHA-256
`5177e67c818d04ebf9c6b0164afc833e431cf4f687e660cffe02d5ef202de47c`,
with search radius `60` preserved. Cleanup restored the exact original bytes.

## Parser, planner, and bootstrap proof

The fixture parser accepted the immutable 359-byte T-110 response with SHA-256
`32474d410485ba1fe987a80a0ed3b41b86f3729ee4fe3b30263f593e4de844d3`:
object `44979`, anchor `20001`, zone/instance `221/0`, coordinates
`13616.7/13298.1/28.5`, named grade `Weak`, and zero errors. Numeric-only,
empty, localized, duplicate, missing, wrong-anchor/zone/instance, zero-object,
non-finite, and trailing-error cases all failed. Transform positive and
negative families also passed.

Both retained selftest planners enumerated all `18,399,744` tuples, found
exactly `125` eligible, and independently matched semantic SHA-256
`38b871081b5c3dc4be700d33f9890c51e10aa3b1c80839b8b916584fa553bfc1`.
They selected choice orders `28/68/56`, distances `10/15/15`, and angles
`+60/-60/+120`, with minimum pairwise `23.728951412717223 m` and row margin
`3.820467349324412 m`. Both ran in separate BelowNormal processes, yielded
every 8,192 tuples, and persisted no enumeration rows. The AboveNormal
scheduling sentinel retained 68 samples with maximum gap `533.132 ms`.

Run one used Login PID `128924` and Game PID `130968`. AboveNormal autonomy
observer PIDs `116812/130472/114256` were prearmed with two raw-verified offline
samples before admission. The Director started at
`2026-09-02T02:54:30.6627118Z`; first admission occurred at
`2026-09-02T02:57:30.7315731Z`, an elapsed `180068.861 ms` within
`[180000,195000]`. Admissions were `20001 → 20002 → 20003` with
`15021.616/15044.714 ms` gaps and zero failures, wrong-zone activations, or
overlap.

## Wave-one proof and terminal failure

The pre-plan stable transform set spanned `648.483 ms`; the latest row was
`375.413 ms` old. After the two live planners completed, fresh samples remained
bit-, identity-, state-, and resource-exact. The command set spanned
`682.017 ms`, its latest row was `217.587 ms` old, and the maximum observer gap
through command was `591.089 ms`.

The live planners again enumerated `18,399,744` tuples and independently
matched, finding `4,491` eligible with semantic SHA-256
`a20d960918bae85143bf81d9387049b17214238bb4708005f94ffb49315aa222`.
They selected orders `259/242/250`, distances `55/55/55`, and angles
`-75/+30/+150`. Predicted pairwise distances were
`106.77777647688175/102.02125919142284/101.77632138550061 m`; minimum
literal-row margin was `5.67660356870654 m`.

Exactly three commands were issued once in bot-ID order. The batch began
`928.269 ms` after the last revalidation sample and all responses completed in
`184.973 ms`. Responses parsed as three distinct `Weak` fixtures:
`33901/37936/37938`. Actual pairwise distances were
`106.74974550042647/102.0266513132784/101.80161122262996 m`; minimum actual
row margin was `5.740056279331796 m`. There was no parser, geometry, observer,
transform, or command error and no retry or directed gameplay command.

The lifecycle validator reached its readiness gate with exactly one normal
logout per identity and Director refills `20001 → 20002 → 20003`, restoring
three qualified bots with zero refill failures. It then failed on the required
fixed-counter evidence:

- Bot `20001`: 16 pending samples, brain steps `177/178/179`, mover steps
  `433/434/435/436`.
- Bot `20002`: 11 pending samples, fixed at brain/mover `179/436`.
- Bot `20003`: 13 pending samples, brain steps `178/179`, mover steps
  `435/436`.

The original terminal error was `fixed-counter-window-invalid:20001:16`.
Independent read-only replay verified every referenced raw hash and found the
same counter windows; bots `20001` and `20003` violate the contract. Wave two,
the six-object/two-credit aggregate, and restart were therefore not attempted.

## Observers, cleanup, and evidence

The same client-observer PID `129400` ran from before Login through after
cleanup and exited cooperatively without relaunch. Independent audit validated
all `1,315` raw snapshots and chain links, zero client/error samples, maximum
gap `728.458 ms`, and terminal chain
`99b33ba8083c20e42913b2437836ba729b2795c982cea6da19ac1d9f2f1095bb`.

Run-one autonomy replay independently validated `778/779/779` raw responses
and derivations with zero malformed samples. Its complete-ledger gate retained
a second failure: graceful-shutdown transport-error-tail gaps of
`2582.087/2597.298/2569.992 ms`, above the `2000 ms` ceiling. These gaps
occurred after the Director's graceful stop began; the prepared full audit
still evaluates the complete ledger and rejects them.

Game then Login stopped through graceful Ctrl+C; all autonomy observers and
the client observer stopped cooperatively. Six bot despawns, one Director stop,
and the zero-runtime cleanup marker were retained. Logs moved losslessly,
BotConfig restored exactly, and final audit proved zero runtime/client/observer
processes, required listeners, and live logs; unchanged MySQL identities;
exact reference/module/host state; and no force termination.

The immutable evidence root is
`D:\Codex-Labs\evidence\T-112\one-zone-autonomy-v9`. Manifest
`final/manifest.json` covers `8,602` entries and `29,445,543` bytes, is
`1,753,792` bytes, and has SHA-256
`b9a11a357e108a05f203ab5a43cd9f3ea9c83f1573748e20b1209b1352699b01`.
Post-write replay found zero missing, mismatched, duplicate, unsafe, unlisted,
or reparse payloads. Terminal-result SHA-256 is
`a6374a1be0338341681f00ae8c5f58c201e3397d59d6f2479dc05408e87935ef`.

Retained procedural anomalies are versioned pre-runtime wrapper corrections,
an incompatible first ledger-share read without client-observer relaunch,
PowerShell shorthand failures in prepared cleanup/observer audit wrappers,
and a no-op first shutdown invocation missing mandatory arguments. Exact audit
logic was recovered in memory without editing sealed scripts or weakening raw,
hash, derivation, source, cleanup, or config checks. No anomaly caused a
fixture retry, observer relaunch, force-stop, client/database access, or
evidence overwrite.

## Unproven boundaries and integration action

Wave-one lifecycle acceptance beyond the failed fixed-counter gate, all of
wave two, the six-object/two-credit aggregate, distinct-PID restart, second
delay measurement, ordered rebootstrap, two-minute target dwell, run-two
observer audit, AAEmu 3.0, client gameplay, database behavior, scale, soak, and
packaging remain unproven. T-037 remains blocked.

Integration action: independently verify the sealed v9 root and integrate this
commit only as the retained T-112 `FAIL` receipt and handoff. Do not reinterpret
it as autonomy acceptance and do not modify or reuse v9. PB-000 alone may
release or reassign the `aaemu12` lease after accepting cleanup. Any successor
requires a new task, exact binding/lease, and fresh versioned evidence root.
