using System;
using System.Globalization;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotStateCommand : ICommand
    {
        public string[] CommandNames { get; set; } = { "botstate" };

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<botId> [state|free] [positiveKillGoal]";
        }

        public string GetCommandHelpText()
        {
            return "Shows or changes the bot's state.\n" +
                   "  /botstate <id>             - Show current state, target, HP, forced state\n" +
                   "  /botstate <id> idle        - Force bot into idle (stops combat)\n" +
                   "  /botstate <id> grind [n]   - Force grinding and optionally stop after n kills\n" +
                   "  /botstate <id> questing    - Force bot into questing mode (enables combat, uses target filter)\n" +
                   "  /botstate <id> roaming     - Force bot into roaming (requires roam destination set)\n" +
                   "  /botstate <id> following   - Force bot into following (target must be set)\n" +
                   "  /botstate <id> resting     - Force bot into resting (heal)\n" +
                   "  /botstate <id> free        - Release forced state, bot resumes automatic control";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            if (args.Length == 0)
            {
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
            }

            if (!BotCommandArgs.TryBotId(args, 0, out var botId, out _))
            {
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
            }

            var bot = BotManager.Instance.GetBot(botId);
            if (bot == null)
            {
                BotCommandArgs.SendUnknownBot(this, messageOutput, botId);
                return;
            }

            var combatState = BotCombatManager.Instance.GetState(bot);
            if (combatState == null)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot.Name}' has no combat state (start listening first).");
                return;
            }

            var moveState = BotManager.Instance.GetBotState(bot.Id);

            // If no second arg, show info
            if (args.Length == 1)
            {
                var forcedStr = combatState.ForcedState.HasValue ? combatState.ForcedState.Value.ToString() : "None (auto)";
                var targetName = combatState.Target == null
                    ? "None"
                    : string.IsNullOrWhiteSpace(combatState.Target.Name)
                        ? $"#{combatState.Target.ObjId}"
                        : combatState.Target.Name;
                var hp = $"{bot.Hp}/{bot.MaxHp}";
                var following = moveState?.FollowTarget?.Name ?? "None";
                var healthFloor = combatState.StopAtTargetHpPercent is { } floor
                    ? $"{floor}%"
                    : "None";
                CommandManager.SendNormalText(this, messageOutput,
                    $"Bot '{bot.Name}' (Id: {bot.Id}) | State: {combatState.CurrentState} | Forced: {forcedStr} | Active: {combatState.IsActive} | Target: {targetName} | HP: {hp} | Following: {following} | StopAtHP: {healthFloor}");
                return;
            }

            // Parse the command
            var cmd = args[1].ToLowerInvariant();

            int? killGoal = null;
            if (args.Length > 2)
            {
                if (cmd is not ("grind" or "grinding") ||
                    !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedGoal) ||
                    parsedGoal <= 0)
                {
                    CommandManager.SendErrorText(this, messageOutput,
                        "A positive kill goal is required and is supported only with grind.");
                    return;
                }

                killGoal = parsedGoal;
            }

            // Handle "free" – release forced state through the combat manager.
            if (cmd == "free")
            {
                combatState.StopAtTargetHpPercent = null;
                combatState.NonlethalFloorReached = null;
                BotCombatManager.Instance.SetForcedState(bot, null);
                if (combatState.IsActive)
                    BotCombatManager.Instance.StartListening(bot);
                CommandManager.SendNormalText(this, messageOutput,
                    $"Bot '{bot.Name}' is now free (current state: {combatState.CurrentState}).");
                return;
            }

            // Map string to enum
            BotCombatStateType? newState = cmd switch
            {
                "idle" => BotCombatStateType.Idle,
                "grind" or "grinding" => BotCombatStateType.Grinding,
                "quest" or "questing" => BotCombatStateType.Questing,
                "roam" or "roaming" => BotCombatStateType.Roaming,
                "follow" or "following" => BotCombatStateType.Following,
                "rest" or "resting" => BotCombatStateType.Resting,
                _ => null
            };

            if (!newState.HasValue)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Invalid state. Use: idle, grind, questing, roaming, following, resting, or free.");
                return;
            }

            combatState.StopAtTargetHpPercent = null;
            combatState.NonlethalFloorReached = null;

            if (killGoal.HasValue)
            {
                combatState.KillGoal = killGoal.Value;
                combatState.KillCount = 0;
                combatState.IsActive = true;
            }

            // Set the requested base state. Temporary combat states normally finish
            // before reverting, but an explicit idle command is an immediate
            // disengage: clear both target references and all movement/follow state.
            combatState.SetForcedState(newState.Value);
            if (newState.Value == BotCombatStateType.Idle)
            {
                BotManager.Instance.StopFollow(bot);
                combatState.Target = null;
                bot.CurrentTarget = null;
                combatState.TransitionTo(BotCombatStateType.Idle);
            }
            else
            {
                // State commands are also wake commands. Reattach a combat brain
                // that may have been shed by inactive-duel cleanup while keeping
                // forced Idle as the zero-work path.
                BotCombatManager.Instance.StartListening(bot);
            }
            var goalSuffix = killGoal.HasValue ? $" with kill goal {killGoal.Value}" : string.Empty;
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' forced into {newState.Value} state{goalSuffix}.");

            // Extra info for states that need extra setup
            if (newState.Value == BotCombatStateType.Roaming && !combatState.RoamDestination.HasValue)
            {
                CommandManager.SendNormalText(this, messageOutput, "[WARNING] Bot has no roam destination set. Use /movebot to set one.");
            }
            if (newState.Value == BotCombatStateType.Following && moveState?.FollowTarget == null)
            {
                CommandManager.SendNormalText(this, messageOutput, "[WARNING] Bot has no follow target set.");
            }
            if (newState.Value == BotCombatStateType.Questing && !combatState.TargetTypeFilter.HasValue)
            {
                CommandManager.SendNormalText(this, messageOutput, "[WARNING] Bot has no target type filter set for questing.");
            }
        }
    }
}
