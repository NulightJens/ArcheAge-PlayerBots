# T-125 handoff: corrected intelligence-wave verification

## Outcome

PASS. The corrected integrated candidate completed its single T-125 full-suite
invocation with 1,957 passed, four intentional skips, zero failures, and 1,961
total tests. The fast-feedback build/test loop is now durable and linked from
the project manifest. The combined source is eligible for a separately leased
live smoke; no live claim is made by this task.

## Bound identity

- Tested module source: `1a52e7b44fab76939d9409561bdfa4739f1425e6`
- Tested module tree: `3f473420369dc46bcf3884c90cfc140b58d30c34`
- AAEmu 1.2 host: `62e3eb1d87da01194802ac886cd500134facad28`
- Build-only checkout:
  `D:\Codex-Labs\aaemu-1.2-r208022-t124-integration-v1`
- Game assembly SHA-256:
  `9A74BA78E03C40B54A4EFA8015D4117CA1B2650D5407ADF939532E6B803E6B86`
- Unit-test assembly SHA-256:
  `BCE05BB12E4F75600464D6800FAE748731D0445C08445B7C63C410F33B26127D`

No feature, compatibility, test, or documentation path outside `ops/` changed
between the corrected T-124 source commit and this verification invocation.

## Full-suite proof

- Command: `dotnet test AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-build --verbosity normal`
- Exit code: 0
- Result: 1,957 passed, four skipped, zero failed, 1,961 total.
- Runner duration: 26.163 seconds; wall duration: 26.733 seconds.
- Intentional skips:
  - `Legacy_Darkrunner_Pve_MatchesGolden`
  - `Legacy_Darkrunner_Pvp_MatchesGolden`
  - `Legacy_Primeval_Pve_MatchesGolden`
  - `Legacy_Primeval_Pvp_MatchesGolden`
- Complete log:
  `D:\Codex-Labs\evidence\T-125\bot-intelligence-wave-verification-v1\full-suite-once.log`
- Log SHA-256:
  `977C520815C27E8F10B98B6B326B73D1A4C5634A2857325C7D6B1D4EC97E22B8`
- Receipt: `ops/evidence/aaemu12-bot-intelligence-wave-verification-v1.yaml`

## Build-loop change

`ops/BUILD-TEST-LOOP.md` now requires immediate writer tests, merged feature
and compatibility lanes, one clean build, one auditable full suite, and only
then a separately leased user-visible live smoke. Failed full gates remain
visible and corrections receive a fresh verification task.

## Runtime and next action

The registered AAEmu host, test database, ArcheAge client, running T-120
processes, and runtime lease were untouched. The next task must acquire the
runtime lease, gracefully transition T-120, deploy this exact candidate, and
physically prove all three behaviors: server-owned level-one identity creation,
autonomous Nuian signpost kill-quest completion/chaining, and transfer-road
route generation without per-action operator commands.
