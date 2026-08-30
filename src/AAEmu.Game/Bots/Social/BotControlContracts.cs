using AAEmu.Game.Models.Game.Team;

namespace AAEmu.Game.Bots.Social;

public enum BotControlVerb
{
    Follow,
    Stay,
    Attack,
    Passive,
    Role
}

public enum BotMovementOrder
{
    Stay,
    Follow
}

public enum BotCombatOrder
{
    Passive,
    Assist
}

public enum BotControlStatus
{
    Accepted,
    UnknownBot,
    Unavailable,
    Unauthorized,
    InvalidTarget,
    InvalidRole
}

public readonly record struct BotControlDispatchResult(BotControlStatus Status, string Message)
{
    public bool Accepted => Status == BotControlStatus.Accepted;
}

public readonly record struct BotControlEvent(
    uint RequesterId,
    uint TeamId,
    uint MasterId,
    BotControlVerb Verb,
    uint TargetObjId = 0,
    MemberRole Role = MemberRole.Undecided);
