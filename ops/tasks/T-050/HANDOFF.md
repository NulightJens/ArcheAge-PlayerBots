# T-050 handoff

T-050 is **INCOMPLETE**. The fresh v2 startup reached Login and Game, but the
authorized startup wrapper failed closed before writing its startup receipt or
capturing initial zero-bot metrics. No combat or stealth acceptance is claimed.

## Source identity

- Final lease ancestor: `d87e7d722cef8578509a24b048f2e10192ddb9c1`.
- Task/thread: `T-050` / `01a05a8e-3f30-7c81-996b-3eab7fe2e934`.
- Integration ancestor: `da570e3cc3936917c097ddd2302525f56125c916`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `78e75fc8995d871de56f761cc45fe4fd71b2ae3e`,
  clean tree `92d100838dd4b963edc25245729ea29759deb05a`.
- Active patch SHA-256:
  `5b44353fef730367b6dfe29baaa0c5141374163f6616549b6724cead91d82ac3`.

## Changed files

- `ops/evidence/aaemu12-t050-combat-stealth-v2.yaml`
- `ops/tasks/T-050/HANDOFF.md`

## Proof and retained failure

Preflight proved the final T-050 lease identity, clean reference and installed
module, exact host/module/patch identities, immutable v1 config hashes, exact
isolated schemas on loopback, retained IDs 20001-20100, no AAEmu process, free
ports 1234/1237/1239/1250/1280, empty runtime log locations, and an absent v2
path before creation.

`Start-ScaleGateRuntime.ps1 -SafetyAcknowledged` started Login PID 116256 and
Game PID 103600. Login emitted the exact selected schema and its loopback
startup markers. Game emitted its server, network, stream, and loopback Web API
ready markers, but the wrapper's strict predicate then failed while converting
the growing Game log value to its string `Content` parameter. No startup receipt,
exact selected Game-schema proof, or initial zero-bot metrics were retained.
The wrapper gracefully stopped Game and Login through Ctrl+C. Its Game cleanup
line proves zero bots and runtimes; no force termination occurred.

All generated logs were moved intact to the new literal v2 evidence root. The
Game error log also retains two collection-modification errors from
`BaseBaiLoader` and two `WorldManager` missing-instance fatal entries. The
fail-closed combat analyzer returned `INCOMPLETE` with native exit code 2. No
retained bot ID, passive NPC, target, buff, qualification command, cohort,
stealth phase, or restart was used. T-036 v1 hashes remain unchanged.

## Final runtime state and boundaries

Game and Login are stopped. All five required ports are free, both runtime log
locations are empty, the installed module and reference remain clean, and the
isolated databases/configs/Data assets are retained. Schema access occurred only
through normal AAEmu startup. There was no direct database query or mutation,
client or AAEmu 3.0 control, source/global-ledger edit, destructive cleanup, or
retry of this immutable v2 attempt.

## Exact integration and control actions

1. Integrator: integrate this task commit as an `INCOMPLETE` runtime receipt
   only. Do not mark T-050 passed and do not activate T-037.
2. PB-000: after the receipt is committed on `integration/aaemu12-world`, release
   `aaemu12` using its zero-bot, graceful-stop, zero-process, and free-listener
   cleanup proof.
3. PB-000: keep T-037 blocked. Dispatch a bounded correction for the startup
   wrapper's Game-log content conversion and review the retained Game runtime
   errors, then dispatch a new immutable physical attempt in a fresh versioned
   evidence directory. Never reuse or overwrite `public-alpha-v2`.
