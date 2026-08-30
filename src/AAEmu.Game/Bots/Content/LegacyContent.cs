using AAEmu.Game.Bots.Content.Actions;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;

namespace AAEmu.Game.Bots.Content;

public static class LegacyContent
{
    private static readonly object s_syncRoot = new();
    private static bool s_registered;

    internal static void ResetForTests()
    {
        lock (s_syncRoot)
        {
            s_registered = false;
        }
    }

    public static void Register()
    {
        lock (s_syncRoot)
        {
            if (!s_registered)
            {
                BotContentRegistry.RegisterStrategy("legacy", static () => new LegacyStrategy());
                BotContentRegistry.RegisterStrategy("follow", static () => new FollowStrategy());
                BotContentRegistry.RegisterStrategy("rest", static () => new RestStrategy());
                BotContentRegistry.RegisterStrategy("search", static () => new SearchStrategy());
                BotContentRegistry.RegisterAction("legacy tick", static () => new LegacyTickAction());
                BotContentRegistry.RegisterAction("follow tick", static () => new FollowAction());
                BotContentRegistry.RegisterAction("rest tick", static () => new RestAction());
                BotContentRegistry.RegisterAction("search tick", static () => new SearchAction());
                BotContentRegistry.RegisterAction("begin-rest", static () => new BeginRestAction());
                BotContentRegistry.RegisterAction("begin-search", static () => new BeginSearchAction());
                BotContentRegistry.RegisterAction("drop-target", static () => new DropTargetAction());
                BotContentRegistry.RegisterAction("avoid-hazard", static () => new AvoidHazardAction());
                BotContentRegistry.RegisterAction("set-facing", static () => new SetFacingAction());
                BotContentRegistry.RegisterAction("reach-melee", static () => new ReachMeleeAction());
                BotContentRegistry.RegisterAction("reach-spell-range", static () => new ReachSpellRangeAction(20f));
                BotContentRegistry.RegisterAction("rear-flank", static () => new RearFlankAction());
                BotContentRegistry.RegisterAction("flee", static () => new FleeAction());
                BotContentRegistry.RegisterAction("unstick", static () => new UnstickAction());
                BotContentRegistry.RegisterAction(BotControlAction.ActionName, static () => new BotControlAction());
                BotContentRegistry.RegisterTrigger("enemy-out-of-melee", static () => new EnemyOutOfMeleeTrigger());
                BotContentRegistry.RegisterTrigger("enemy-out-of-spell-range", static () => new EnemyOutOfSpellRangeTrigger());
                BotContentRegistry.RegisterTrigger("not-facing-target", static () => new NotFacingTargetTrigger());
                BotContentRegistry.RegisterTrigger("not-behind-target", static () => new NotBehindTargetTrigger());
                BotContentRegistry.RegisterTrigger("in-hostile-area", static () => new InHostileAreaTrigger());
                BotContentRegistry.RegisterTrigger("low-health", static () => new LowHealthTrigger());
                BotContentRegistry.RegisterTrigger("target-invalid", static () => new TargetInvalidTrigger());
                BotContentRegistry.RegisterTrigger("target-stealthed", static () => new TargetStealthedTrigger());
                BotContentRegistry.RegisterTrigger("stuck", static () => new StuckTrigger());
                BotContentRegistry.RegisterTrigger("leader-in-combat", static () => new LeaderInCombatTrigger());
                BotContentRegistry.RegisterTrigger("follow-distance", static () => new FollowDistanceTrigger());
                s_registered = true;
            }

            BotContentRegistry.Freeze();
        }
    }
}
