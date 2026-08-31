# Client test driver

## Decision

Use a layered local test driver for routine ArcheAge client acceptance. Keep Computer Use for discovery, visual review, and recovery when a screen has not yet been modeled. Do not build a second headless ArcheAge protocol client as the primary test system.

The real client remains the system under test. The driver should combine narrow programmatic controls with server-side setup and telemetry:

1. The existing AAEmu Web API creates fixtures, issues bounded bot commands, and captures authoritative server state.
2. `AAEmu.ClientDriver` launches and observes the real client, sends only allowlisted input to its verified window, and captures client evidence.
3. Computer Use explores new or changed UI flows and validates presentation that cannot be established from logs, packets, or screenshots alone.

This follows OpenAI's supported computer-use pattern: a custom harness may mix visual and programmatic interaction instead of requiring every action to be driven through screenshots and free-form clicks. See the [OpenAI Computer use guide](https://developers.openai.com/api/docs/guides/tools-computer-use).

## Why not a protocol bot

A new client-protocol implementation would have to duplicate version-specific login tickets, encryption, packet sequencing, world state, and movement rules. It would be expensive to keep faithful across AAEmu 1.2 and 3.0, and it would not validate the actual launcher, client rendering, input handling, or UI. Protocol-level probes can still be added for narrow diagnostics, but they are not a replacement for real-client acceptance.

## Delivery stages

### Stage 0: deterministic observation (implemented)

The first slice is read-only:

- `status` emits one JSON snapshot of the `archeage` process, main-window handle/title/rectangle, log metadata, lifecycle state, and milestone times.
- `serve` exposes `GET /health` and `GET /v1/status` on `127.0.0.1` only.
- Lifecycle states come from stable client log events: login connection, game connection, world connection/authorization, loading start/completion, disconnect, and quit.
- The API never returns raw log lines, launcher credentials, or command-line secrets.

Run it with:

```powershell
dotnet run --project tools\AAEmu.ClientDriver -- status --log "$env:USERPROFILE\Documents\AAClassic\ArcheAge.log"
dotnet run --project tools\AAEmu.ClientDriver -- serve --log "$env:USERPROFILE\Documents\AAClassic\ArcheAge.log"
```

Validate the fixture parser and loopback API with:

```powershell
.\scripts\Test-ClientDriver.ps1
```

### Stage 1: allowlisted launch and graceful close (implemented)

The driver wraps the installed `AAEmu.Common.Launcher.dll` rather than clicking through the launcher. A JSON profile pins the launcher assembly and client executable by exact SHA-256, permits only a loopback endpoint, and contains no username or password. `verify-profile` validates that boundary; `probe-launcher` exercises launcher ticket initialization with synthetic probe values without starting a process; `launch` reads a username and password from an interactive console or two redirected standard-input lines and waits for a lifecycle marker from the new log session.

The stock launcher's `settings.aelcf` format stores `lastLoginPass` as plaintext. The driver deliberately does not read that file. Passwords are rejected in command-line options and profile fields and are not returned in JSON. For unattended tests, provide a disposable test credential through redirected standard input; a future secret-store adapter should use Windows Credential Manager rather than the stock settings file.

`request-close` requires the exact launch profile and PID, re-verifies the process name, executable path, and SHA-256, then posts the normal Windows close message to the verified main window. It never calls `Kill`, `Stop-Process`, or another forced-termination path. If the client does not exit before the timeout, the driver reports `close_requested` and leaves it running.

```powershell
dotnet run --project tools\AAEmu.ClientDriver -- verify-profile --profile <profile.json>
dotnet run --project tools\AAEmu.ClientDriver -- probe-launcher --profile <profile.json>
$credentialLines | dotnet run --project tools\AAEmu.ClientDriver -- launch --profile <profile.json> --wait-for login_connected
dotnet run --project tools\AAEmu.ClientDriver -- request-close --profile <profile.json> --process-id <pid>
```

### Stage 2: bounded real-client input

Add semantic operations such as `focus`, `key`, `click-client`, and `type-chat-command` using Win32 input APIs. Every mutating call must require a current process ID and window handle, verify the executable path and foreground window, transform client-relative coordinates through the captured rectangle, use an expiring input lease, and record the action without recording typed secrets. No process injection, memory editing, or general-purpose remote desktop endpoint is permitted.

### Stage 3: visual evidence

Capture the verified client window directly. Prefer exact pixel/template assertions for stable widgets and OCR only where text is the acceptance subject. Retain Computer Use for exploratory calibration, animation/quality review, and unexpected states.

### Stage 4: scenario runner

Compose client operations with AAEmu Web API commands and explicit assertions. Each scenario should emit source/build identity, client/server version, fixture IDs, timestamps, server truth, client lifecycle, screenshots, and a pass/fail reason. Replays must fail closed when the expected process, window, log generation, world, or character is ambiguous.

## Intended testing split

- Unit and adapter tests: no client.
- Routine client regressions: client driver plus AAEmu Web API.
- Visual smoke and newly discovered flows: Computer Use, later converted into driver scenarios when stable.
- Protocol diagnostics: narrow purpose-built probes only when server and client logs cannot isolate a fault.
