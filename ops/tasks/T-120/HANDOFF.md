# T-120 handoff

Verdict: `BLOCKED-no-existing-eligible-level-one-nuian`. The exact T-119
candidate is integrated, installed, built, enabled, and running successfully,
but the contracted physical quest-intake demonstration cannot start because
the leased public-alpha dataset contains no eligible level-one Nuian identity.
Continued exploratory testing did admit the three requested level-51 ScaleBots:
all three independently discovered a nearby side-quest giver, walked normally,
and accepted through AAEmu's native quest lifecycle. This is a real partial
pass for intake only, not a level-one, story-first, or five-quest pass. No quest
state was forced or fabricated.

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
- Final live metrics at `2026-09-02T13:15:35.6168649Z` report runtime count 2,
  active bots 2, zero runtime overlaps, zero tick errors, and zero spawn
  failures. The Director runtime snapshot still reports `enabled=false`,
  `reason=disabled`.

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
not down-level a ScaleBot, create a character, query or write the database, or
issue any quest mutation command. Read-only `/botdebug` and `/botquest`
scan/status/inspect diagnostics were used. One explicit initial teleport staged
ScaleBot000 beside the user, as allowed by the contract; no later controller
movement was a teleport.

Consequently, client-visible level-one admission, live story-first selection,
native story-plus-side acceptance on one chosen giver, and level-one
botdebug/log/journal agreement remain unproven. This is still an
infrastructure/data-fixture blocker for the official verdict.

## Continued exploratory live result

The high-level diagnostic nevertheless proved a useful slice of the shipped
behavior on all three requested ScaleBots:

- `ScaleBot000` (ID 20001, level 51) selected Maude object 44869/template 3597
  and side quest 330, `A Friendly Reminder`, from 33.58 m. `/botdebug`
  coordinates advanced from `(15532.366,15349.34,131.92491)` to
  `(15556.891,15353.397,129.07507)` under normal movement. The server emitted
  `StartQuest, Quest:330` and `quest_intake_accepted`; read-only native status
  then reported `Ready / QuestComplete`. With no other eligible nearby start,
  the older one-kill lifecycle took over and the bot later logged out normally.
- `ScaleBot001` (ID 20002, level 51) received no staging or teleport. It
  selected Guard object 44953/template 8172 and side quest 6628, `Gladiator`,
  from 29.13 m, walked normally, emitted native `StartQuest`, and reached
  `Ready / QuestComplete` with intake counters `accepted=1, rejected=0`.
- `ScaleBot002` (ID 20003, level 51) independently repeated the same native
  result from 30.57 m with no staging or teleport and the same clean counters.

The authoritative server markers are in the live Game log at lines
450712/450714/450765/450770 for ScaleBot000,
454957/454959/455419/455442 for ScaleBot001, and
455827/455829/455938/455961 for ScaleBot002. The recent log slice has zero
`ERROR`/`FATAL` markers and Game `Error.log` remains absent.

This demonstrates nearby exact-NPC discovery, bounded normal travel, and
native acceptance on three independent identities. It also exposes the exact
product boundary: this controller is intake-only. It does not perform quest
objectives, travel to reporters, choose rewards, report quests, or chain the
first five Nuian quests. Every live candidate was `main_story=false`, so the
story-priority rule remains unit-tested but not physically demonstrated.

## Runtime state and exact next action

The healthy client, launcher, Login, and Game processes were intentionally
left running for the user, as the contract requires. `Over` remains in-world;
ScaleBot001 and ScaleBot002 remain active and idle after their native accepts;
ScaleBot000 logged out normally. Quest intake remains enabled and the original
config bytes remain retained for later exact restoration. Later cleanup must
close the client/launcher normally, stop Game then Login gracefully, restore
the retained bytes, and prove zero processes/listeners.

The next action is a separately authorized data-provisioning task: create and
retain a clean level-one Nuian bot identity in a versioned successor test
dataset without deleting/resetting the current database. Then rerun only the
blocked physical acceptance against that explicit identity under the AAEmu 1.2
runtime lease. A separate implementation task is required for autonomous
objective execution, reporting, reward selection, and multi-quest chaining.
Do not reinterpret this T-120 result as a full pass.

Full machine-readable receipt:
`ops/evidence/aaemu12-autonomous-quest-intake-integration-v1.yaml`.
