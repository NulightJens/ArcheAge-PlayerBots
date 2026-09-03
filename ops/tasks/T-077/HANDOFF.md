# T-077 handoff

T-077 passes its deterministic observer/parser gate. The repository now has a
strict-mode-safe, offline-safe observer for one declared bot identity. It
preserves HTTP entity bytes separately from transport metadata and derived
samples, refuses existing output paths, and exposes only the fixed read-only
`botdebug` route.

## Source identity and changed files

- Writer source base: `6b77e8e3c92c4c293bfb89454ac23f50ffe42ce1`
  (`Dispatch T-077 observer correction`).
- Control binding commit: `d72fe742513d9e5f01b4f9fb6bddd2bd9b4b9cb7`.
  It bound this exact task/thread/worktree and was intentionally not merged into
  the writer history.
- Added `scripts/autonomy/AutonomyObserver.psd1`,
  `scripts/autonomy/AutonomyObserver.psm1`,
  `scripts/autonomy/Observe-AutonomyBot.ps1`,
  `scripts/autonomy/README.md`,
  `scripts/autonomy/tests/Test-AutonomyObserver.ps1`, and
  `scripts/autonomy/.test-runs/.gitignore`.
- Added only this handoff outside `scripts/autonomy/**`.

## Proof

`pwsh -NoLogo -NoProfile -File scripts/autonomy/tests/Test-AutonomyObserver.ps1`
passed **64 deterministic assertions**. Final retained test artifacts are under
`scripts/autonomy/.test-runs/run-20260901T1229567619226Z-117460`.

The test verified the exact retained T-075 fixture before reading it and again
afterward: 165 bytes, SHA-256
`f1d865e388eca68afd064d5bbc89fcad18577e97a806c2afb9e3a77e1646bf98`.
It parsed in place as typed `offline` evidence with `online=false`, bot ID 20001,
and a null object ID. The fixture remained byte-identical and zero copies of its
bytes appeared in test artifacts.

Synthetic tests covered offline variants; online shapes with and without object
ID/host metrics; optional response fields under strict mode; invalid JSON and
field shapes; identity mismatch; HTTP transport error; exact raw-byte capture;
raw/transport/derived separation; two successful offline samples producing
immutable `armed` then `live` boundaries; exact request route/body; existing-path
refusal with retained bytes unchanged; and pre-output refusal of non-loopback or
caller-supplied command paths.

Static AST allowlists proved that production code contains only
`/commands/botdebug`, exposes only `BotId`, loopback API base, new output path,
sampling/timeout, and bounded-test parameters, exports only the parser and fixed
observer functions, and uses only the declared transport/file/parser command and
.NET type inventories. `Test-ModuleManifest` passed for module version 1.0.0;
`git diff --check` exited zero. PSScriptAnalyzer was not installed. Git emitted
only the repository's retained LF-to-CRLF working-copy warnings.

Two pre-green test artifact roots are retained under `.test-runs/` without a
summary. Both reached the existing-path test and then failed because the test
read `.Count` directly from a single filesystem object under strict mode. The
test-only assertion was corrected to force array shape; subsequent retained runs
passed 61, 62, and finally 64 assertions. No production observer failure is
retained.

## Runtime state and unproven boundaries

No AAEmu runtime, process, listener, deployed host, MySQL instance, database,
game client, lease/ledger, retained evidence, module C# source, or AAEmu 3.0
workspace was controlled or changed. Tests used only short-lived synthetic
loopback TCP responders that completed normally. No host build, AAEmu unit/full
suite, installation, live observation, gameplay, or physical acceptance is
claimed; those are outside T-077.

## Integration action

From this exact task worktree, resolve the final task `HEAD`, verify its parent is
`6b77e8e3c92c4c293bfb89454ac23f50ffe42ce1` and its diff contains only the seven
declared paths above, then cherry-pick that one commit onto the current
`integration/aaemu12-world` tip. Do not cherry-pick the separate binding commit
from the writer history. After integration, a newly leased runtime task may use
a fresh immutable evidence root and must wait for both offline boundary files
before any separately authorized staging.
