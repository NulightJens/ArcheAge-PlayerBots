using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Social;

public sealed class BotControlAction : IBotAction
{
    public const string ActionName = "party control command";

    public string Name => ActionName;

    public bool IsUseful(BotContext context) => true;

    public bool IsPossible(BotContext context) => context.Runtime?.Social != null;

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (ev.Payload is not BotControlEvent command)
            return BotActionResult.Impossible;

        var social = context.Runtime.Social;
        if (!social.IsAuthorized(command.RequesterId, command.TeamId, command.MasterId))
            return BotActionResult.Vetoed;

        switch (command.Verb)
        {
            case BotControlVerb.Follow:
                social.ApplyFollow();
                break;
            case BotControlVerb.Stay:
                social.ApplyStay();
                break;
            case BotControlVerb.Attack:
                var target = social.ResolveMasterTarget(command.TargetObjId);
                if (target == null)
                    return BotActionResult.Impossible;
                social.ApplyAttack(target);
                break;
            case BotControlVerb.Passive:
                social.ApplyPassive();
                break;
            case BotControlVerb.Role:
                social.ApplyRole(command.Role);
                break;
            default:
                return BotActionResult.Impossible;
        }

        return BotActionResult.Success;
    }
}
