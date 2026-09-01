@{
    RootModule = 'AutonomyObserver.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'a862ce1b-a6ef-40a1-b369-e995dad735d0'
    Author = 'PlayerBots contributors'
    CompanyName = 'PlayerBots'
    Copyright = '(c) PlayerBots contributors'
    Description = 'Offline-safe, read-only observation and parsing for one declared PlayerBots identity.'
    PowerShellVersion = '7.0'
    FunctionsToExport = @(
        'ConvertFrom-AutonomyBotDebugResponse',
        'Start-AutonomyObserver'
    )
    CmdletsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @()
    PrivateData = @{
        PSData = @{
            Tags = @('PlayerBots', 'Observation', 'AAEmu12')
        }
    }
}
