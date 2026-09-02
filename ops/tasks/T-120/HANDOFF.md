# T-120 handoff

Verdict: `BLOCKED-no-existing-eligible-level-one-nuian`. The exact T-119
candidate is integrated, installed, built, enabled, and running successfully,
but the contracted physical quest-intake demonstration cannot start because
the leased public-alpha dataset contains no eligible level-one Nuian identity.
No bot was admitted and no quest state was fabricated.

## Source, install, and build

- T-119 candidate `2d1b8d7103d35c46ec1bc9f13df36e40f4ba2b15`
  was replayed as `fb0a315c9579fb2e7e249e47b37efa07a9404c51`.
  Both have stable patch ID
  `d525d7bc14e72ac0c5042bc6443044762e253084`; the replay tree is
  `a7dfc4d1bffcdddc25c2dc45fc9feed71365eb70`.
- The registered host module is clean/detached at the replay commit. The
  AAEmu 1.2 reference remains clean/detached at
  `62e3eb1d87da01194802ac886cd500134facad28`. Compatibility patch SHA-256 is
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Direct TUnit proof passed 98/98 controller/host/command tests and 32/32 safe
  config executions. The destructive-cleanup config test was not invoked under
  machine policy. The first dotnet-test-style invocation is retained as a
  tooling mismatch: it compiled, then TUnit rejected `--filter`/`--logger` and
  exited 5 without running tests. It is not treated as source proof.
- The registered AAEmu.Game rebuild completed with 30 warnings, zero errors in
  5.39 seconds. Assembly SHA-256 is
  `1db5e555c8228bd9da131b21a45ed58fbe12dec7574bd53374c9343c0f2d7d04`.

## Configuration and live runtime

- Exact prior deployed config bytes are retained at
  `D:\Codex-Labs\sessions\T-120\quest-intake-live-v1\config\BotConfig.pre-t120.bin`
  (1772 bytes, SHA-256
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`).
- The deployed config enables only quest intake with scan radius 60 m,
  interaction radius 6 m, and retry backoff 30000 ms. Activity Director is
  disabled with no Director identities. Deployed config SHA-256 is
  `dac891aa41a8e691973208ecfb79f8a996db42c052f46175998725dd53221ca6`.
- The healthy interactive session remains running: Login PID `111900`, Game
  PID `38844`, launcher PID `92432`, and client PID `117252`. Required
  listeners 1234/1237 belong to Login and 1239/1250/1280 belong to Game. The
  Game API is responsive, Game `Error.log` is absent, and no startup
  `ERROR`/`FATAL` marker was found.
- Live metrics at `2026-09-02T08:43:40.9966162Z` reported runtime count 0,
  active bots 0, spawn count 0, and spawn failures 0. The Director runtime
  snapshot reports `enabled=false`, `reason=disabled`.

## Exact physical blocker

Computer Use selected only the registered ArcheAge window and observed the
client in-world as level-50 `Over`, with the zone label `Desiree Peak`. The
loopback world API independently reports character ID 1, `Over`, level 50,
online.

Server startup logged exactly 104 character names and 104 used character IDs.
The retained donor dump accounts for the four original identities at levels
50, 51, 50, and 50. The retained public-alpha seed manifest accounts for all
other identities: bot IDs 20001-20100 are 100 clones of character 2, the
level-51 Nuian template. These two retained sources account for all 104 loaded
identities. Therefore no existing level-one Nuian is available to admit.

T-120 explicitly forbids character creation, database/quest-state edits, and
GM quest acceptance/progress/report/completion. It also requires reporting
this exact condition instead of substituting an ineligible character. I did
not down-level a ScaleBot, create a character, query or write the database,
admit a bot, teleport a bot, or issue any quest lifecycle command.

Consequently, client-visible bot admission, story-first selection, ordinary
walking, native story-plus-side acceptance, and botdebug/log/journal agreement
remain unproven. This is an infrastructure/data-fixture blocker, not a passing
gameplay result.

## Runtime state and exact next action

The healthy client, launcher, Login, and Game processes were intentionally
left running for the user, as the contract requires. `Over` remains in-world;
there are zero bots. Quest intake remains enabled and the original config bytes
remain retained for later exact restoration. Later cleanup must close the
client/launcher normally, stop Game then Login gracefully, restore the retained
bytes, and prove zero processes/listeners.

The next action is a separately authorized data-provisioning task: create and
retain a clean level-one Nuian bot identity in a versioned successor test
dataset without deleting/resetting the current database. Then rerun only the
blocked physical acceptance against that explicit identity under the AAEmu 1.2
runtime lease. Do not reinterpret this T-120 result as a pass.

Full machine-readable receipt:
`ops/evidence/aaemu12-autonomous-quest-intake-integration-v1.yaml`.
