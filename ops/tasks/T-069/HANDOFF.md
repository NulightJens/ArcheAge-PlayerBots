# T-069 handoff

T-069 is **FAIL** at the first complete functional violation. Iteration one
proved autonomous activity selection, exact-target combat, a server-authoritative
kill, `+14` native experience, one qualified progression record, and normal
self-logout. It also proved an exact `-45` MP delta at completion, so the
required debt-free post-activity recovery boundary was violated. The contract
therefore forbade iterations two and three and restart persistence.

## Source identity and changed files

- Lease/thread: `88182ede5f32399d0ffd8684391fb8a5260c7b0e` /
  `01a05c73-58af-7d72-94ef-bca2b2dd39e7`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `4287ec53e998ecff2e5eb9670908166957a79662`,
  clean tree `1c6820f2f47ec79a3aab40174e1b448f894f67e4`.
- Changed files: `ops/evidence/aaemu12-t069-one-bot-autonomy-v3.yaml`
  and `ops/tasks/T-069/HANDOFF.md` only.

## Proof and first blocker

Preflight proved the exact committed lease and task thread, clean registered
reference/module identities, the receipted 30-entry host overlay, qualified
binary/config hashes, isolated public-alpha schemas, zero conflicting
Login/Game/client processes, five free loopback ports, empty active logs, an
absent fresh evidence root, and observed MySQL PIDs 6308 and 8076. The retained
guarded runner started Login PID 119256 and Game PID 116676 with the exact five
listeners and initial bot/runtime counts of zero.

After `addbot 20001`, the persistent bot recovered naturally from 8119/9516 to
9516/9516 HP while remaining at full 6966/6966 MP, targetless, stationary, and
noncombat. Only then was one passive template-10004 opportunity staged at 12 m.
Bot 20001 (`ScaleBot000`, ObjId 26357) independently emitted
`activity=grind reason=nearby_mortal`, selected exact NPC 37947, entered the
existing navigation/combat path, cast once successfully, applied the native
damage effect, killed the 148 HP mortal instance, received exact server kill
credit, and gained 14 experience.

The single structured progression record is exact: baseline
`2026-09-01T10:20:08.5742533Z`, completion
`2026-09-01T10:20:13.8887707Z`, XP 8082014 to 8082028, HP 9516/9516 unchanged,
inventory 4 slots/10 units unchanged with a stable fingerprint, and MP 6966 to
6921. That `-45` MP delta is newly created resource debt at completion. Because
the observation is complete and violates the recovery contract, the verdict is
`FAIL`, not `INCOMPLETE`. Normal self-logout completed at
`2026-09-01T10:20:13.9112512Z` and returned the host to zero bots/runtimes.

## Cleanup, unproven boundaries, and integration action

Game then Login stopped through graceful Ctrl+C. The shutdown marker reports
zero remaining bots/runtimes; final checks found zero Login/Game/client
processes, zero required listeners, and empty active log paths. MySQL PIDs 6308
and 8076 were unchanged and never queried or controlled. The retained native
post-shutdown PhysicsThread disposal race did not prevent clean cleanup; forced
termination was not used.

Raw evidence is sealed at
`D:\Codex-Labs\evidence\T-069\one-bot-autonomy-v3`; all 26 manifest payloads
matched their declared lengths and SHA-256 hashes, with manifest SHA-256
`0397ae08227fdc8c2952ef99208da592e8315471173efba068a5c4523f33b558`.
No second/third iteration, restart persistence, larger population, client,
source/deployment, database, ledger, or AAEmu 3.0 work was attempted.

Integrator: fast-forward/cherry-pick this commit as a truthful T-069 `FAIL`
receipt only. PB-000 may release the `aaemu12` lease using this cleanup proof,
then dispatch only a bounded lifecycle correction that waits for natural
targetless, noncombat, debt-free completion before normal logout. Keep T-041
and T-037 blocked.
