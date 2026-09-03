using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public sealed class BotRotationCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botrotation"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId|all> {show|reload|set <rotationId>}";

    public string GetCommandHelpText() =>
        "Shows, reloads, or overrides the data rotation attached to a bot.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!TryTarget(args, out var target) || args.Length < 2)
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var operation = args[1].ToLowerInvariant();
        switch (operation)
        {
            case "show" when args.Length == 2:
                Show(target, messageOutput);
                return;
            case "reload" when args.Length == 2:
                Reload(target, messageOutput);
                return;
            case "set" when args.Length == 3 && !string.Equals(target, "all", StringComparison.OrdinalIgnoreCase):
                Set(target, args[2], messageOutput);
                return;
            default:
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
        }
    }

    private void Show(string target, IMessageOutput messageOutput)
    {
        var manager = BotRotationManager.Instance;
        EnsureLoaded(manager);
        var bots = ResolveBots(target).ToArray();
        if (bots.Length == 0)
        {
            SendNoBots(target, messageOutput);
            return;
        }

        foreach (var bot in bots)
        {
            var runtime = BotHost.Instance.GetRuntime(bot.Id);
            if (runtime == null)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot.Name}' has no bot runtime.");
                continue;
            }

            var rotationId = runtime.RotationOverrideId ?? runtime.AttachedRotationId;
            var rotation = manager.GetRotation(rotationId);
            if (rotation == null)
            {
                CommandManager.SendNormalText(this, messageOutput,
                    $"Bot '{bot.Name}' rotation: (none, learned-skill decision fallback)");
                continue;
            }

            var rows = AllRows(rotation).ToArray();
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' rotation={rotation.Id} archetype={rotation.Archetype}");
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' rows filler={CountBand(rows, 11, 12)} normal={CountBand(rows, 12, 30)} " +
                $"move={CountBand(rows, 30, 35)} interrupt={CountBand(rows, 40, 50)} " +
                $"emergency={CountBand(rows, 88, 92)}");

            var strategy = runtime.Engines[(int)BotEngineKind.Combat]?.Strategies.Values
                .OfType<RotationStrategy>()
                .FirstOrDefault();
            var selected = strategy?.Filler.LastSelectedActionNames ?? [];
            if (selected.Count == 0)
            {
                CommandManager.SendNormalText(this, messageOutput,
                    $"Bot '{bot.Name}' rotation rows won: (none)");
                continue;
            }

            foreach (var row in selected.TakeLast(8))
                CommandManager.SendNormalText(this, messageOutput, $"Bot '{bot.Name}' rotation row={row}");
        }
    }

    private void Reload(string target, IMessageOutput messageOutput)
    {
        var manager = BotRotationManager.Instance;
        manager.Reload();
        CommandManager.SendNormalText(this, messageOutput,
            $"Bot rotations reloaded for all live bots; loaded={manager.Rotations.Count}.");
        if (ResolveBots(target).Any())
            return;

        SendNoBots(target, messageOutput);
    }

    private void Set(string target, string rotationId, IMessageOutput messageOutput)
    {
        if (!BotCommandArgs.TryBotId([target], 0, out var botId, out _))
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

        var manager = BotRotationManager.Instance;
        EnsureLoaded(manager);
        if (manager.GetRotation(rotationId) == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Unknown rotation '{rotationId}'. Loaded rotations: {string.Join(',', manager.Rotations.Keys.Order(StringComparer.OrdinalIgnoreCase))}.");
            return;
        }

        var runtime = BotHost.Instance.GetRuntime(botId);
        if (runtime == null || !manager.SetRotation(runtime, rotationId))
        {
            CommandManager.SendErrorText(this, messageOutput, $"Could not attach rotation '{rotationId}' to bot '{bot.Name}'.");
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' rotation set to '{rotationId}'.");
    }

    private static IEnumerable<Character> ResolveBots(string target)
    {
        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
            return BotManager.Instance.GetAllBots();

        return BotManager.Instance.GetBot(uint.Parse(target)) is { } bot
            ? [bot]
            : [];
    }

    private static bool TryTarget(string[] args, out string target)
    {
        target = null;
        if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            return false;
        if (string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            target = "all";
            return true;
        }

        if (!BotCommandArgs.TryBotId(args, 0, out _, out _))
            return false;
        target = args[0];
        return true;
    }

    private static void EnsureLoaded(BotRotationManager manager)
    {
        if (manager.Rotations.Count == 0)
            manager.Load();
    }

    private static IEnumerable<BotRotationRow> AllRows(BotRotationDefinition rotation)
    {
        return (rotation.Default ?? []).Concat((rotation.Rules ?? []).SelectMany(rule => rule?.Then ?? []));
    }

    private static int CountBand(IEnumerable<BotRotationRow> rows, float minimum, float maximum)
    {
        return rows.Count(row => row.Relevance >= minimum && row.Relevance < maximum);
    }

    private void SendNoBots(string target, IMessageOutput messageOutput)
    {
        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
            CommandManager.SendErrorText(this, messageOutput, "No active bots found.");
        else if (uint.TryParse(target, out var botId))
            BotCommandArgs.SendUnknownBot(this, messageOutput, botId);
    }
}
