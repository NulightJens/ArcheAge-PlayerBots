# PlayerBots runtime

The host schedules bot movement, combat, quest and party behavior.
`Host/IBotDriver.cs` connects optional external client control; `ServerDriver`
provides the ordinary server bot path. `Host/IBotHostObserver.cs` accepts
optional observers without adding a telemetry dependency to the module.
Register observers before admitting bots. Observer exceptions are contained.
