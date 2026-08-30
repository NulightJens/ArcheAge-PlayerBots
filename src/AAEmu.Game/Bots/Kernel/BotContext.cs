using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Bots;

namespace AAEmu.Game.Bots.Kernel;

public sealed class BotContext
{
    public BotContext(
        Character bot,
        BotRuntime runtime,
        BotBlackboard blackboard,
        DateTime now,
        BotConfig config,
        BotEngineKind engineKind,
        BotCombatTask brain = null,
        IBotMover mover = null)
    {
        Bot = bot ?? throw new ArgumentNullException(nameof(bot));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
        Now = now;
        Config = config ?? throw new ArgumentNullException(nameof(config));
        EngineKind = engineKind;
        Brain = brain;
        Mover = mover;
    }

    public Character Bot { get; }
    public BotRuntime Runtime { get; }
    public BotBlackboard Blackboard { get; }
    public DateTime Now { get; }
    public BotConfig Config { get; }
    public BotEngineKind EngineKind { get; }
    public BotCombatTask Brain { get; }
    public IBotMover Mover { get; }
    public Action<string> EventSink { get; set; }
    public Func<Unit> Defender { get; set; }

    public Unit TryDefend()
    {
        if (Defender != null)
            return Defender();

        return DefendRules.IsBeingAttackedByPlayer(Bot, out var attacker) ? attacker : null;
    }

    public void SetDestination(Vector3 destination, bool run = true)
    {
        BotManager.Instance.SetBotDestination(Bot, destination.X, destination.Y, destination.Z, run);
    }

    public void Stop() => BotManager.Instance.StopBot(Bot);

    public void Face(Character target)
    {
        if (target == null || Bot.Transform == null || target.Transform == null)
            return;

        var from = Bot.Transform.World.Position;
        var to = target.Transform.World.Position;
        var degrees = MathF.Atan2(to.Y - from.Y, to.X - from.X) * 180f / MathF.PI;
        Bot.Transform.Local.SetRotationDegree(0f, 0f, degrees);
    }
}
