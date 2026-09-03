# T-043 contract

## Outcome

Wave 2 has isolated, versioned AAEmu 1.2 Game and Login schemas suitable for later lease-controlled runtime work, plus retained non-secret provenance sufficient for PB-000 to register them.

## Pass

- Before writing, prove the exact output directory and both destination schemas are absent; an existing target is a blocker and requires a higher version, never overwrite or reset.
- Read donor connection material only from registered workspace `aaemu12_legacy_t022` and use `New-IsolatedScaleDatabase.ps1` to create exactly `aaemu_playerbots_game_public_alpha_v1` and `aaemu_playerbots_login_public_alpha_v1`.
- Retain donor dumps and the generated 100-bot manifest under the exact new output directory; do not commit dumps or credentials.
- Verify schema presence, source/seed provenance, exact manifest count, unique character identities, and database names using read-only queries after creation.
- Commit a sanitized fingerprinted receipt and handoff containing no password, connection string, raw dump, or secret-bearing config.
- Do not start Login, Game, or a client; do not claim or edit the runtime lease or global workspace registry.

## Non-goals

- Runtime configuration or server startup.
- Resetting, deleting, dropping, truncating, or mutating any donor or retained schema.
- Product source changes, integration-host source changes, or population/combat acceptance.
