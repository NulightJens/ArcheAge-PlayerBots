# T-097 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`. The exact T-096
candidate was independently reviewed, replayed blob-for-blob onto the bound
saved lineage, installed in the registered AAEmu 1.2 checkout, and qualified
without starting or controlling a runtime.

## Source identity

- Preparation/binding: `8515e189fd775e8a83853e6cca62d7f517cce48c` /
  `fade69068009a90a9f2cd7936618fbede28e8f43`.
- Candidate/sole parent/tree:
  `859e0daf64df482fcb17d83e16344387453a19b7` /
  `1ebfd0ec47138ebf15268befde9ed1671aa05ef8` /
  `593e0cf3d53fced072a665ed5de0aa6549946bf4`.
- Exact replay and installed source/tree:
  `761ffa1e0bd76d06532688f34b45e192a493b239` /
  `c36b1255cccb3a782e44e87a56bc9e867a946048`.
- Compatibility patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Game assembly SHA-256:
  `c1b3bd922c900c31c76a5748359b8363ea7c56ba34d27daed9c90fd1ddbd51ef`.

## Review, installation, and proof

The candidate has one exact parent and only the declared BotConfig source,
BotConfig tests, T-096 receipt, and T-096 handoff. Its one shared `300000` ms
ceiling is used by validation and runtime configuration conversion. The
required negative, `60000`, `180000`, `300000`, `300001`, and three-minute
runtime conversion cases all passed; `180000` is preserved end-to-end.

The saved branch and control worktree were clean at the binding commit. The
registered reference remained clean and detached at host base
`62e3eb1d87da01194802ac886cd500134facad28`. Strict forward apply-check and
complete integration-host reverse-check passed for the unchanged 28-file
patch. The installed module moved cleanly from predecessor source/tree
`e284c739ae168fc95fb77a91bf0f23bd5dd2f6a4` /
`4b3f96aaedb96ec40c1dc5eef4256efe02cd99f2` to the exact replay source/tree;
the host overlay retained its expected 30 status entries.

- BotConfig focused selection: `32` passed, `0` skipped, `0` failed.
- Director focused selection: `8` passed, `0` skipped, `0` failed.
- Clean no-incremental solution build: `0` errors, `79` retained warnings.
- Full AAEmu 1.2 suite, exactly one invocation: `1,851` passed, `4`
  intentional legacy-golden skips, `0` failed (`1,855` total).
- Sanitized receipt:
  `ops/evidence/aaemu12-activity-director-delay-range-integration-v1.yaml`.

## Runtime state and boundaries

No Login, Game, runtime observer, MySQL/database, or ArcheAge client was
started, stopped, controlled, or queried. Deployed runtime configuration and
external evidence were untouched. Game/Login/client process count and occupied
required-port count are zero; ports `1234/1237/1239/1250/1280` are free.

T-094 remains an immutable retained FAIL. This PASS proves only exact source
integration, installation, focused behavior, clean build, and the complete
unit suite. It does not prove the physical 180-second admission delay,
three-bot autonomy, restart persistence, scale, soak, packaging, release
readiness, or AAEmu 3.0. T-037 remains blocked.

## Exact integration action

Fast-forward saved branch `integration/aaemu12-world` from binding commit
`fade69068009a90a9f2cd7936618fbede28e8f43` through the scoped commit containing
this receipt and handoff, without rewriting history. PB-000 may then release or
reassign the build-only lease and dispatch a fresh versioned runtime successor
against source `761ffa1e0bd76d06532688f34b45e192a493b239` and tree
`c36b1255cccb3a782e44e87a56bc9e867a946048`. That successor must use a new
evidence root and prove the configured 180-second first admission; never reuse
or overwrite the retained T-094 evidence root.
