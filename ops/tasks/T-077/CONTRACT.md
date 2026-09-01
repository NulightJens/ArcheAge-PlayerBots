# T-077 contract

## Outcome

The repository has a reusable, deterministic autonomy observer and parser that
can be armed before `addbot`, preserves original response bytes, emits derived
samples separately, treats the valid offline response as evidence rather than
an error, and never exposes a gameplay-directing command surface.

## Required implementation

- Add a narrowly scoped `scripts/autonomy` module and observer entry point.
  Parsing must be strict-mode safe: every optional regex capture or response
  field is accessed through explicit existence/success checks. Offline samples
  must return a typed/structured result with `online=false`, nullable object ID,
  preserved bot identity when present, and no exception.
- The observer accepts only the fixed `botdebug` command for one declared bot
  ID. It must not accept arbitrary command names or arguments and must contain
  no `addbot`, `botstate`, target, travel, attack, fixture, progression,
  recovery, logout, removebot, database, client, or runtime-control path.
- Keep transport/raw-byte capture separate from derived parsing. Provide an arm
  boundary that can be proven after a successful offline sample and a liveness
  boundary usable before fixture staging. Never overwrite an existing output
  path.
- Add deterministic tests and concise documentation. Cover at least: the exact
  retained T-075 165-byte response/hash read-only; synthetic offline variants;
  online samples with and without optional fields; malformed/transport-error
  classification; strict-mode execution; raw/derived separation; existing-path
  refusal; and static command-surface allowlisting.

## Pass

- Verify the retained fixture SHA-256 is exactly
  `f1d865e388eca68afd064d5bbc89fcad18577e97a806c2afb9e3a77e1646bf98`
  before using it read-only. No retained evidence is copied, edited, or
  reinterpreted.
- All deterministic tests pass from a clean task worktree and leave no
  out-of-scope files. Record exact counts/commands and any retained warnings.
- Commit only `scripts/autonomy/**` and `ops/tasks/T-077/HANDOFF.md`.

## Non-goals

Runtime proof; gameplay/product behavior change; deployed-host installation or
build; module C# edits; database/client work; evidence retry; Activity Director;
scale; soak; packaging; AAEmu 3.0; or global-ledger/lease edits.
