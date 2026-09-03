# T-085 handoff

T-085 is **PASS-receipt-integrity** for a retained T-041 **FAIL**. Candidate
`9118d8a244d6ec89d742db7efa9ff5b43a201b5e` was independently verified and
replayed byte-for-byte as commit
`205aad0376290cb9b6fa47f38bdd6b8878269df6`. It contains only the T-041 receipt
and handoff; no source, tooling, ledger, lease, host, runtime, config, database,
client, or predecessor-evidence change was accepted.

## Source identity and exact replay

- Candidate/parent:
  `9118d8a244d6ec89d742db7efa9ff5b43a201b5e` /
  `e32a96ac20adc83b41662a3cb39e638bf12aaaf1`.
- T-085 binding/dispatch:
  `a4e2c136ddc5a012059afbfaaff1f7b0d977b0c3` /
  `dba356bbadb63343b063a3a64036f762ca3e0df9`, thread
  `01a05da4-8357-7463-94db-dfc407bacee6`.
- Candidate and replay stable patch ID:
  `747dbd25e603ffdd9db4a476d306075969577ae4`.
- Replayed blobs are byte-identical: receipt
  `6c323d9df785890b615cdf7781c033909f295839` and T-041 handoff
  `517ee4b3e9dcfba68e0562a592dccdd342981609`.
- T-085 adds only
  `ops/evidence/aaemu12-t041-fail-integration-v1.yaml` and this handoff.

## Independent proof

Manifest `a141d75bffc87ef37e6a01f53d71261c2f5b441885522637a4a4adb190460a00`
was recomputed read-only. All 6,271 payloads and all 28,811,374 bytes matched
their declared paths, lengths, and SHA-256 values, with zero duplicate, unsafe,
missing, mismatched, unlisted, or reparse-point entries.

The semantic replay passed 67 of 67 assertions: source/binding/config 15/15,
observers/Director 18/18, commands/wave/failure 18/18, and cleanup/boundaries
16/16. Three prearmed observers were offline at samples 0 and 1. The Director
started once at zero, admitted `[20001, 20002, 20003]` in order in zone `221`,
held target/max `3`, and recorded zero failures, wrong-zone bots, or overlaps.
All 2,080 derived and transport samples and 2,056 raw responses passed embedded
length/hash review; the 24 response-less samples are the retained post-API
shutdown transport errors.

The eight raw command transports are exactly four metrics snapshots, one
roster GET, and three `spawnpassive 10004 12` fixtures. There is no `addbot`,
`removebot`, or directed gameplay command. All bots selected
`grind / nearby_mortal`, but all three native credits for targets `44963`,
`44975`, and `44974` went to bot `20001`. Only `20001` progressed
`8082084 -> 8082126` (`+42 XP`), recovered debt-free, logged out, and refilled.
Bots `20002` and `20003` never received a credited completion. Four raw metrics
snapshots retain zero tick errors and overlaps; wave 2 and restart were not run.

The original and restored config bytes both hash to
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Nine contracted Director fields were assigned; eight leaf values differed
because retry backoff was already `30000`, and no unexpected property changed.
Cleanup is graceful Game-then-Login with zero final AAEmu/observer/client
processes, listeners, or live logs, unchanged MySQL PIDs `[6308, 8076]`, and no
direct database access.

## Retained anomalies, branch result, and action

Continuous client-process sampling was not retained between its two zero
snapshots. The post-cleanup PhysicsThread disposed-provider warning remains
sealed under SHA-256
`1760ece520bcc4d8ba8cf04148190edc0d0aa8fb924bd64acc66e90600826cab`.
Neither limitation is upgraded into a PASS or erased.

Saved branch `integration/aaemu12-world` is fast-forwarded from
`a4e2c136ddc5a012059afbfaaff1f7b0d977b0c3` through the commit containing this
receipt and handoff, without history rewriting. T-085 did not start, stop,
control, query, or mutate any runtime, MySQL/database, client, registered host,
deployed config, external evidence, global ledger/lease, source, tooling,
AAEmu 3.0 state, or full-suite gate.

PB-000 may accept the sealed FAIL receipt, keep T-041 as FAIL and T-037
blocked, and release or reassign the `aaemu12` lease in a separate Control Tower
commit. Recovery requires a bounded source correction and a fresh versioned
runtime-proof task; the T-041 evidence root must never be reused or overwritten.
