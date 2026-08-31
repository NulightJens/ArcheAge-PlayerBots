# T-007 contract

## Outcome

Deterministic test and runtime summaries can be translated into bounded, reusable evidence receipts whose fingerprint fields, source hashes, and verdict provenance are explicit and independently verifiable.

## Pass

- A non-overwriting PowerShell entry point accepts supplied metadata plus deterministic JSON or line-oriented evidence inputs and emits a stable, versioned receipt without modifying its inputs.
- Receipt fields cover every version-1 fingerprint requirement in `ops/GATES.yaml`, retain input SHA-256 values, distinguish PASS, FAIL, and INCOMPLETE, and fail closed when required evidence is missing or malformed.
- Timestamps and environment-dependent values are supplied inputs; identical inputs produce byte-identical output.
- Tests cover stable ordering, input preservation, overwrite refusal, malformed input, missing fingerprint material, and all three verdicts without starting a runtime or database.
- Secrets, raw logs, database dumps, videos, screenshots, machine-specific credentials, and mutable TestResults artifacts are never committed.

## Non-goals

- Running any AAEmu gate.
- Approving resource budgets.
- Editing product source, global ledgers, workspace registrations, runtime leases, or existing evidence receipts.
