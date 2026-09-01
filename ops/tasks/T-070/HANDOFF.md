# T-070 handoff

T-069 is integrated only as the truthful `FAIL` receipt for newly created MP
debt at completion. The verdict, evidence, and cleanup boundary are unchanged.

## Source identity and integration

- Declared integration base: `88182ede5f32399d0ffd8684391fb8a5260c7b0e`.
- Preserved Control Tower descendants: dispatch `1ebe2deae3dbbb8adcf9c5e0c29ee977eabb8a18`
  and task binding `c7e2f3090510ec3c754de0e4efef5e8dc79fa006`.
- Reviewed candidate: `1f26c4309868b379ab0fb64cad59ae48a6a75648`,
  exact parent `bbd6d8912be17d7084c439f2bbb42871b248fa16`.
- Integrated receipt replay: `00ab2a46c9c310488936e23b751a0b4be38168f7`.
- Immutable module source/tree: `4287ec53e998ecff2e5eb9670908166957a79662` /
  `1c6820f2f47ec79a3aab40174e1b448f894f67e4`.
- Integration action: fast-forward `integration/aaemu12-world` from the preserved
  task-binding descendant through the receipt replay and this handoff commit;
  do not rewrite history.

## Integrity proof and retained failure

The replayed T-069 receipt and handoff have the candidate's exact Git blobs.
The external manifest SHA-256 is
`0397ae08227fdc8c2952ef99208da592e8315471173efba068a5c4523f33b558`;
all 26 payloads independently matched their declared lengths and SHA-256
hashes, with zero missing or unlisted payloads.

The retained proof begins at natural full HP/MP while targetless and noncombat,
then records autonomous grind selection, exact-target mortal combat and kill
credit, `+14` XP, one progression record, and normal self-logout. Completion
retained an exact `-45` MP delta, so the result remains `FAIL`, not `PASS` or
`INCOMPLETE`. Iterations two and three and restart persistence were correctly
stopped and remain unproven.

## Runtime boundary and Control Tower action

T-070 did not start or control runtime, MySQL, or a client; access a database;
or change source, deployed hosts, external evidence, global ledgers, or the
lease. T-069's retained cleanup is zero bots/runtimes, zero Login/Game/client
processes, zero required listeners, empty active logs, graceful Game-then-Login
shutdown, and unchanged MySQL PIDs 6308 and 8076.

PB-000 must release the `aaemu12` lease using that T-069 cleanup proof, retain
both the T-065 v2 and T-069 v3 evidence roots, and dispatch only a bounded
lifecycle recovery correction that waits for a natural targetless, noncombat,
debt-free completion boundary before normal logout. Keep T-041 and T-037
blocked.
