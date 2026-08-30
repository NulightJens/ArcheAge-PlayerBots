using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotResetCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["botreset"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<botId>";
        }

        public string GetCommandHelpText()
        {
            return "Resets a bot's combat state, clears target, and forces a new target search.\n" +
                   "Also stops movement and re‑applies gear/skills.";
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

            BotCombatManager.Instance.ResetBot(bot);

            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' reset. Combat state cleared, movement stopped, and archetype refreshed.");
        }
    }
}
