# T-073 handoff

T-073 is **INCOMPLETE** at the first missing required evidence boundary. The
sole started iteration completed the intended product behavior: bot `20001`
autonomously selected the exact staged mortal, navigated, killed it with server
credit, gained `+14` experience, recovered from `6921/6966` to `6966/6966` MP,
emitted ordered recovery `pending` then `completed`, produced one debt-free
progression record, and self-logged out normally. The observer was not armed in
time to capture brain/mover counters during that 2.632-second pending window,
so suspension non-advancement is absent/ambiguous and the iteration cannot be
counted as successful.

## Source identity and changed files

- Lease/thread: `7261daa3a715213781c2c6a4e163ece69abddda8` /
  `01a05ca9-8270-7ed1-aa9a-652dce47db8c`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `68aaaa3334a408d1d6d21e44472a8984e78618c2`,
  clean tree `9fa74d8057df8b5eb276ad223a10ed9b12791f88`.
- Changed files: `ops/evidence/aaemu12-t073-one-bot-autonomy-v4.yaml`
  and `ops/tasks/T-073/HANDOFF.md` only.

## Proof and blocker

Preflight proved the exact committed lease, clean task/control/reference/module
workspaces, the receipted 30-entry host overlay, exact binary/config hashes,
isolated public-alpha schemas, zero conflicting processes/clients/listeners,
empty active logs, an absent fresh root, and observed MySQL PIDs 6308/8076. The
retained guarded runner started Login PID 80636 and Game PID 119924 with exact
schema markers, five loopback listeners, and initial bot/runtime counts zero.

At the full, targetless, noncombat, stationary boundary, one passive template
10004 fixture was staged at 12 m. Bot 20001 (`ScaleBot000`, ObjId 7788)
independently selected target 37952, cast once, received exact kill credit,
gained 14 XP, and recovered naturally before its completion snapshot. All
behavior checks passed, but the read-only observer's first pending-window query
arrived after logout and returned the offline-bot response. Because direct
brain/mover counter non-advancement was not captured, the contract requires
`INCOMPLETE`, not `PASS` or `FAIL`. Iterations two/three and restart persistence
were not staged.

## Cleanup and integration action

Game then Login stopped through graceful Ctrl+C. Final proof shows zero bots,
runtimes, Login/Game/client processes, or required listeners; all five ports
are free, active logs are empty after evidence preservation, and MySQL PIDs
6308/8076 are unchanged. No force stop, database/client access, source/host/
ledger edit, gameplay/recovery/logout command, evidence retry, or larger
population occurred. The known post-readiness PhysicsThread disposal warning
was retained after the zero-bot/zero-runtime shutdown marker.

Raw evidence is sealed at
`D:\Codex-Labs\evidence\T-073\one-bot-autonomy-v4`; all 382 manifest payloads
match their declared lengths and SHA-256 hashes. Manifest SHA-256 is
`9f3b32de05e43ed64dff7dfd2b32219d3114aba7364f59674926a8c1a2278a14`.

Integrator: fast-forward/cherry-pick this commit as a truthful T-073
`INCOMPLETE` receipt only. PB-000 may release the `aaemu12` lease using the
cleanup proof. Any fresh proof must use a new immutable root and pre-arm its
read-only brain/mover counter observer before fixture staging; keep T-041 and
T-037 blocked.
