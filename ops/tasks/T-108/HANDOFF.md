# T-108 handoff

Verdict: `INCOMPLETE`. The mandatory retained-transform planner self-test
enumerated all `681,472` tuples but the implemented literal row-oriented
own-versus-foreign-fixture margin produced `0` eligible tuples instead of the
contracted exact `2,430`. The contract required a fail-closed stop before any
configuration, observer, runtime, client, database, fixture, or gameplay action.

## Source identity

- Binding commit/tree:
  `3c1329cf6e95a46998f889aa71cdd2673aa7ea4c` /
  `afe7815bf265219e842f5081316243459f3eec66`; dispatch base
  `6a4444cba7bb3e2e9452787afe4a85b24ee522f3`.
- Installed module source/tree:
  `037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
  `a1b9302625a65e10dfa9b7e11393a67134f914e8`.
- Host: `62e3eb1d87da01194802ac886cd500134facad28`;
  compatibility patch SHA-256
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`;
  Game assembly SHA-256
  `2385d16554b1fc9df1c77d45612b39c8fdac20721f20eac31ec9d0c32cc71696`.
- Client/autonomy observer blobs:
  `0f8b5e2b3a811877648020b715e4737777d4947a` /
  `94b82fb18a39e2356d43796b54e3eeea5ad7ee12`.

## Changed files

This artifact commit changes only:

- `ops/evidence/aaemu12-t108-one-zone-autonomy-v7.yaml`
- `ops/tasks/T-108/HANDOFF.md`

No module source, tests, global ledgers, lease, workspace registry, deployed
host, client, database, or predecessor evidence was changed.

## Proof and retained failure

Preflight proved the exact clean detached reference and installed module,
accepted 30-entry host overlay with complete reverse-patch check, exact patch
and assembly, original 1,772-byte BotConfig SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`,
zero runtime/client/observer processes, free required ports, empty live logs,
and unchanged OS-observed MySQL PIDs `6308/8076` without database access.

The self-test counts were `681,472` finite and within-radius tuples,
`427,142` meeting the pairwise gate, `142` meeting the literal row-oriented
3 m ownership gate, and `0` meeting both. The best row-ownership tuple had a
`4.331053860687011` m margin but only `14.077234576620343` m minimum pairwise
distance. The best row-ownership result among pairwise-valid tuples had only a
`2.1637271317828493` m margin. The maximum-minimum-pairwise tuple reached
`96.48496349078026` m but had a `-4.8120719752989345` m row margin.

Independent successor diagnostics identified the exact ambiguity without
weakening the failed gate. For matrix
`anchorToFixture[anchorIndex][fixtureIndex]`, row-oriented comparison yields
`142` ownership-valid and `0` eligible tuples; column-oriented fixture
ownership yields `4,386` ownership-valid and the exact contracted `2,430`
eligible tuples. A successor must resolve that interpretation before running a
new mandatory self-test. Full decoded float bits, coordinate samples, matrices,
and extrema are retained in the sealed terminal result and diagnostics.

The immutable v7 root is
`D:\Codex-Labs\evidence\T-108\one-zone-autonomy-v7`. Its manifest covers 31
payloads and 86,107 bytes with zero missing, length/hash mismatched, duplicate,
unsafe, unlisted, or reparse entries. Manifest SHA-256 is
`ba96e8171f58bf1c0c4b600d5ae5d1c981aadb865d8e05ccf260e02f2e8a3430`;
terminal-result SHA-256 is
`3bba737efc1f047ec80e63e99f158cd823028c60170a20ee9b940173c2e6144c`.

Retained anomalies are the missing `selftest/result.json` after the required
self-test stop, one read-only diagnostic argument error, a compile error in the
retained first comparison source before the successful create-new v2, and a
first manifest script aggregation error before successful create-new v2
sealing. None changed external state or caused evidence overwrite.

## Runtime state and unproven boundaries

Cleanup verdict is `PASS-CLEAN-PRE-RUNTIME-BLOCKER`. BotConfig remains the
exact original bytes. Login, Game, client and observers were never started;
there were zero fixture, spawn, and directed gameplay commands, no database or
client access, and no forced termination. Required process, port and live-log
counts remain zero, and observed MySQL identities are unchanged.

No live transform plan, selected tuple, fixture geometry, lifecycle wave,
refill, restart, delay measurement, rebootstrap, or dwell was attempted or
proven. Runtime behavior, scale, soak, packaging, client gameplay, database
behavior, and AAEmu 3.0 remain unproven. T-037 remains blocked.

## Exact integration action

Independently verify the sealed v7 root, then integrate only this receipt and
handoff commit as retained T-108 `INCOMPLETE`; do not reinterpret it as autonomy
acceptance and do not mutate or reuse v7. PB-000 alone releases or reassigns the
lease. Any successor requires a new exact task binding, lease, and fresh
versioned evidence root, with the row-versus-column ownership ambiguity
resolved before the next mandatory self-test.
