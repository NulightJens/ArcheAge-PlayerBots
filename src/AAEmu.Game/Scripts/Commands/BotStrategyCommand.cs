using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public sealed class BotStrategyCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botstrategy"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId|all> {co|nc|de} {+|-|~|?}<name>[,<name>…]";

    public string GetCommandHelpText() =>
        "Adds, removes, toggles, or lists bot strategies for an engine kind; changes drain pending actions.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!BotCommandArgs.TryBotStrategy(args, out var target, out var kind, out var operation, out var names, out _))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        if (operation != '?' && names.Any(name => !BotContentRegistry.StrategyNames.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            CommandManager.SendErrorText(
                this,
                messageOutput,
                $"Unknown strategy. Registry names: {string.Join(",", BotContentRegistry.StrategyNames.Order(StringComparer.OrdinalIgnoreCase))}.");
            return;
        }

        var bots = ResolveBots(target).ToList();
        if (bots.Count == 0)
        {
            if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
                CommandManager.SendErrorText(this, messageOutput, "No active bots found.");
            else
                BotCommandArgs.SendUnknownBot(this, messageOutput, uint.Parse(target));
            return;
        }

        foreach (var bot in bots)
        {
            var engine = BotHost.Instance.GetRuntime(bot.Id)?.Engines[(int)kind];
            if (engine == null)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot.Name}' has no {kind} engine.");
                continue;
            }

            if (operation == '?')
            {
                CommandManager.SendNormalText(this, messageOutput,
                    $"Bot '{bot.Name}' {kind} Active strategies: {engine.ListStrategies() switch { "" => "(none)", var list => list }}");
                continue;
            }

            foreach (var name in names)
            {
                var changed = operation switch
                {
                    '+' => engine.AddStrategy(name),
                    '-' => engine.RemoveStrategy(name),
                    '~' => engine.ToggleStrategy(name),
                    _ => false
                };
                var verb = operation switch
                {
                    '+' => changed ? "added" : "already active",
                    '-' => changed ? "removed" : "not active",
                    _ => engine.HasStrategy(name) ? "added" : "removed"
                };
                CommandManager.SendNormalText(this, messageOutput,
                    $"Bot '{bot.Name}' {kind} strategy '{name}' {verb}.");
            }
        }
    }

    private static IEnumerable<Character> ResolveBots(string target)
    {
        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
            return BotManager.Instance.GetAllBots();

        return BotManager.Instance.GetBot(uint.Parse(target)) is { } bot
            ? [bot]
            : [];
    }
}
