using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class AddBot : ICommand
{
    public string[] CommandNames { get; set; } = ["addbot", "add_bot"];

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
        return "Logs in an existing offline character as a server-controlled bot.";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!BotCommandArgs.TryBotId(args, 0, out var targetCharacterId, out _))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var result = BotManager.Instance.SpawnBot(targetCharacterId, out var bot);
        if (result == SpawnResult.Ok)
        {
            CommandManager.SendNormalText(this, messageOutput,
                $"Spawned bot '{bot.Name}' (Id: {bot.Id}, ObjId: {bot.ObjId}) in the world.");
        }
        else if (result == SpawnResult.AlreadyActive)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Bot with character id {targetCharacterId} is already spawned.");
        }
        else if (result == SpawnResult.Online)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Character with id {targetCharacterId} is already online.");
        }
        else
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Failed to spawn bot for character id {targetCharacterId}, check the server console.");
        }
    }
}
