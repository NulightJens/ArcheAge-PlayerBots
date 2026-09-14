# Install PlayerBots

Requirements: Git, Python 3.11 or newer, .NET 10 SDK, a configured AAEmu 1.2 server,
and your own matching client/data. Use a separate server checkout for the preview.

```powershell
git clone https://github.com/AAEmu/AAEmu AAEmu-PlayerBots
git -C AAEmu-PlayerBots checkout 62e3eb1d87da01194802ac886cd500134facad28
python install.py --host C:/path/to/AAEmu-PlayerBots
dotnet build C:/path/to/AAEmu-PlayerBots/AAEmu.Game/AAEmu.Game.csproj
```

The installer requires the exact clean host revision and an unused module destination.
It installs the module and applies one versioned host patch. Keep the original server
checkout for rollback. It does not start a server or change a database.

Follow AAEmu's normal server/data setup. Apply the included SQL migration to the
intended game database using your normal database administration tool. It adds the
bot archetype plan table; existing characters remain intact. Configure the module's
`Configurations/BotConfig.json` alongside the built Game service's configuration.

For new bot creation, set `AAEMU_PLAYERBOTS_ACCOUNT_ID` to the numeric ID of a dedicated
existing game account before starting Game. Saved offline characters can be used with
`/addbot` without creating new characters. Bot creation does not create that account.

Start your server normally and join as a GM. `/bot` lists the available commands.
See [Playing with bots](PLAYING.md). Quest autonomy is opt-in. Keep
`StuckTeleportEnabled` disabled for ordinary unassisted operation.

Upgrading: prepare a fresh checkout and preserve the prior installation, configuration
and database backup. This installer does not overwrite an existing module.
