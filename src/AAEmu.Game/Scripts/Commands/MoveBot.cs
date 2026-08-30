using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class MoveBot : ICommand
    {
        public string[] CommandNames { get; set; } = ["movebot", "walkbot"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<characterId> <x> <y> <z> [walk|run]";
        }

        public string GetCommandHelpText()
        {
            return "Moves a bot to the given coordinates by walking/running. Use 'walk' or 'run' as optional 5th argument (default run).";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            if (args.Length < 4 || !BotCommandArgs.TryBotId(args, 0, out var botId, out _)
                || !BotCommandArgs.TryCoord(args[1], out var x)
                || !BotCommandArgs.TryCoord(args[2], out var y)
                || !BotCommandArgs.TryCoord(args[3], out var z))
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

            bool run = true;
            if (args.Length > 4 && args[4].Equals("walk", StringComparison.OrdinalIgnoreCase))
                run = false;

            BotManager.Instance.SetBotDestination(bot, x, y, z, run);
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' is now moving {(run ? "running" : "walking")} to ({x}, {y}, {z}).");
        }
    }
}
