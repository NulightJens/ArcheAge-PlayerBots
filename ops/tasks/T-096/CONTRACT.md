# T-096 contract: widen Activity Director initial-delay range

Start from the exact committed binding and accepted module source
`e284c739ae168fc95fb77a91bf0f23bd5dd2f6a4`. The T-094 retained evidence proves
that deployed `ActivityDirectorInitialDelayMs=180000` was clamped by
`BotConfig.Validate()` to 60000, producing first admission after `60020.997` ms.

Make the smallest upstream-faithful source change that preserves 180000 through
both validation and runtime configuration while retaining a bounded upper
limit. Use a five-minute (`300000` ms) supported ceiling unless direct source
evidence requires a smaller non-blocking design. Remove duplicated magic limits
where practical without broad refactoring. Update focused tests to prove at
least: negative clamps to zero; 60000 remains unchanged; 180000 remains
unchanged; exact upper bound remains unchanged; above-bound clamps to the upper
bound; `ToRuntimeConfiguration()` carries 180000 into its `TimeSpan`.

Run the focused BotConfig/Director tests and a clean AAEmu 1.2 build through the
registered read-only reference or a temporary non-deployed proof workspace as
allowed by the contingency policy. Regenerate
`compatibility/aaemu-1.2-r208022-v3.patch`; require it applies cleanly to host
base `62e3eb1d87da01194802ac886cd500134facad28`, record its SHA-256, and ensure it
contains the source/test correction without unrelated changes. Do not run the
full suite; the later Integrator owns the once-per-wave full-suite gate.

Commit only the declared write scope and produce a concise handoff with exact
base/head, changed files, focused counts, build warnings/errors, patch hash and
apply proof, retained failures, and exact integration/install action. Never
touch the deployed/reference host as a writer, installed module, runtime,
database, client, global ledgers/lease, T-094 evidence, or AAEmu 3.0.
