# PlayerBots runtime

The host schedules bot movement, combat, quest and party behavior.
`Host/IBotDriver.cs` connects optional external client control; `ServerDriver`
provides the ordinary server bot path. `Host/IBotHostObserver.cs` provides
optional bot lifecycle and activity notifications.
Register observers before admitting bots. Observer exceptions are contained.
