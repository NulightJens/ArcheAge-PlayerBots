using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Host;

public static class BotScheduler
{
    public static BotCadence Classify(BotRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (runtime.CombatState.CurrentState is BotCombatStateType.Combat or BotCombatStateType.Dueling or BotCombatStateType.Searching)
            return BotCadence.Combat;

        if (runtime.MovementState.Destination != null || runtime.MovementState.FollowTarget != null)
            return BotCadence.Moving;

        if (runtime.CombatState.CurrentState == BotCombatStateType.Resting)
            return BotCadence.Resting;

        return BotCadence.Idle;
    }

    public static TimeSpan NextDelay(BotCadence cadence, uint botId, int roll)
    {
        var config = BotConfig.Instance;
        return cadence switch
        {
            BotCadence.Combat => TimeSpan.FromMilliseconds(config.ReactDelayCombatMs),
            BotCadence.Moving => TimeSpan.FromMilliseconds(config.ReactDelayMovingMs),
            BotCadence.Resting => TimeSpan.FromMilliseconds(config.ReactDelayRestingMs),
            BotCadence.Inactive => TimeSpan.FromMilliseconds(config.PassiveDelayMs),
            BotCadence.Idle => TimeSpan.FromMilliseconds(IdleDelay(config, roll)),
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null)
        };
    }

    public static TimeSpan InitialStagger(uint botId)
    {
        return TimeSpan.FromMilliseconds(botId % 10 * 10);
    }

    private static double IdleDelay(BotConfig config, int roll)
    {
        var min = (int)Math.Ceiling(config.ReactDelayIdleMinMs);
        var max = (int)Math.Floor(config.ReactDelayIdleMaxMs);
        if (max <= min)
            return min;

        var span = (uint)(max - min + 1);
        var offset = unchecked((uint)roll) % span;
        return min + offset;
    }
}
