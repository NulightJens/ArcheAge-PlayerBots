# T-087 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`. The exact T-086
candidate was independently reviewed, replayed byte-identically onto the
committed T-087 lease lineage, installed in the registered AAEmu 1.2 checkout,
and qualified without starting or controlling a runtime.

## Source identity

- Preparation/binding: `f5df7623d33587a8b0bc4737733e8cec8b00b2cf` /
  `e80873f92e0ce93dd0604e80062d14cadcac1b45`.
- Candidate/parent/tree: `5c5d61c1f05c9419ba38e1cd247d35b6e8e92439` /
  `0da7166627f53118184687736132a88cdb668e1e` /
  `bba5acc13efc2ad2c7224c816969a61a27efb01d`.
- Byte-identical replay and installed source/tree:
  `e284c739ae168fc95fb77a91bf0f23bd5dd2f6a4` /
  `4b3f96aaedb96ec40c1dc5eef4256efe02cd99f2`.
- Patch SHA-256: `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`;
  both manifest declarations match. Stable patch ID:
  `51cf303462d170dd61551ee2f4d410ac0e293ce8`.

## Independent result

The candidate has the exact five-path scope and clean detached writer parent.
The legacy one/two-argument path never evaluates `BotManager` and preserves
its aliases, help, parsing/failure order, success bytes, terrain placement,
passivity, displacement suppression, and no-respawn behavior. The optional
third unsigned ID resolves only the exact active bot; all required invalid,
stale, nonfinite, and boundary-inconsistent states fail before NPC creation.
The code snapshots a detached transform, revalidates the same bot, preserves
world/zone/instance, emits the three audit fields, and does not mutate bot
target, activity, combat, or transform state. No product defect was found.

The installed module fast-forwarded from `2243b53dcbda7a65ab66123c4bce4864d4c743dd`
to the replay source. Strict reference apply-check, complete host reverse-check,
installer check-only before and after one normal invocation, and all final
source/host/reference integrity checks passed.

- Complete no-incremental solution build: `0` errors, `79` retained warnings.
- Exact focused selection: `28` passed, `0` skipped, `0` failed.
- Full AAEmu 1.2 suite, exactly one invocation: `1,847` passed, `4` intentional
  legacy-golden skips, `0` failed (`1,851` total).
- Sanitized receipt:
  `ops/evidence/aaemu12-passive-fixture-anchor-integration-v1.yaml`.

## Runtime state and boundaries

No Login, GameServer, MySQL, database, client, bot runtime, runtime config, or
retained runtime evidence was started, stopped, controlled, accessed, queried,
or edited. Game/Login/client process counts are zero and ports
`1234/1237/1239/1250/1280` are free. The reference remains clean/detached at
`62e3eb1d87da01194802ac886cd500134facad28`; the installed module is clean;
the host retains exactly the 28 receipted patch paths plus the expected module
and migration entries.

This PASS proves source/install/build/unit behavior only. It does not
reinterpret retained T-041 or prove physical anchor separation, three-bot
autonomy, runtime persistence, scale, soak, packaging, release readiness, or
AAEmu 3.0.

## Exact integration action

Fast-forward saved branch `integration/aaemu12-world` from binding commit
`e80873f92e0ce93dd0604e80062d14cadcac1b45` through the scoped commit containing
this receipt and handoff, without history rewrites. PB-000 may then release the
T-087 build-only lease and dispatch a fresh versioned one-zone Director proof
against source `e284c739ae168fc95fb77a91bf0f23bd5dd2f6a4` and tree
`4b3f96aaedb96ec40c1dc5eef4256efe02cd99f2`, with one isolated passive
opportunity anchored to each configured bot and a new evidence root. T-037
remains blocked until that proof passes.
