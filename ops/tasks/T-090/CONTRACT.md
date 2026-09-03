# T-090 contract: portable continuous client-process observer

Create a repository-owned observer at
`scripts/autonomy/Observe-ClientProcessAbsence.ps1` and deterministic tests at
`scripts/autonomy/tests/Test-ClientProcessObserver.ps1`. Retain the exact T-088
finding: its task-local Windows PowerShell child wrote one raw snapshot and
then exited before its first derived row because `Get-FileHash` was unavailable.

The observer must run under both `powershell.exe -NoProfile` and `pwsh
-NoProfile`; use only APIs available in Windows PowerShell 5.1 and modern pwsh.
Do not call or depend on `Get-FileHash`. Compute SHA-256 with
`System.Security.Cryptography.SHA256`, using exact file bytes and lower-case
hex. Accept an explicit fresh output directory, cooperative stop-sentinel path,
bounded sample interval, and explicit process-name set with safe client-only
defaults. Refuse ambiguous/existing outputs without overwriting.

For each monotonically numbered sample, atomically retain one raw process
snapshot and append one derived ledger row containing UTC timestamp, sequence,
process count and identities, raw length/SHA-256, previous-row hash, and current
row hash. Publish a ready marker only after the first raw file and its matching
derived row are durably written. Flush each row. On cooperative sentinel, write
a final summary with sample count, first/last timestamps, maximum adjacent gap,
nonzero sample count, error count, and terminal chain hash, then exit zero.
Unexpected query/hash/write failures must retain a concise error record and exit
nonzero without claiming readiness or success. Never stop or mutate observed
processes.

Tests must use fresh isolated directories without deleting prior artifacts and
cover at least: zero-match sampling, detectable current-shell process sampling,
raw hash and ledger-chain recomputation, ready ordering, multiple samples,
cooperative stop, gap summary, existing-output refusal, invalid interval/name,
and no `Get-FileHash` dependency. Run the complete test script under both
Windows PowerShell and pwsh. Do not run AAEmu, the ArcheAge client, or the full
unit suite. Commit only the two scripts and concise T-090 handoff; PB-000 will
dispatch independent integration before another runtime proof.
