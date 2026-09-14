#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Navigation;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>Manual diagnostics using the same native action adapter as quest autonomy.</summary>
public sealed partial class BotQuestCommand
{
    private void Interact(string[] args, IMessageOutput output)
    {
        if (args.Length is < 3 or > 4 || !uint.TryParse(args[0], out var id) ||
            !uint.TryParse(args[2], out var objectId) ||
            args[1] is not ("inspect" or "approach" or "use") ||
            (args.Length == 4 && !uint.TryParse(args[3], out _)))
        {
            CommandManager.SendDefaultHelpText(this, output);
            return;
        }
        var bot = BotManager.Instance.GetBot(id);
        var runtime = BotHost.Instance.GetRuntime(id);
        if (bot == null || runtime == null)
        {
            CommandManager.SendErrorText(this, output, "Bot is not active.");
            return;
        }
        lock (runtime.SyncRoot)
        {
            var plan = BotQuestInteractions.ForDoodad(bot, bot.ParentWorld?.GetDoodad(objectId),
                args.Length == 4 ? uint.Parse(args[3]) : 0);
            if (!plan.Available)
            {
                CommandManager.SendErrorText(this, output, plan.Reason);
                return;
            }
            if (args[1] == "inspect")
            {
                CommandManager.SendNormalText(this, output,
                    $"bot={id} doodad={plan.DoodadTemplateId}:{objectId} phase={plan.Phase} skill={plan.SkillId} " +
                    $"position={plan.Position} range={plan.Range} in_range={BotQuestInteractions.InRange(bot.Transform.World.Position, plan.Position, plan.Range)}");
                return;
            }
            // Explicit Idle prevents a manual action from racing quest/combat ownership.
            if (runtime.CombatState.ForcedState != BotCombatStateType.Idle || runtime.CombatState.Target != null ||
                runtime.MovementState.FollowTarget != null || bot.IsDead || bot.IsInBattle)
            {
                CommandManager.SendErrorText(this, output, "Hold the bot with botstate <id> idle before manual interaction.");
                return;
            }
            if (args[1] == "approach")
            {
                var accepted = BotQuestApproachPlanner.TryForWorldObject(bot.Transform.World.Position,
                    plan.Position, plan.Range, bot.ParentWorld.GetHeight, bot.ParentWorld.IsWater,
                    out var point) && BotManager.Instance.SetBotTravelDestination(bot, point, true,
                    BotTravelIntent.Interaction, BotMovementOwner.External);
                CommandManager.SendNormalText(this, output, $"manual_assistance bot={id} approach_requested={accepted}; inspect arrival before use.");
                return;
            }
            if (runtime.MovementState.IsMoving || runtime.MovementState.Destination.HasValue)
            {
                CommandManager.SendErrorText(this, output, "Wait for the bot to stop before use.");
                return;
            }
            var result = BotQuestInteractions.Execute(bot, plan);
            CommandManager.SendNormalText(this, output,
                $"manual_assistance bot={id} started={result.Started} reason={result.Reason}; verify native outcomes.");
        }
    }
}
#endif
