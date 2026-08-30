using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotConfigCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["reloadbotconfig", "rbc"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return string.Empty;
        }

        public string GetCommandHelpText()
        {
            return "Reloads BotConfig.json from disk without restarting the server.";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            BotConfig.Instance.Reload();
            CommandManager.SendNormalText(this, messageOutput, "BotConfig reloaded successfully.");
        }
    }
}