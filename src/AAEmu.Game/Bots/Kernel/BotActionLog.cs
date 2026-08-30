namespace AAEmu.Game.Bots.Kernel;

public readonly record struct BotActionLog(
    DateTime Time,
    string Action,
    float Relevance,
    BotActionResult Result);
