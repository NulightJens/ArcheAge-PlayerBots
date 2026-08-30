using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotArchetypeReloadCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["reloadbotarchetype", "reloadarchetype"];

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
            return "Reloads BotArchetypes.json from disk without restarting the server.";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            if (BotArchetypeManager.Instance.Reload())
                CommandManager.SendNormalText(this, messageOutput, "Bot archetype definitions reloaded successfully.");
            else
                CommandManager.SendErrorText(this, messageOutput, "Bot archetype definitions reload failed; existing definitions were kept.");
        }
    }
}