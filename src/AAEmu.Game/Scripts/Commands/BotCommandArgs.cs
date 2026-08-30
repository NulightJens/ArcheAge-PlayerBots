using System.Globalization;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public static class BotCommandArgs
{
    public static bool TryBotId(string[] args, int index, out uint id, out string error)
    {
        id = 0;
        if (args == null || index < 0 || index >= args.Length || !uint.TryParse(args[index], out id) || id == 0)
        {
            error = "help";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryCoord(string value, out float coordinate)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate) &&
               float.IsFinite(coordinate);
    }

    public static bool TryBotStrategy(
        string[] args,
        out string target,
        out BotEngineKind engineKind,
        out char operation,
        out string[] names,
        out string error)
    {
        target = null;
        engineKind = default;
        operation = default;
        names = [];
        error = "help";

        if (args == null || args.Length < 3 || string.IsNullOrWhiteSpace(args[0]) ||
            string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
            return false;

        target = args[0];
        if (!string.Equals(target, "all", StringComparison.OrdinalIgnoreCase) &&
            !TryBotId(args, 0, out _, out _))
            return false;

        engineKind = args[1].ToLowerInvariant() switch
        {
            "co" => BotEngineKind.Combat,
            "nc" => BotEngineKind.NonCombat,
            "de" => BotEngineKind.Dead,
            _ => (BotEngineKind)(-1)
        };
        if (!Enum.IsDefined(engineKind))
            return false;

        var operationAndNames = args[2].Trim();
        if (operationAndNames.Length == 0)
            return false;

        operation = operationAndNames[0];
        if (operation is not ('+' or '-' or '~' or '?'))
            return false;

        var nameText = operationAndNames[1..];
        if (operation == '?')
        {
            error = null;
            return true;
        }

        names = nameText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = names.Length > 0 && names.All(name => !string.IsNullOrWhiteSpace(name));
        if (valid)
            error = null;
        return valid;
    }

    public static void SendUnknownBot(ICommand command, IMessageOutput messageOutput, uint id)
    {
        CommandManager.SendErrorText(command, messageOutput, $"No active bot found with id {id}.");
    }
}
