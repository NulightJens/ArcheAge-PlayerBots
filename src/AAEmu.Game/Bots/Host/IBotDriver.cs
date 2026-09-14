#if !PLAYERBOTS_AAEMU_3_0
#nullable enable
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Bots.Questing;

namespace AAEmu.Game.Bots.Host;

public interface IBotDriver
{
    // True when an external driver owns the bot's movement and actions.
    bool Owns(Character bot);
    // True skips the module's step for this tick.
    bool BeforeStep(BotRuntime runtime, DateTimeOffset now);
    bool CanRunBrain => true;
    float? ObservedFacing(BotRuntime runtime, DateTimeOffset now) => null;
    void RequestFacing(Character bot, float radians) { }
    // True includes the owning client in the prepared movement broadcast.
    bool AllowOwnerMovement(Character bot, MoveType move);
    // Null rejects an owned action; the module must not execute it on the server.
    IPendingAction? TryEnqueue(BotRuntime runtime, BotClientRequest request);
    void Pause(BotRuntime runtime);
    void Release(BotRuntime runtime);
}

public sealed record BotClientRequest(uint SkillId, SkillCastTarget? Target, uint DoodadId, string Kind)
{
    public BotInteractionPlan? Interaction { get; init; }
    public Action<SkillResult, DateTimeOffset>? Completed { get; init; }
}

public sealed class ServerDriver : IBotDriver
{
    public static ServerDriver Instance { get; } = new();
    public bool Owns(Character bot) => false;
    public bool BeforeStep(BotRuntime runtime, DateTimeOffset now) => false;
    public bool AllowOwnerMovement(Character bot, MoveType move) => false;
    public IPendingAction? TryEnqueue(BotRuntime runtime, BotClientRequest request) => null;
    public void Pause(BotRuntime runtime) { }
    public void Release(BotRuntime runtime) { }
}
#endif
