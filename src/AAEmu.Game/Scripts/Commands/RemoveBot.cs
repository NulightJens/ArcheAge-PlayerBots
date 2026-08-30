using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class RemoveBot : ICommand
{
    public string[] CommandNames { get; set; } = ["removebot", "remove_bot"];

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
        return "Logs out a currently active bot, saving its state, same as a normal player logout.";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!BotCommandArgs.TryBotId(args, 0, out var targetCharacterId, out _))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var bot = BotManager.Instance.GetBot(targetCharacterId);
        if (bot?.IsInDuel == true)
        {
            CommandManager.SendErrorText(this, messageOutput,
                "Bot is in a duel; end the duel first (/botstate <id> idle after the duel, or wait).");
            return;
        }

        if (BotManager.Instance.DespawnBot(targetCharacterId))
            CommandManager.SendNormalText(this, messageOutput, $"Bot (Id: {targetCharacterId}) logged out.");
        else
            BotCommandArgs.SendUnknownBot(this, messageOutput, targetCharacterId);
    }
}
