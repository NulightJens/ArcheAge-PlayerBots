# T-125 contract: corrected intelligence-wave verification

Bind verification to corrected module commit
`1a52e7b44fab76939d9409561bdfa4739f1425e6`, tree
`3f473420369dc46bcf3884c90cfc140b58d30c34`, the retained T-124 build-only
checkout, and its recorded host and patch fingerprints. Do not change feature
source in this task.

Run exactly one full AAEmu 1.2 unit-test suite with `--no-build` against the
corrected T-124 assemblies. Require a nonzero discovery count, zero failures,
and retain the complete command output, exit code, duration, counts, four
intentional skip identities, log SHA-256, source identity, and assembly
fingerprints. Stop and report honestly on any mismatch; do not repeat the full
suite inside T-125.

Codify the immediate-feedback sequence in `ops/BUILD-TEST-LOOP.md` and link it
from `ops/PROJECT.yaml`: writer compile and focused feature tests, merged
compatibility/regression lanes, one clean no-incremental solution build, one
full suite, then separately leased live smoke. A failure returns to the narrow
failed lanes before a fresh verification task; it is never hidden by retries.

Do not touch the registered runtime, database, client fixture, runtime lease,
feature source, prior evidence, or AAEmu 3.0. Write the verification receipt and
handoff and commit only the declared control/evidence paths.
