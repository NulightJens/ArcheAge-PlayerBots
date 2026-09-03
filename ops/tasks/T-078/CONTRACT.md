# T-078 contract

## Outcome

T-077's exact seven-path tooling commit is independently reviewed, replayed,
retested, and fast-forwarded onto `integration/aaemu12-world`. The integrated
observer is qualified for a fresh runtime proof, but no live behavior is
claimed by this task.

## Pass

- Verify saved integration identity
  `d72fe742513d9e5f01b4f9fb6bddd2bd9b4b9cb7`, candidate
  `0e48860649ca26a7148350ffdb103118c9e6aba0`, exact parent
  `6b77e8e3c92c4c293bfb89454ac23f50ffe42ce1`, clean worktrees, and exactly the
  seven declared T-077 paths.
- Review the implementation for strict-mode-safe optional-field/capture access,
  fixed loopback `/commands/botdebug` command surface, raw/transport/derived
  separation, two-sample arm/liveness boundaries, and refusal to overwrite
  existing output paths.
- Replay the seven paths without semantic changes. Independently run
  `pwsh -NoLogo -NoProfile -File scripts/autonomy/tests/Test-AutonomyObserver.ps1`
  and require all assertions green, exact retained-fixture length/hash, zero
  fixture copies, module-manifest validity, and static allowlists.
- Add only T-078's concise handoff, preserve the Control Tower history through
  the dispatch/binding descendants, and fast-forward the saved branch without
  rewriting history.

## Non-goals

Runtime/build/install/database/client/host/module-C# mutation; ledger/lease
edits; evidence retry; gameplay validation; Activity Director; scale; soak;
packaging; or AAEmu 3.0.
