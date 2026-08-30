using System.Globalization;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Queues a native movement jump for one active bot or the current bot population.
/// </summary>
public sealed class BotJumpCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botjump"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId|botName|all>";

    public string GetCommandHelpText() =>
        "Queues a real server-side jump for one active bot or all active bots.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args == null || args.Length != 1 || !TryResolveBots(args[0], out var bots))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        if (bots.Count == 0)
        {
            CommandManager.SendErrorText(this, messageOutput,
                args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
                    ? "No active bots found."
                    : $"No active bot found for selector '{args[0]}'.");
            return;
        }

        var queued = 0;
        foreach (var bot in bots.OrderBy(bot => bot.Id))
        {
            var mover = BotHost.Instance.GetRuntime(bot.Id)?.Mover;
            if (mover?.RequestJump() == true)
                queued++;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Jump queued for {queued}/{bots.Count} active bot(s); skipped bots are airborne, cooling down, casting, impaired, or unavailable.");
    }

    private static bool TryResolveBots(string selector, out List<Character> bots)
    {
        bots = [];
        if (selector.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            bots = BotManager.Instance.GetAllBots();
            return true;
        }

        if (uint.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var botId))
        {
            if (botId == 0)
                return false;

            var botById = BotManager.Instance.GetBot(botId);
            if (botById != null)
                bots.Add(botById);
            return true;
        }

        if (string.IsNullOrWhiteSpace(selector))
            return false;

        var bot = BotManager.Instance.GetAllBots()
            .FirstOrDefault(candidate => candidate.Name.Equals(selector, StringComparison.OrdinalIgnoreCase));
        if (bot != null)
            bots.Add(bot);
        return true;
    }
}
