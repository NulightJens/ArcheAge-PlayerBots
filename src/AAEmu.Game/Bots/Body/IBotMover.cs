using System.Numerics;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Body;

public interface IBotMover
{
    void SetDestination(Character bot, Vector3 position, bool run = true, float tolerance = 0.5f);
    void SetRecoveryDestination(Character bot, Vector3 position, bool run = true, float tolerance = 0.5f) =>
        SetDestination(bot, position, run, tolerance);
    void StopIfMoving(Character bot);
    void StopImmediately(Character bot);
    void Face(Character bot, float angle);
    void Teleport(Character bot, Vector3 position);
    void Follow(Character bot, Character target, float distance);
    void StopFollow(Character bot);
    void SendRelaxedStance(Character bot);
}

public sealed class BotManagerMover : IBotMover
{
    public static BotManagerMover Instance { get; } = new();

    private BotManagerMover()
    {
    }

    public void SetDestination(Character bot, Vector3 position, bool run = true, float tolerance = 0.5f)
    {
        BotManager.Instance.SetBotDestinationIfChanged(bot, position, run, tolerance);
    }

    public void SetRecoveryDestination(Character bot, Vector3 position, bool run = true, float tolerance = 0.5f)
    {
        BotManager.Instance.SetBotRecoveryDestinationIfChanged(bot, position, run, tolerance);
    }

    public void StopIfMoving(Character bot)
    {
        BotManager.Instance.StopIfMoving(bot);
    }

    public void StopImmediately(Character bot)
    {
        BotManager.Instance.StopImmediately(bot);
    }

    public void Face(Character bot, float angle)
    {
        if (bot?.Transform == null)
            return;

        BotManager.Instance.GetBroadcaster(bot.Id)?.SendFaceTarget(bot.Transform.World.Position, angle - 90f, bot.IsInBattle);
        bot.Transform.FinalizeTransform();
    }

    public void Teleport(Character bot, Vector3 position)
    {
        BotManager.Instance.MoveBotTo(bot, position.X, position.Y, position.Z);
    }

    public void Follow(Character bot, Character target, float distance)
    {
        BotManager.Instance.SetFollowTarget(bot, target, distance);
    }

    public void StopFollow(Character bot)
    {
        BotManager.Instance.StopFollow(bot);
    }

    public void SendRelaxedStance(Character bot)
    {
        if (bot?.Transform == null)
            return;

        BotManager.Instance.GetBroadcaster(bot.Id)?.SendRelaxedStance(bot.Transform.World.Position);
    }
}
