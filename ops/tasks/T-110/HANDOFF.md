# T-110 handoff

Verdict: `INCOMPLETE`. The exact row-oriented planner and run-one delayed
bootstrap passed, but the attempt stopped after the first fixture response was
rejected by an overstrict local parser. The response used `grade=Weak` while
the parser required a numeric grade. The contract forbade retrying or issuing
later fixture commands after that validation error. Independent audit also
found autonomy-observer gaps above the 2,000 ms ceiling.

## Proven boundaries

- Exact binding `80bb3b9f3dc028e2b1830ef10421eed4dc13f4e1`, host
  `62e3eb1d87da01194802ac886cd500134facad28`, and installed module
  `037b4a87dd25df74fc8db5506c1cbc7fe3301b44` / tree
  `a1b9302625a65e10dfa9b7e11393a67134f914e8` were used.
- Preflight proved the clean detached reference/module, exact 30-entry host
  overlay, patch/assembly/observer identities, free required ports, empty live
  logs, zero runtime/client/observer processes, and OS-only MySQL PIDs
  `6308/8076`.
- The independent mandatory self-test enumerated all `18,399,744` tuples,
  found exactly `125` eligible, selected choice orders `28/68/56` with
  distances `10/15/15` and angles `+60/-60/+120`, and matched semantic hash
  `38b871081b5c3dc4be700d33f9890c51e10aa3b1c80839b8b916584fa553bfc1`.
  It persisted zero enumeration rows.
- Run one pre-armed all three observers with two raw-verified offline samples.
  Director admission occurred at `180038.044 ms` in order
  `20001 → 20002 → 20003`, with `15049.859/15058.018 ms` gaps and zero
  failures, wrong-zone activations, or overlap.
- The live plan re-enumerated `18,399,744` tuples, again found `125`, matched
  the independent semantic hash, and selected the same tuple. Predicted
  pairwise distances were `23.728951412717223`, `31.125385098200656`, and
  `33.68843783628416` m; minimum literal row margin was
  `3.820467349324412` m.

## Terminal blockers

Exactly one command was issued, without retry:

`spawnpassive 10004 10 20001 60`

The 359-byte response reported object `44979`, anchor bot `20001`, zone `221`,
instance `0`, and finite rounded coordinates `(13616.7, 13298.1, 28.5)`.
Request/response SHA-256 values are
`bd65dc9952049bf75e4610852cbfaade425e0714bc895769f11bb76766b314da` /
`32474d410485ba1fe987a80a0ed3b41b86f3729ee4fe3b30263f593e4de844d3`.
The local response grammar required numeric `grade`, but the valid server
response emitted `grade=Weak`; it failed closed before commands two and three.

The independently recomputed autonomy maximum gaps were `2575.486`,
`2565.165`, and `2565.218 ms`, exceeding the contracted `2000 ms`. Therefore
neither this partial fixture result nor the otherwise valid response can be
promoted to a PASS.

## Observer and cleanup proof

The same Windows PowerShell client-observer PID `127276` covered both before
Login and after cleanup. All `1,039` raw files and chain links validated, with
zero client/error samples, maximum gap `719.196 ms`, and terminal chain
`b106ad863e3b33b018d2e50041ba8885a842dd24e2a61e71c31d68dc4702dc7b`.
Run-one autonomy raw validations were `524/525/525`, with zero malformed
samples; post-shutdown transport-error tails were retained.

Game then Login and all autonomy observers stopped gracefully. The client
observer stopped cooperatively through a fresh sentinel. Runtime logs moved
losslessly, BotConfig was restored to exact 1,772-byte SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`,
and terminal cleanup proved zero runtime/client/observer processes, required
listeners, and live logs. MySQL identities, source/module state, and the host
overlay remained unchanged. No database or client access and no forced
termination occurred.

## Sealed evidence and retained anomalies

The immutable root is
`D:\Codex-Labs\evidence\T-110\one-zone-autonomy-v8`. Its manifest covers
`5,918` payloads and `25,649,977` bytes with zero missing, mismatched,
duplicate, unsafe, unlisted, or reparse entries. Manifest SHA-256 is
`b8678f4526f80197a0b659c9f22b660b686b3ed75731c3f57095123aedc943e1`;
terminal-result SHA-256 is
`bd29e1151367d88bd7df033dea51d88d4bec442ce3cdd54a1631afa5aab38a78`.

Retained anomalies include create-new superseded tooling drafts, recovery of
the continuously running client observer after an incompatible ledger-share
read, PowerShell shorthand/empty-byte wrapper failures during cleanup, and an
early retained `final.json` FAIL snapshot before the terminal
`final-post-client-stop.json` PASS. These did not cause a relaunch, retry,
force-stop, evidence overwrite, client access, or database access.

## Unproven boundaries and integration action

Wave-one three-fixture ownership, actual three-fixture geometry, both complete
lifecycle/recovery/logout/refill waves, all six fixture objects/credits, run-two
distinct-PID restart/rebootstrap, and the two-minute dwell are unproven. T-037
remains blocked.

Independently verify the sealed v8 root, then integrate only this receipt and
handoff commit as retained T-110 `INCOMPLETE`; do not reinterpret it as
autonomy acceptance and do not mutate or reuse v8. PB-000 alone releases or
reassigns the aaemu12 lease. A successor requires a new exact binding, lease,
fresh versioned evidence root, a response parser accepting the server's grade
token, and observer scheduling that remains below the 2-second gap ceiling
during both exhaustive live planners.
