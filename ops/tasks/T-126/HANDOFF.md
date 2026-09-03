# T-126 handoff: outcome-driven delivery reset

## Outcome

PASS. The last 24-hour delivery window is inventoried, the source/deployed/live
gap is explicit, and the project now uses `ops/GOALS.yaml` plus
`ops/DELIVERY-MODEL-V2.md` as its executable product model. The historical task
ledger and every retained failure remain intact but no longer define roadmap
progress.

## Source identity and audit proof

- Control baseline: `cc128ae8fb84e6ca98642566766da43152a4f1c6` on
  `integration/aaemu12-world`.
- Audited range: `bf0ae36fc65eea4f341b936c9f7a961e9d474580..cc128ae8fb84e6ca98642566766da43152a4f1c6`.
- Result: 64 commits, 137 files, 19,188 insertions, 61 deletions, and 22
  registered worktrees.
- Product-touching commits: 7; governance/evidence-only commits: 57.
- YAML parse: PASS for PROJECT, CURRENT, BOARD, GOALS, and T-126 TASK through
  the retained YamlDotNet 16.3.0 parser.
- `git diff --check`: PASS; only expected Windows LF/CRLF conversion warnings.
- Runtime wiring inspection: the road session appears in source, tests, and
  documentation but has no bot-host owner. Identity is command-callable and
  quest lifecycle is host-wired.

## Changed files

- `AGENTS.md`
- `docs/ROADMAP.md`
- `ops/AUDIT-2026-09-02.md`
- `ops/BOARD.yaml`
- `ops/CURRENT.yaml`
- `ops/DELIVERY-MODEL-V2.md`
- `ops/GOALS.yaml`
- `ops/PROJECT.yaml`
- `ops/ROADMAP.md`
- `ops/tasks/T-126/TASK.yaml`
- `ops/tasks/T-126/CONTRACT.md`
- `ops/tasks/T-126/HANDOFF.md`

## Runtime and retained boundaries

The retained T-120 AAEmu server, database, client, processes, config, installed
module, and runtime lease were not changed. Integrated candidate
`1a52e7b44fab76939d9409561bdfa4739f1425e6` remains uninstalled; deployed
source remains `fb0a315c9579fb2e7e249e47b37efa07a9404c51`. No live feature
acceptance is claimed by this task.

## Exact next action

Run G-001 as one vertical slice under a successor runtime contract: gracefully
transition T-120, install exact corrected source plus reviewed host adapters and
dedicated bot-account configuration, start the real server and active sentinel
client, create one level-one Nuian bot at the observed location, and let it
attempt the first five starter quests without per-action commands. Fix the
first behavioral blocker in the same slice, redeploy immediately, and repeat.
Do not activate three-bot population or road-travel goals until G-001 is
accepted.
