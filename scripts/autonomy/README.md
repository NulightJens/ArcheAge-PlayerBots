# Offline-safe autonomy observer

This directory contains a read-only observer for one declared PlayerBots ID.
It sends only `botdebug <BotId>` as `@system` to the loopback AAEmu Web API.
The bot ID is a required integer; neither a command name nor free-form command
arguments are accepted by the module or entry point.

The observer is intentionally source/tooling only. It does not start or stop a
server, inspect or change a database, control a game client, stage world state,
or direct gameplay. Runtime ownership and evidence-root creation remain the
responsibility of a separately leased runtime task.

## Run

Choose a new, absent output directory. The observer refuses any path that
already exists and all files inside a new run use create-new semantics.

```powershell
pwsh -NoLogo -NoProfile -File scripts/autonomy/Observe-AutonomyBot.ps1 `
  -BotId 20001 `
  -ApiBase http://127.0.0.1:1280/api `
  -OutputPath D:\Codex-Labs\evidence\T-NNN\observer
```

With the default `MaximumSamples=0`, observation continues until the caller
ends the script through its normal console cancellation path. A bounded value
is available for deterministic qualification only; a bounded invocation exits
nonzero unless both startup boundaries were written.

Wait for both immutable boundary files before any separately authorized world
staging:

1. `boundaries/armed.json` appears after the first valid offline response.
2. `boundaries/live.json` appears after a second valid offline response.

The two-sample rule proves that the parser accepted the expected absent-bot
shape and that the observer remained live. An online response never creates
either startup boundary.

## Artifact layout

- `raw/NNNNNN-botdebug.response.bin` contains the exact HTTP entity bytes.
- `transport/NNNNNN-botdebug.transport.json` records status, byte length, hash,
  and fixed endpoint metadata without embedding the response.
- `derived/NNNNNN-botdebug.sample.json` contains only normalized parser fields
  plus a path/hash reference to the raw file.
- `boundaries/armed.json` and `boundaries/live.json` are written once.

Valid absent-bot responses produce a typed sample with `classification=offline`,
`online=false`, the requested bot ID, the reported bot ID when present, and a
null object ID. Online responses support an optional object ID and optional host
metrics. Invalid JSON/fields become `malformed`; network and non-success HTTP
results become `transport-error`. Optional fields and regex groups are checked
for existence and success under `Set-StrictMode -Version Latest`.

## Deterministic qualification

```powershell
pwsh -NoLogo -NoProfile -File scripts/autonomy/tests/Test-AutonomyObserver.ps1
```

The test verifies the pinned 165-byte T-075 response by SHA-256 before reading
it, parses it in place, and verifies the hash again afterward. It does not copy
the retained fixture. Synthetic loopback responses prove raw/derived separation,
arm and liveness boundaries, transport classification, existing-path refusal,
and the production AST/route allowlist. Versioned test artifacts remain under
`.test-runs/` and are ignored by Git.
