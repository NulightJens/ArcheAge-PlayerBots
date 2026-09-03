# Live bot monitor

`Show-LiveBotMonitor.ps1` displays one bot's current quest, combat, navigation, rotation, health, and runtime decisions. It refreshes a static color-coded board and appends only changed decisions to a log.

The monitor is read-only. It calls `botdebug` and `botrotation` through AAEmu's loopback command API.

```powershell
pwsh -NoLogo -NoProfile -File scripts/autonomy/Show-LiveBotMonitor.ps1 `
  -BotId 128 `
  -LogPath C:\playerbots-logs\bot-128-decisions.log `
  -ServerLogPath C:\aaemu\AAEmu.Game\bin\Debug\net10.0\Logs\Server.log
```

`ServerLogPath` is optional. When supplied, the board lists quest IDs observed for that bot. The active quest comes from `botdebug`; older IDs are labeled observed because the monitor does not mutate or independently inspect quest state.

Keep the Web API bound to `127.0.0.1`. The board waits and reconnects after a graceful server restart.
