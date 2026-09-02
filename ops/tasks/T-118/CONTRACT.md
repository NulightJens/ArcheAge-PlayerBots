# T-118 contract: interactive PlayerBots live demo

Use only PB-000's exact committed T-118 thread/worktree binding and AAEmu 1.2
runtime lease. Require the binding's sole parent to be the preparation commit
reported by PB-000. Do not take runtime or GUI action before that binding is
committed. This is an interactive user session, not a qualification run.

Require pinned host `62e3eb1d87da01194802ac886cd500134facad28`, clean installed
module source/tree `39f748fb3904584b50e1dabc0cfb0b3045793165` /
`7a9b2c3296bb5aee03c0016a4a7a72bb4c75073d`, compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
Game assembly SHA-256
`0b770b543b167f7ae57fcb8eb9ad18357e75a5241b0b170a9b1fa4ac05f564f6`,
and original deployed BotConfig SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Require zero Login, Game, ArcheAge, and PlayerBots observer processes; free ports
1234, 1237, 1239, 1250, and 1280; and empty live-log locations. Observe the
existing MySQL identities only through OS metadata. Never connect, query, start,
stop, or modify a database. Retain T-117's v10 root exactly as found and never
write into it.

Before changing BotConfig, preserve its exact original bytes at a new
session-local, non-client, non-evidence-root path that will remain available for
later cleanup. Change only these nine Activity Director assignments:

- enabled `true`
- zone `221`
- character IDs `[20001,20002,20003]`
- population minimum/target/maximum `2/3/3`
- initial delay `2000` ms
- reconciliation interval `5000` ms
- retry backoff `30000` ms

Preserve `SearchRadius` 60 and every other byte-level setting semantically.
Start Login and then Game using the accepted local wrappers with hidden launch
helpers and persistent server processes. Require healthy process identities,
loopback listeners on 1234/1237/1239/1250/1280, Game API readiness, PlayerBots
startup, Director start, and admissions for 20001, 20002, and 20003 with no
startup failure. Do not issue fixture or directed gameplay commands.

Use the `computer-use:computer-use` skill for the GUI boundary. Open the
pre-existing AAEmu 1.2 launcher at
`D:\AAEmu\Launcher\AAEmu.Launcher\AAEmu.Launcher.exe`, select exactly one
returned launcher window, and verify it is visible. The launcher is configured
for the registered `aaemu12_client` fixture/junction and loopback server. Do not
automate any authentication dialog, enter or reveal credentials, click the
login/play action, or interact in game; leave the launcher ready for the user to
take over. Do not edit the registered read-only client fixture.

Leave Login, Game, the interactive Director config, and launcher running. Write
`HANDOFF.md` with exact server PIDs/listeners, API/Director/admission status,
launcher window/process status, preserved-config location/hash, runtime lease
state, and user actions. Commit only the handoff. Do not call the session done
if the two servers are not healthy or the launcher window is not visible.

At a later explicit cleanup request, close the client/launcher normally, stop
Game then Login gracefully, restore the exact preserved BotConfig bytes, verify
zero relevant processes/listeners and clean live-log state, and update the
handoff. Never force-stop, delete, reset, or discard anything.
