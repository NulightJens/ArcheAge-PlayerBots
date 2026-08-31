# Deterministic combat and stealth qualification

This directory contains the offline T-044 gate used to prepare, validate, and
summarize the later T-036 physical AAEmu 1.2 run. These scripts do not start or
stop AAEmu, acquire a runtime lease, create or modify a database, deploy a
module, or control a client.

The gate is deliberately fail-closed. PASS requires all 1/5/10/25/50/100
combat cohorts, matched Idle controls using the same retained IDs, supplied
target object/template IDs, a verified supplied stealth buff, bounded
timeouts, byte-exact log segments, resource samples, per-bot health evidence,
cleanup, graceful shutdown, and a clean restart. Missing or ambiguous material
is INCOMPLETE. A complete observation that violates a behavioral expectation
is FAIL.

## Offline proof

Run the deterministic synthetic fixtures from the module source checkout:

```powershell
pwsh -NoProfile -File .\scripts\combat\Test-CombatQualificationHarness.ps1
```

The fixtures cover the complete pass case, mortal kill failure, matched Idle
activity, stealth-loss evidence, in-radius reacquisition, bounded timeout/radius
release, stale log offsets, cleanup gaps, restart gaps, and malformed partial
evidence. They do not sleep and make no network, process, client, or database
calls.

## T-036 physical workflow

1. Copy `qualification-input.template.json` to a retained evidence directory
   outside Git. Fill every placeholder from the claimed AAEmu 1.2 runtime:
   exactly 100 unique character IDs; six distinct mortal passive-NPC object
   targets; two distinct stealth targets; the verified stealth buff ID; the
   isolated database receipt; and exact source/build hashes. The generator
   rejects the committed placeholder values.

2. Generate the immutable plan and a fail-closed evidence skeleton. Neither
   command overwrites an earlier artifact:

```powershell
$module = 'D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\modules\archeage-playerbots'
$run = 'D:\Codex-Labs\evidence\T-036\REPLACE-RUN-ID'
pwsh -NoProfile -File "$module\scripts\combat\New-CombatQualificationPlan.ps1" `
  -InputPath "$run\qualification-input.json" `
  -OutputPath "$run\plan.json"
pwsh -NoProfile -File "$module\scripts\combat\New-CombatQualificationEvidenceTemplate.ps1" `
  -PlanPath "$run\plan.json" `
  -OutputPath "$run\evidence.json"
```

3. For each plan phase, capture the current server-log byte length before the
   first stimulus and after the final observation. Record those exact offsets,
   the path of the retained log (or explicit rollover archive), and SHA-256 of
   exactly that byte range. Never infer offsets from timestamps. A hash mismatch
   is INCOMPLETE.

```powershell
$log = 'REPLACE-RETAINED-Server.log'
$startOffset = (Get-Item -LiteralPath $log).Length
# Execute only the phase stimuli from plan.json and collect responses/metrics.
$endOffset = (Get-Item -LiteralPath $log).Length
$bytes = [IO.File]::ReadAllBytes($log)
$segment = [byte[]]::new([int]($endOffset - $startOffset))
[Array]::Copy($bytes, [long]$startOffset, $segment, 0, $segment.Length)
$segmentSha256 = [Convert]::ToHexString(
  [Security.Cryptography.SHA256]::HashData($segment)).ToLowerInvariant()
```

4. Execute each `stimuli` entry exactly in plan order through the loopback
   command API. Record the HTTP outcome, response messages, and errors in the
   corresponding evidence entry. The generated plan expands every bot ID; it
   never uses an implicit `all` selector.

```powershell
$api = 'http://127.0.0.1:1280'
$actor = '@system'
$stimulus = $plan.cohorts[0].combat.stimuli[0] # advance in exact plan order
$body = ConvertTo-Json -Compress @{
  character = $actor
  arguments = $stimulus.arguments
}
$response = Invoke-RestMethod -Method Post `
  -Uri "$api/api/commands/$($stimulus.command)" `
  -ContentType 'application/json' -Body $body -TimeoutSec 30
```

   Capture `/botmetrics snapshot` at each phase boundary. Record the complete
   returned `T021_METRICS` document as `metricsStart`/`metricsEnd`, at least two
   timestamped process CPU/working-set/private-memory samples, and one
   `/botdebug <id>` health result for every cohort bot. Stealth phases must also
   retain `/botdebug` search samples as structured `active`, `elapsedSeconds`,
   and `radiusMeters` values. The supplied target is stealthed and unstealthed
   with the generated `/botbuffnpc <attackerBotId> <npcObjId> <buffId|-buffId>`
   stimuli.

5. After every Idle or combat phase, execute the generated per-ID `removebot`
   cleanup commands and prove zero bots/runtimes. Combat and stealth targets
   must be dead. Do not reuse a dead target object in another phase.

6. After the final zero-population cleanup, use the runtime lease's graceful
   shutdown path. Record the exact log segment containing
   `BOT ev=shutdown_cleanup remaining_bots=0 remaining_runtimes=0`, prove the
   prior PID exited, start the same build through the authorized T-036 runtime
   procedure, and record a distinct PID/start time, startup log segment, and a
   zero-population metrics snapshot. T-044 provides no start/stop command.

7. Analyze the retained material. The result contains derived summaries and
   hashes only; raw physical logs stay outside Git.

```powershell
pwsh -NoProfile -File "$module\scripts\combat\Test-CombatQualificationEvidence.ps1" `
  -PlanPath "$run\plan.json" `
  -EvidencePath "$run\evidence.json" `
  -OutputPath "$run\result.json"
```

Exit code 0 is PASS, 1 is a complete measured FAIL, and 2 is INCOMPLETE.

## Authoritative event requirements

Combat requires per-bot `Idle -> Combat` and return-to-`Idle` transitions,
positive cast deltas, and a `kill_credit` for the exact supplied target. Idle
controls require zero casts, kills, searches, and recoveries. Stealth
reacquisition requires exact-target loss, `Combat -> Searching`, bounded debug
samples, exact-target `target_found`, and `Searching -> Combat`. Release
requires the same loss/search evidence, no target-found event, bounded samples,
`search_give_up`, and `Searching -> Idle`. Every phase is also tied to exact
population, health, resource, cleanup, and byte-offset evidence.
