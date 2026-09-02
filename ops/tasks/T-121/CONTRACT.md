# T-121 contract: server-owned arbitrary-level bot identities

Start from the exact committed source base in `TASK.yaml`. This task replaces
the provisioning-only clone assumption with a production bot identity factory;
it is not authorization to mutate the running T-120 dataset or host.

Implement a server-owned identity path that does not use a player's account or
client-visible character slots. Reuse AAEmu's authoritative character creation
rules through a narrow host compatibility seam: normal character-ID allocation,
name uniqueness, race/gender model and spawn selection, level validation,
inventory/equipment initialization, ability and skill initialization, empty
quest state, persistence, manager registration, and subsequent `BotManager`
admission must remain authoritative. Do not reproduce creation rules with raw
SQL and do not require a fake `GameConnection` or emit client packets.

Expose a bounded GM command equivalent to
`/createbot <name> <race> <gender> <archetype> <level> [here|race-spawn]`.
Validate level one through the AAEmu server cap and reject malformed, duplicate,
or unsupported input without a partial identity. An explicit `here` placement
uses the caller's finite current world/instance/position; race spawn uses the
native template. A requested archetype at level one is a progression plan, not
permission to grant skills or gear above that level. Higher levels must receive
only the level-appropriate native state.

Persist successful identities under one dedicated configurable server-owned bot
account, register them in the authoritative bot roster, and admit them through
the existing normal spawn path. Creation must be transactionally fail-closed at
the module boundary and observable through structured success/failure output.
Do not add a destructive delete command; later retirement must be reversible.

Factor any required AAEmu host change into
`compatibility/aaemu-1.2-r208022-bot-identity-factory.patch`. Never edit the
registered or reference host. Focused tests must cover level one, server cap,
invalid level, duplicate name, unavailable account/template/ID/persistence,
Nuian race spawn, caller placement, clean quest state, level-appropriate skills
and inventory, roster registration, admission, rollback/fail-closed behavior,
and unchanged loading of existing persisted bots. Run an isolated AAEmu 1.2
build with zero errors and record exact proof in `HANDOFF.md`.

