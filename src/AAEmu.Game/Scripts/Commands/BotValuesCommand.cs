using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public sealed class BotValuesCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botvalues"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId> [filter]";

    public string GetCommandHelpText() => "Shows the bot blackboard values and their computed times.";

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

        var blackboard = BotHost.Instance.GetRuntime(botId)?.Blackboard;
        if (blackboard == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot.Name}' has no runtime.");
            return;
        }

        var filter = args.Length > 1 ? args[1] : null;
        var values = blackboard.Snapshot()
            .Where(value => string.IsNullOrWhiteSpace(filter) || value.name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (values.Count == 0)
        {
            CommandManager.SendNormalText(this, messageOutput, $"Bot '{bot.Name}' values: (none)");
            return;
        }

        foreach (var value in values)
        {
            var computedAt = value.computedAt == DateTime.MinValue ? "<not computed>" : value.computedAt.ToString("O");
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' value {value.name}={value.value} computedAt={computedAt}");
        }
    }
}
