# T-051 contract

## Outcome

The AAEmu 1.2 startup wrapper safely evaluates a live, growing Game log as one
string and accepts readiness only after exact credential-free selected-schema
markers for both Login and Game plus their required loopback/start markers.

## Pass

- Reproduce T-050's parameter-conversion failure with a deterministic fixture
  derived from its retained startup evidence. Do not edit v1/v2 evidence.
- Correct the growing-log read/call boundary so the predicate always receives
  exactly one scalar string. Read failures or ambiguous content must continue to
  fail closed and the wrapper must preserve graceful Ctrl+C recovery.
- Extend the AAEmu 1.2 compatibility patch so `GameService` emits the resolved
  `AppConfiguration.Instance.Connections.MySQLProvider.Database` after config
  precedence and before database update/startup advances. Log only the schema
  name—never host, port, user, password, connection string, or credentials.
- Add an exact escaped Game-schema pattern anchored to the expected GameService
  line. Donor schemas, updater prefixes, substring collisions, missing markers,
  wrong logger names, and regex metacharacters must fail closed.
- Cover scalar strings, accidental arrays, a large growing-log fixture, exact
  Login and Game matches, and all required negative cases in deterministic
  no-runtime tests. Do not weaken existing T-048 assertions.
- Regenerate the AAEmu 1.2 patch SHA-256 in every manifest location without
  changing frozen AAEmu 3.0 identity. Prove the whole patch applies cleanly to
  the registered clean `aaemu12_reference` using `git apply --check` only.
- Run PowerShell parser checks for all changed scripts/modules, manifest JSON
  parse/hash reproduction, prohibited credential-field scan, focused relevant
  AAEmu 1.2 tests, and `git diff --check`.
- Commit only declared source/docs and a concise handoff with exact integration
  action. Runtime and physical qualification remain pending.

## Non-goals

- Deploying to the integration host; starting Login/Game/client/MySQL; claiming
  a lease; accessing databases; retrying combat/stealth; resolving unrelated
  retained Game errors; Population Director, scale, soak, release, or AAEmu 3.0.
