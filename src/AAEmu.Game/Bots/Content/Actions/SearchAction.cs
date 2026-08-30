using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.AI.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class SearchAction : IBotAction
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly IBotMover _mover;
    private readonly Func<BotContext, IReadOnlyList<Character>> _nearbyCharacters;
    private readonly Func<BotContext, Unit, bool> _fight;

    public SearchAction(
        IBotMover mover = null,
        Func<BotContext, IReadOnlyList<Character>> nearbyCharacters = null,
        Func<BotContext, Unit, bool> fight = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
        _nearbyCharacters = nearbyCharacters ?? DefaultNearbyCharacters;
        _fight = fight ?? ((context, target) => BasicCombat.Execute(context.Bot, context.Runtime.CombatState, target));
    }

    public string Name => "search tick";

    public bool IsUseful(BotContext context)
    {
        return context.Runtime.CombatState.CurrentState == BotCombatStateType.Searching;
    }

    public bool IsPossible(BotContext context) => context.Runtime.CombatState.LastKnownTargetPosition.HasValue;

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var state = context.Runtime.CombatState;
        var mover = context.Mover ?? _mover;
        if (!state.LastKnownTargetPosition.HasValue)
        {
            state.IsSearching = false;
            state.SearchRadius = 0f;
            state.SearchAngle = 0f;
            ExitTemporaryState(context, mover);
            return BotActionResult.Success;
        }

        if ((context.Now - state.SearchStartTime).TotalSeconds > 50)
        {
            Log(context, $"BOT id={context.Bot.Id} ev=search_give_up");
            state.LastKnownTargetPosition = null;
            state.IsSearching = false;
            state.SearchRadius = 0f;
            state.SearchAngle = 0f;
            ExitTemporaryState(context, mover);
            return BotActionResult.Success;
        }

        var targetPosition = state.LastKnownTargetPosition.Value;
        var currentPosition = context.Bot.Transform.World.Position;
        var distanceToLast = Vector3.Distance(currentPosition, targetPosition);
        Unit foundTarget = null;

        context.Runtime.HostMetrics?.RecordWorldScan(BotWorldScanKind.Search);
        foreach (var character in _nearbyCharacters(context))
        {
            if (character == context.Bot)
                continue;
            if (state.InDuel ? character != state.DuelOpponent : !context.Bot.CanAttack(character))
                continue;

            var distanceToCharacter = Vector3.Distance(currentPosition, character.Transform.World.Position);
            if (distanceToCharacter <= 2f || !IsStealthed(character))
            {
                foundTarget = character;
                break;
            }
        }

        if (foundTarget != null)
        {
            state.Target = foundTarget;
            state.LastKnownTargetPosition = null;
            state.IsSearching = false;
            state.SearchRadius = 0f;
            state.SearchAngle = 0f;
            if (state.InDuel)
            {
                state.DuelOpponent = foundTarget;
                state.TransitionTo(BotCombatStateType.Dueling);
            }
            else
            {
                state.TransitionTo(BotCombatStateType.Combat);
            }

            if (_fight(context, foundTarget))
                state.LastSkillTime = context.Now;
            return BotActionResult.Success;
        }

        if (distanceToLast > 1f)
        {
            mover.SetDestination(context.Bot, targetPosition, run: true);
            return BotActionResult.Success;
        }

        state.SearchAngle += 0.15f;
        var elapsed = (float)(context.Now - state.SearchStartTime).TotalSeconds;
        var currentRadius = Math.Min(30f, 2f + elapsed / 20f * 28f);
        state.SearchRadius = currentRadius;
        var destination = new Vector3(
            targetPosition.X + (float)Math.Cos(state.SearchAngle) * currentRadius,
            targetPosition.Y + (float)Math.Sin(state.SearchAngle) * currentRadius,
            targetPosition.Z);
        if (context.Bot.ParentWorld != null)
        {
            var groundZ = context.Bot.ParentWorld.GetHeight(destination.X, destination.Y);
            if (groundZ > 0)
                destination.Z = groundZ;
        }

        mover.SetDestination(context.Bot, destination, run: true);
        return BotActionResult.Success;
    }

    private static void ExitTemporaryState(BotContext context, IBotMover mover)
    {
        context.Runtime.CombatState.RestorePreviousState();
        context.Runtime.CombatState.RevertToForcedState();
        mover.StopImmediately(context.Bot);
        BotCombatManager.SendRelaxedStance(context.Bot, mover);
    }

    private static bool IsStealthed(Unit unit)
    {
        return unit != null && unit.Buffs.HasEffectsMatchingCondition(effect => effect.Template.Stealth);
    }

    private static IReadOnlyList<Character> DefaultNearbyCharacters(BotContext context)
    {
        return WorldManager.GetAround<Character>(context.Bot, 30f, true).ToList();
    }

    private static void Log(BotContext context, string message)
    {
        Logger.Trace(message);
        context.EventSink?.Invoke(message[(message.IndexOf("ev=", StringComparison.Ordinal))..]);
    }
}
