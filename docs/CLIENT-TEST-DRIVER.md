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

### Stage 2: bounded real-client input (implemented)

`serve-input` is a separate loopback-only mutating API. Startup requires the allowlisted profile, exact PID, exact decimal main-window handle, and an audit path whose parent already exists. It re-verifies the PID, process name, executable path, executable SHA-256, main-window ownership, current main handle, visibility, and client rectangle before issuing a random in-memory bearer lease and again before every action. Leases last at most 30 seconds and allow at most 16 counted actions.

The endpoints are deliberately narrow:

- `POST /v1/focus` accepts an empty body and establishes verified foreground ownership.
- `POST /v1/key` accepts Enter, Escape, Tab, Space, arrows, or F1-F12 only.
- `POST /v1/click-client` accepts an in-bounds client-relative point, converts it through Windows, and rejects a point occluded by another top-level window.
- `POST /v1/type-chat-command` accepts only a 2-160 character printable-ASCII slash command. It rechecks foreground ownership between opening chat, typing, and submitting.

The bearer token exists only in the startup stream and memory; it is never accepted on the command line or written to the audit. Chat audit records contain only the command verb, character count, and SHA-256, never the raw command. There is no arbitrary text endpoint, process injection, memory editing, forced termination, or general-purpose remote-desktop surface.

Run the deterministic native-window gate with an installed launch profile:

```powershell
.\scripts\Test-ClientDriver.ps1 -LauncherProfile <profile.json>
```

That gate proves positive focus/key/click/chat delivery, four fail-closed cases, the action cap, raw-text redaction, forbidden-primitive scanning, and graceful fixture close. The real-client smoke waits for one stable titled ArcheAge handle, performs only focus plus Escape, and requests verified graceful close:

```powershell
.\scripts\Test-RealClientInput.ps1 -LauncherProfile <profile.json> -EvidenceDirectory <evidence-directory>
```

### Stage 3: verified capture and image assertions (implemented)

`capture-window` writes one new BMP from the exact verified foreground client area. It uses the same profile/PID/current-visible-main-window/path/SHA-256 boundary as input, rejects a background target, checks five client-area points for top-level-window occlusion, caps each dimension at 16,384 pixels and memory at 512 MiB, and refuses to overwrite an existing output. The driver and native fixture opt into per-monitor DPI awareness so client-relative input and physical capture pixels share one coordinate system.

The deterministic fixture paints a fixed three-color client pattern. `Test-ClientDriver.ps1` captures only that client area and exercises the reusable assertion command against two exact regions and one uniquely located template. It also proves that mismatches produce a structured failed verdict and exit code `3`, and that an undeclared tolerance field is rejected. `Test-RealClientInput.ps1` captures the SHA-pinned real client after verified focus, checks file size/hash/identity/dimensions, then continues with Escape and graceful close.

```powershell
dotnet run --project tools\AAEmu.ClientDriver -- capture-window --profile <profile.json> --process-id <pid> --window-handle <handle> --output <capture.bmp>
dotnet run --project tools\AAEmu.ClientDriver -- assert-image --capture <capture.bmp> --spec <image-assertions.json>
```

An assertion spec uses this strict, versioned shape:

```json
{
  "schemaVersion": 1,
  "regionAssertions": [
    {
      "name": "character-screen-anchor",
      "rectangle": { "x": 40, "y": 40, "width": 32, "height": 32 },
      "expectedRgbSha256": "<SHA-256 of top-down row-major RGB bytes>"
    }
  ],
  "templateAssertions": [
    {
      "name": "enter-world-button",
      "templatePath": "templates/enter-world.bmp",
      "searchRectangle": { "x": 900, "y": 700, "width": 800, "height": 300 },
      "expectedMatches": [{ "x": 1420, "y": 850 }]
    }
  ]
}
```

Template paths may be absolute or relative to the spec. Inputs are bounded uncompressed 24-bit or 32-bit BMP files. Regions hash canonical top-down RGB bytes; template matches are exact RGB matches and expected coordinates are client-capture coordinates. Specs contain 1–64 uniquely named assertions, searches are capped at 100,000,000 pixel comparisons, and evidence retains at most 16 template matches. Unknown fields, out-of-bounds rectangles, unsupported BMP encodings, oversized inputs, and ambiguous excessive matches fail closed. A completed comparison emits capture/spec/template hashes, dimensions, every expected and actual result, `ocrUsed: false`, and an explicit verdict.

Exact image assertions are appropriate only for stable visual anchors. OCR should be introduced only where text is itself the acceptance subject. Retain Computer Use for exploratory calibration, animation/quality review, and unexpected states.

### Stage 4: character selection to authoritative world (implemented)

`Test-RealClientWorld.ps1` attaches to one already authenticated client at character selection. It does not receive or persist credentials. The caller supplies the exact profile, PID, decimal main-window handle, expected server character, and retained host root. The runner requires a clean canonical module identity by default, records the retained host and embedded-module Git identities, and validates the profile's pinned client path and SHA-256.

The scenario first requires the named character to be uniquely present and offline in the loopback AAEmu Web API. A 30-second, three-action input lease focuses the exact window, captures the character-selection frame, clicks the calibrated character slot, and clicks **Start Game**. It then requires all of the following within a bounded wait:

- the same log session advances in bytes and last-write time;
- character-server authorization is already present in the same log session;
- `worldLoading` and `worldLoaded` differ from their pre-action values;
- the driver derives `world_loaded` for the exact PID and handle;
- the exact named character changes from offline to online in server truth.

After the transition, the runner records `/botmetrics snapshot`, focuses the exact gameplay window with a separate one-action lease, and captures a hash-verified gameplay frame. The immutable JSON summary includes Git/profile/client identity, lifecycle snapshots, character identity, Web API truth, input audits, capture hashes/dimensions, Computer Use counts, and explicit safety declarations. A failure also writes a new retained summary and never closes or force-terminates the client.

The default physical coordinates target the calibrated `1920x1080` ArcheAge 3.0 character-selection layout. Supply different expected dimensions and coordinates only after a new visual calibration:

```powershell
.\scripts\Test-RealClientWorld.ps1 `
  -LauncherProfile <profile.json> `
  -EvidenceDirectory <new-evidence-directory> `
  -ProcessId <exact-pid> `
  -WindowHandle <exact-decimal-handle> `
  -ExpectedCharacterName <native-character> `
  -HostRoot <retained-aaemu-host>
```

Native fixture creation remains an exploratory setup step because the guarded driver intentionally has no arbitrary-text endpoint. If Computer Use created that fixture, pass `-FixtureCreationComputerUseActions` so the summary distinguishes those setup actions from the repeatable scenario's `scenarioComputerUseActions: 0`. Animated or translucent production screens must not be frozen into brittle exact-region hashes; use the Stage 3 matcher only for demonstrably stable anchors.

## Intended testing split

- Unit and adapter tests: no client.
- Routine client regressions: client driver plus AAEmu Web API.
- Visual smoke and newly discovered flows: Computer Use, later converted into driver scenarios when stable.
- Protocol diagnostics: narrow purpose-built probes only when server and client logs cannot isolate a fault.
