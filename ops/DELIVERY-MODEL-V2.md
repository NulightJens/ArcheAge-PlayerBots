# PlayerBots delivery model v2

## Governing idea

Progress is a bot doing something useful in ArcheAge on the registered server,
witnessed by the registered client, with native server state proving that the
result was real. Code, tests, commits, tasks, and receipts are inputs to that
outcome. They are not substitutes for it.

`ops/GOALS.yaml` is the short product ledger. `ops/BOARD.yaml` is retained as a
historical task index and may contain implementation records, but its task
count does not determine roadmap status.

## One active vertical slice

At most one product goal is executable at a time. It owns the code change,
focused tests, host patch, deployment, server/client run, correction loop, and
acceptance. A discovered failure stays in that slice; it does not create a
prepare/failure/replay/integrate chain of tasks.

Use no more than three durable workspaces for new work:

1. the Control Tower/integration worktree;
2. one current vertical-slice worktree when source isolation is genuinely
   needed; and
3. the registered lease-controlled AAEmu integration/runtime checkout.

Existing worktrees are retained under the no-destruction policy, but no longer
justify parallel roadmap claims. New parallel source tasks require a concrete
independent outcome and must not delay the active vertical slice.

## State ladder

Every capability has exactly one current state:

1. `specified` — player behavior and markers are defined;
2. `source-green` — focused tests and compile pass;
3. `integrated` — merged source and host seams pass regression gates;
4. `deployed` — that exact source, patch set, config, and assembly are installed;
5. `server-proven` — native game state records the expected result;
6. `client-witnessed` — an active client observes the player-facing behavior;
7. `repeatable` — the scenario repeats without per-action control and survives
   any required graceful restart;
8. `accepted` — every contracted marker passes on the same candidate.

Only `accepted` advances a product goal. Reports must show integrated and
deployed fingerprints side by side. If they differ, the dashboard is red.

## Test loop

Headless proof remains necessary, but its purpose is narrow:

- unit tests prove deterministic decisions, failure boundaries, and adapters;
- focused integration tests prove AAEmu-native authority calls;
- headless server runs prove lifecycle, persistence, cleanup, resource use, and
  scale after the behavior already works;
- an active sentinel client proves visible movement, interaction, combat,
  equipment, animation, packets, and player proximity.

Every feature acceptance run uses both the real server and an active client.
The server remains the authority; the client is the visual/interaction oracle.
The server simulates the world without a client loading every area, so large
population and soak runs remain headless after a smaller client-witnessed slice
passes.

The correction cadence is short:

```text
one behavior -> focused test -> build -> deploy -> graceful restart
             -> server marker + client marker -> fix first blocker -> repeat
```

Do not accumulate a multi-feature wave before deployment. A full suite runs at
the end of an accepted slice or when shared infrastructure changes; it does not
delay the next live check after every small correction.

## Marker contract

Each scenario declares all of these before coding:

- **start marker:** exact character, level, location, config, data version, and
  installed fingerprints;
- **decision marker:** why the bot selected the goal and target;
- **motion marker:** continuous position/zone change using normal movement, not
  teleportation;
- **interaction marker:** native accept/use/attack/report action and range;
- **authority marker:** native quest, inventory, XP, health, position, or
  persistence delta;
- **client marker:** visible bot/action/result captured at meaningful state
  transitions;
- **recovery marker:** bounded retry or explicit safe suspension;
- **end marker:** completed outcome, retained identity, errors/overlaps, and
  graceful runtime state.

Screenshots or video alone do not prove native progress. Logs or database state
alone do not prove a player-visible bot. Both sides must agree.

## Test data and runtime

Use versioned, isolated scenario data. Never reset a database destructively.
Create a successor scenario account/data version and retain the previous one.
Scenario characters may be created through the server-owned identity factory
and reused when persistence is part of the test.

The runtime lease remains mandatory. The active goal owns one graceful server
transition, deployment, and client session until it passes or reaches a true
external blocker. Normal client close and graceful Game-then-Login shutdown are
required; forced process termination is prohibited.

## Definition of done

A feature is done only when:

1. focused tests and the required regression lane pass;
2. exact source, host patches, assemblies, config, and test-data version are
   recorded and installed;
3. a real server run produces every authority marker;
4. an active client witnesses every material player-facing transition;
5. the bot runs without per-action operator commands after scenario start;
6. the run repeats and any required persistence survives a distinct-PID
   graceful restart;
7. zero unexplained tick errors, runtime overlaps, stuck movement, synthetic
   quest progress, or unintended bots remain; and
8. the product-goal ledger is updated from the same evidence.

Commands may create the fixture, start the scenario, and inspect state. They
may not choose each target, teleport the bot through the route, inject quest
credit, or complete the objective in place of the AI.

## Control Tower dashboard

The Control Tower reports only:

- active product goal and one player-facing sentence;
- integrated fingerprint versus deployed fingerprint;
- current state-ladder position;
- last server marker and last client marker;
- first blocking behavior;
- next code/deploy/test action; and
- runtime/client/lease state.

No status is green because a task, handoff, evidence translation, or expected
test count completed. Governance work should remain below ten percent of a
normal slice's changed lines and commits unless the user explicitly asks for a
governance audit.

## Immediate execution sequence

The first successor runtime task must:

1. gracefully close the retained T-120 client and stop Game then Login;
2. install exact product source `1a52e7b44fab76939d9409561bdfa4739f1425e6`
   with its required reviewed AAEmu 1.2 patches;
3. provision/use a dedicated retained server-owned bot account and set
   `AAEMU_PLAYERBOTS_ACCOUNT_ID` before server start;
4. start the server and active sentinel client;
5. create one level-one Nuian bot at the observed starter/signpost location;
6. let it attempt G-001 without per-action commands;
7. fix only the first real blocker in the same slice, redeploy, and repeat; and
8. add the second and third bots only after the one-bot five-quest slice passes.

Road routing is not part of the first live claim until its session is owned by
the runtime goal/movement loop. Route generation by itself is a diagnostic.
