using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Host;

public static class BotActivityGovernor
{
    public static bool IsAlwaysActive(BotRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        if (runtime.CombatState.CurrentState is BotCombatStateType.Combat or BotCombatStateType.Dueling or BotCombatStateType.Searching)
            return true;
        if (runtime.MovementState.FollowTarget != null || runtime.CombatState.ForcedState != null)
            return true;

        var now = runtime.Schedule.Now;
        return runtime.Blackboard.TryGet(BotValues.NearestRealPlayerDistance, now, out var distance) &&
               distance <= (float)BotConfig.Instance.ActivityRealPlayerRadius;
    }

    public static bool IsInRotation(uint botId, long windowIndex, int activePercent)
    {
        activePercent = Math.Clamp(activePercent, 0, 100);
        if (activePercent == 0)
            return false;
        if (activePercent == 100)
            return true;

        var hash = unchecked((botId * 2654435761u) ^ (uint)windowIndex);
        return hash % 100 < activePercent;
    }

    public static int EffectiveActivePercent(
        int configured,
        double hostTickMsEma,
        double hostBudgetMs,
        double serverPressureMs = double.NaN,
        double serverBudgetMs = 0)
    {
        configured = Math.Clamp(configured, 0, 100);
        if (configured == 0)
            return 0;

        var hostPercent = ScaleToBudget(configured, hostTickMsEma, hostBudgetMs);
        var serverPercent = ScaleToBudget(configured, serverPressureMs, serverBudgetMs);
        return Math.Min(hostPercent, serverPercent);
    }

    private static int ScaleToBudget(int configured, double observedMs, double budgetMs)
    {
        if (!double.IsFinite(observedMs) || budgetMs <= 0 || observedMs <= budgetMs)
            return configured;
        if (observedMs >= budgetMs * 2)
            return 0;

        var scaled = configured * ((budgetMs * 2 - observedMs) / budgetMs);
        return Math.Clamp((int)scaled, 0, configured);
    }
}
