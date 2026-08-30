using AAEmu.Game.Core.Managers;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotDebugCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["botdebug"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<characterId>";
        }

        public string GetCommandHelpText()
        {
            return "Prints debug info for a bot's movement and combat state.";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
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

            var moveState = BotManager.Instance.GetBotState(botId);
            var combatState = BotCombatManager.Instance.GetState(bot);

            CommandManager.SendNormalText(this, messageOutput, $"=== Bot '{bot.Name}' (Id: {bot.Id}, ObjId: {bot.ObjId}) ===");
            CommandManager.SendNormalText(this, messageOutput, $"Position: {bot.Transform.World.Position}");
            CommandManager.SendNormalText(this, messageOutput, $"HP: {bot.Hp}/{bot.MaxHp}, MP: {bot.Mp}/{bot.MaxMp}");
            CommandManager.SendNormalText(this, messageOutput, $"IsDead: {bot.IsDead}, IsInBattle: {bot.IsInBattle}");

            if (moveState != null)
            {
                CommandManager.SendNormalText(this, messageOutput, $"--- Movement State ---");
                CommandManager.SendNormalText(this, messageOutput, $"Destination: {moveState.Destination?.ToString() ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput, $"IsRunning: {moveState.IsRunning}");
                CommandManager.SendNormalText(this, messageOutput, $"IsMoving: {moveState.IsMoving}");
                CommandManager.SendNormalText(this, messageOutput, $"IsFalling: {moveState.IsFalling}");
                CommandManager.SendNormalText(this, messageOutput, $"FallVelocity: {moveState.FallVelocity}");
                CommandManager.SendNormalText(this, messageOutput, $"FollowTarget: {moveState.FollowTarget?.Name ?? "null"}");
            }
            else
            {
                CommandManager.SendErrorText(this, messageOutput, "No movement state found.");
            }

            if (combatState != null)
            {
                CommandManager.SendNormalText(this, messageOutput, $"--- Combat State ---");
                CommandManager.SendNormalText(this, messageOutput, $"IsActive: {combatState.IsActive}");
                CommandManager.SendNormalText(this, messageOutput, $"IsResting: {combatState.IsResting}");
                CommandManager.SendNormalText(this, messageOutput, $"Target: {combatState.Target?.Name ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput, $"KillCount: {combatState.KillCount}");
                CommandManager.SendNormalText(this, messageOutput, $"ShouldRespawn: {combatState.ShouldRespawn}");
            }
            else
            {
                CommandManager.SendNormalText(this, messageOutput, "Combat state: not active.");
            }

            // Also check task status
            CommandManager.SendNormalText(this, messageOutput, $"Movement task running: {BotManager.Instance.IsMovementTaskRunning(botId)}");
            CommandManager.SendNormalText(this, messageOutput, $"Combat task running: {BotCombatManager.Instance.IsTaskRunning(botId)}");
            var hostMetrics = BotHost.Instance.Metrics;
            CommandManager.SendNormalText(this, messageOutput, $"Host metrics: bots={hostMetrics.LastTickBots}, active={hostMetrics.ActiveBots}, tick_ms_ema={hostMetrics.TickMsEma:F2}, max={hostMetrics.MaxTickMs:F2}, skipped={hostMetrics.SkippedTicks}, brain_steps={hostMetrics.BrainStepsTotal}, mover_steps={hostMetrics.MoverStepsTotal}");
        }
    }
}
