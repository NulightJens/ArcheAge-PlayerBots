using System;

namespace AAEmu.Game.Models.Game.Bots;

internal sealed class BotDiagnostics
{
    public Exception LastError { get; private set; }
    public DateTime LastErrorAt { get; private set; }
    public int ErrorCount { get; private set; }

    public void RecordError(Exception error)
    {
        LastError = error;
        LastErrorAt = DateTime.UtcNow;
        ErrorCount++;
    }
}
