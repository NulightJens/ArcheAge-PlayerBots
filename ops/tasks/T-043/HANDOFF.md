# T-043 handoff

## Verdict

PASS. The exact absent Game and Login targets were created once from the registered `aaemu12_legacy_t022` donor, the exact external directory retains both donor dumps and the 100-bot manifest evidence, and independent read-only verification passed. No donor, retained database, source checkout, runtime lease, or global ledger was modified.

## Source identity

- Task worktree: `C:\Users\jensh\.codex\worktrees\cbbe\PB-W00-control`
- Starting detached HEAD: `b564ea55dbc21ade081b8ae15b22e5f38f0abd14`
- Wave 2 base ancestor: `e0350b3ee24352a1d96d81a60f34475fd8e56a45`
- Registered donor: `aaemu12_legacy_t022`, clean at `72fd2cc70bba3887e093e8a3800f13fed9b3e111`
- Registered validation host: `aaemu12_integration`, host HEAD `62e3eb1d87da01194802ac886cd500134facad28`, installed module HEAD `60f574d70c35dce418dc8a9ca53a99bd775bf099`
- The immutable task commit is the single T-043 commit containing this handoff and receipt; its hash is reported after commit creation.

## Changed files

- `ops/evidence/aaemu12-database-public-alpha-v1.yaml`
- `ops/tasks/T-043/HANDOFF.md`

External retained artifacts are confined to `D:\Codex-Labs\aaemu-1.2-playerbots-database-public-alpha-v1`. Database writes are confined to the two exact new schemas below.

## Sanitized database identity

- Game: `aaemu_playerbots_game_public_alpha_v1`
- Login: `aaemu_playerbots_login_public_alpha_v1`
- Donor identities: `aaemu_game` and `aaemu_login`
- Database provenance: `abcb9bc92e4a8688b8e6908a49ebf8a1b5d4662d398b26756604b72487c5951c`
- Receipt: `ops/evidence/aaemu12-database-public-alpha-v1.yaml`

No credential, provider value, connection string, or secret-bearing config content is recorded.

## Proof

- Before provisioning, the output directory and both destination schemas were absent; both donor schemas were present.
- MySQL and `mysqldump` 8.0.43 were available, the donor path/revision matched the registry, the donor checkout was clean, and no AAEmu 1.2 or unregistered Login/Game process was active.
- `New-IsolatedScaleDatabase.ps1` ran with the exact Game/Login schema names, template character `2`, and bot count `100` without weakening its safeguards.
- The output directory contains exactly two donor dumps, `retained-bot-ids.json`, and `seed-evidence.json`; the two dump scans found zero prohibited `DROP`, `TRUNCATE`, or `DELETE FROM` statements.
- The Game manifest has 100 rows, 100 distinct IDs, 100 distinct names, exact IDs `20001` through `20100`, one template ID (`2`), and one seed version (`t021-seed-v1`).
- The manifest joins to 100 active, uniquely identified, uniquely named characters with zero missing rows, invalid rows, or name mismatches. Seeded dependent data includes 1,000 abilities, 4,000 skills, 700 containers, and 1,400 items.
- Donor and target Game definitions match across 411 non-manifest columns. Donor and target Login definitions match across 24 columns and three tables, and their exact per-table row-count fingerprints match.
- The complete gate receipt is green with command/environment fingerprint `9a5eb51aa33c0e6c61d6dd5accaa76e1c125158268137bee3b0c5cae2cfe7428`.

## Retained failures

- The first read-only probe selected tracked template configs whose database fields are placeholders; it stopped before any write. The resolved local configs inside the exact registered donor workspace passed.
- Several report and audit formatting attempts had PowerShell type, expression, or parser errors. They changed no data; the corrected independent queries and audit passed.
- No product, provisioning, or verification failure remains.

## Runtime and environment state

- No Login, Game, client, or MySQL process was started, stopped, or controlled by T-043.
- No AAEmu 1.2 or unregistered Login/Game process was active before provisioning or during the final verification.
- The pre-existing Login process in registered workspace `aaemu30_runtime_frozen` was observed, retained, and untouched.
- The `aaemu12` runtime lease, workspace registry, Control Tower ledgers, validation host, donor checkout, deployed host, and client fixture were not modified.
- Both new schemas and the external output directory are retained. They are no longer valid absent provisioning targets and must not be reset, dropped, truncated, or overwritten.

## Unproven boundaries

- No live Login/Game startup, client connection, population, combat, stealth, cleanup, logout, restart, or scale-budget acceptance is claimed.
- The schemas are not authorized for runtime use until integration is accepted and PB-000 records the database identity under the lease workflow.

## Exact integration action

The Integrator should verify this task changed only the two declared repository paths, then cherry-pick the immutable T-043 commit reported with this handoff onto `integration/aaemu12-world`. Do not rerun provisioning during integration; the retained external schemas and artifacts are the proof subject.

## Exact PB-000 registration action

After the Integrator accepts the T-043 commit and the Wave 2 integrated candidate is green, PB-000 should make one control-only bookkeeping commit that:

1. Adds a versioned `aaemu12_database_public_alpha_v1` registration to `ops/WORKSPACES.yaml` containing the exact Game and Login schema names, external output path, donor workspace `aaemu12_legacy_t022`, evidence receipt path, database provenance hash, and lease-controlled access; it must contain no credential or connection material.
2. Sets T-043 to `status: done` and `integration: integrated` in `ops/BOARD.yaml`, recording the immutable integrated commit.
3. Records the accepted T-043 receipt and registered database identity in `ops/CURRENT.yaml` without changing track policy.
4. Sets `ops/RUNTIME-LEASE.yaml` `aaemu12.database_identity` to `aaemu12_database_public_alpha_v1` only when the integrated candidate is green. Leave the lease unclaimed until PB-000 explicitly activates and assigns the later runtime task.
