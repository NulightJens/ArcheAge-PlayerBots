# T-085 contract: integrate and verify the T-041 FAIL receipt

## Outcome

Independently verify the exact T-041 candidate
`9118d8a244d6ec89d742db7efa9ff5b43a201b5e`, its immutable external evidence,
its runtime semantics, and its cleanup. If and only if those checks are green,
integrate the exact two-file FAIL receipt onto saved branch
`integration/aaemu12-world` and add a concise verification receipt/handoff.

## Review gates

- Require exact candidate parent
  `e32a96ac20adc83b41662a3cb39e638bf12aaaf1` and exactly these two candidate
  paths: `ops/evidence/aaemu12-t041-one-zone-autonomy-v1.yaml` and
  `ops/tasks/T-041/HANDOFF.md`. Reject any source, tooling, ledger, lease, or
  predecessor-evidence change.
- Treat `D:\Codex-Labs\evidence\T-041\one-zone-autonomy-v1` as immutable,
  read-only evidence. Recompute all manifest path/length/SHA-256 entries, total
  bytes, the manifest SHA-256, and the absence of unlisted payloads. Expected:
  `6,271` payloads, `28,811,374` bytes, manifest
  `a141d75bffc87ef37e6a01f53d71261c2f5b441885522637a4a4adb190460a00`.
- Verify a bounded semantic matrix from raw/derived payloads, not only the
  candidate prose: exact task/lease/source/config fingerprints; all three
  observers offline before admission; Director start once and ordered
  zero-to-IDs `[20001,20002,20003]` in zone `221`; target/max `3`; zero
  admission failures/wrong-zone/duplicate overlap; exactly three allowed
  `spawnpassive 10004 12` commands; no addbot/removebot/directed gameplay;
  all identities selecting `grind/nearby_mortal`; three exact native credits
  and +42 XP all belonging to `20001`; no credited completion for `20002` or
  `20003`; no wave 2/restart; zero tick errors; observer raw-hash integrity;
  graceful Game-then-Login/observer cleanup; zero final processes/listeners/
  live logs; unchanged observed MySQL PIDs; no database access; and exact
  deployed config restoration SHA-256
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
- Retain and classify both declared limitations: continuous client sampling was
  not retained, and the post-cleanup PhysicsThread disposed-provider warning.
  They must not be silently upgraded into a PASS or erased.

## Integration and boundary

Do not start or control any runtime, MySQL/database, or client. Do not write the
registered host, runtime config, external evidence, product source, tooling,
global ledgers/lease, scale, soak, packaging, or AAEmu 3.0. Do not consume a
full-suite gate for a two-file evidence integration.

After review passes, replay/cherry-pick the candidate's exact two files onto
the current saved integration lineage, write
`ops/evidence/aaemu12-t041-fail-integration-v1.yaml` and
`ops/tasks/T-085/HANDOFF.md`, and fast-forward the saved branch without history
rewrites. If review fails, do not integrate the candidate; write only a truthful
rejection handoff within scope.

The handoff must state candidate/parent, stable patch identity, payload and
semantic counts, retained anomalies, saved-branch result, runtime non-use, and
the exact Control Tower action. T-041 remains FAIL and T-037 remains blocked.
PB-000 alone releases/reassigns the runtime lease and dispatches a correction.
