# T-007 handoff

## Verdict

PASS. The deterministic evidence and log receipt translator satisfies the
`deterministic-evidence-tool-tests` version-1 gate in the focused local proof.

## Source identity

- Codex worktree: `C:\Users\jensh\.codex\worktrees\b9a4\PB-W00-control`
- Starting state: clean detached HEAD at
  `b564ea55dbc21ade081b8ae15b22e5f38f0abd14`
- Starting HEAD parent: Wave 2 base
  `e0350b3ee24352a1d96d81a60f34475fd8e56a45`
- Delivered source: the immutable commit containing this handoff; its SHA is
  reported by the completed T-007 task after commit creation.

## Changed files

- `scripts/evidence/New-EvidenceReceipt.ps1`
- `scripts/evidence/README.md`
- `scripts/evidence/.gitignore`
- `scripts/evidence/tests/Test-EvidenceReceipt.ps1`
- `scripts/evidence/tests/fixtures/metadata.valid.json`
- `scripts/evidence/tests/fixtures/evidence.pass.json`
- `scripts/evidence/tests/fixtures/evidence.pass.lines`
- `scripts/evidence/tests/fixtures/evidence.fail.json`
- `scripts/evidence/tests/fixtures/evidence.incomplete.json`
- `scripts/evidence/tests/fixtures/evidence.malformed.json`
- `ops/tasks/T-007/HANDOFF.md`

No product source, existing evidence receipt, global ledger, workspace registry,
runtime lease, database, deployed host, client file, or raw evidence was changed.

## Proof

- PowerShell AST parsing: PASS for the translator and focused test harness.
- `pwsh -NoLogo -NoProfile -File scripts/evidence/tests/Test-EvidenceReceipt.ps1 -ArtifactsDirectory scripts/evidence/.test-runs/proof-003`: PASS, 53 assertions across 18 retained translator attempts.
- Stable receipt SHA-256 from two identical translations:
  `de20661aceba0a85839b70a53f37faa428774139482e78784989408c252b5c52`.
- Covered JSON and versioned line inputs, ordinal ordering, byte-identical output,
  source SHA retention and preservation, atomic overwrite refusal, each of the
  seven required fingerprint fields, missing and malformed material, and
  PASS/FAIL/INCOMPLETE fail-closed verdicts.
- `git diff --check`: PASS.
- Credential-pattern scan of committed fixture and tool candidates: no matches.

## Retained failures

- `scripts/evidence/.test-runs/proof-001.console.log` retains the first red proof.
  PowerShell 7.6 parsed the supplied ISO timestamp as a `DateTime`; the strict
  translator correctly rejected that non-string value. Parsing was changed to
  preserve JSON date strings; the subsequent retained proofs passed.
- `scripts/evidence/.test-runs/proof-001/debug-valid.console.log` retains the
  focused reproduction of the same failure. Neither failed attempt produced a
  receipt.
- An exploratory `rg` command used Unix-style path globs that Windows rejected;
  the corrected `rg -g '*.ps1'` inspection succeeded. It made no file changes.
- Test-run material is intentionally retained under the in-scope ignored
  `.test-runs/` directory and is not committed as mutable test evidence.

## Runtime state

No runtime lease was claimed. AAEmu, the client, and all databases were neither
started nor changed. The runtime state recorded by the Control Tower remains
unchanged.

## Unproven boundaries

- No AAEmu gate, live runtime, client, database, resource budget, or physical
  acceptance was run; all are contract non-goals.
- The focused proof used PowerShell 7.6.4 on Windows. Other PowerShell versions
  and operating systems were not qualified.
- Integration-branch cherry-pick and post-integration rerun remain the
  Integrator's responsibility.

## Exact integration action

From a clean `integration/aaemu12-world` worktree, resolve the completed source
worktree HEAD, verify it equals the immutable SHA in the T-007 task report, then
cherry-pick that one commit and rerun the focused proof:

```powershell
$t007Commit = git -C 'C:\Users\jensh\.codex\worktrees\b9a4\PB-W00-control' rev-parse HEAD
git cherry-pick $t007Commit
pwsh -NoLogo -NoProfile -File scripts/evidence/tests/Test-EvidenceReceipt.ps1
```
