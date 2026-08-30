namespace AAEmu.Game.Bots.Kernel;

public readonly record struct BotEvent(string Name = null, object Payload = null);
