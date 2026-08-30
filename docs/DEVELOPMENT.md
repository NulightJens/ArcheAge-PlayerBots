# Development

## Working layout

Develop with this repository cloned into a compatible AAEmu checkout:

```text
AAEmu/modules/archeage-playerbots
```

Run the installer once, commit the host patch in a local AAEmu integration branch, then edit module files normally. The conditional targets compile module code without copying it into AAEmu directories.

## Build and test

From the AAEmu root:

```powershell
dotnet build AAEmu.slnx --no-incremental
dotnet test AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-build
```

AAEmu UnitTests use Microsoft Testing Platform. For a focused test, build first, run the generated `AAEmu.UnitTests.exe --list-tests`, then use an exact discovered ID with `--filter-uid`.

## Change ownership

- Put new PlayerBots production code under `src/AAEmu.Game`.
- Put new PlayerBots tests under `tests/AAEmu.UnitTests`.
- Put tunable behavior in `Data/BotRotations` or `Data/BotArchetypes.json` when possible.
- Change the AAEmu compatibility patch only when the module truly needs a new host boundary.
- Do not copy AAEmu-owned files into the module to avoid adding a hook.

When a host-hook change is unavoidable, reproduce it in a fresh checkout of the manifest's base commit, verify the module build/tests, regenerate the named patch, and document the compatibility change in the changelog.

## Review checklist

- Does the action wait for legal skill range and facing?
- Can follow, combat, heal, death, invalid target, party changes, and logout preempt/recover correctly?
- Is any scan cached or bounded rather than repeated per bot?
- Does randomness avoid changing every tick?
- Are native ArcheAge systems used where they already provide the behavior?
- Are unit tests paired with one observable physical acceptance case?
- Are resource measurements compared with an Idle control cohort?

## Data and security

Use versioned isolated test databases. Keep account credentials, server configuration, raw player data, recordings, database snapshots, and large logs out of Git. The synthetic Web API actor is for loopback administration only.
