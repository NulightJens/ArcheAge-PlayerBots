using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public sealed class BotActionsCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botactions"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId> [co|nc]";

    public string GetCommandHelpText() => "Shows the selected engine's last 32 action attempts (default: nc).";

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

        var engineKind = BotEngineKind.NonCombat;
        if (args.Length > 1)
        {
            engineKind = args[1].ToLowerInvariant() switch
            {
                "co" => BotEngineKind.Combat,
                "nc" => BotEngineKind.NonCombat,
                _ => (BotEngineKind)(-1)
            };
            if (!Enum.IsDefined(engineKind))
            {
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
            }
        }

        var engine = BotHost.Instance.GetRuntime(botId)?.Engines[(int)engineKind];
        if (engine == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot.Name}' has no {engineKind} engine.");
            return;
        }

        var actionLog = engine.SnapshotLog();
        if (actionLog.Length == 0)
        {
            CommandManager.SendNormalText(this, messageOutput, $"Bot '{bot.Name}' actions: (none)");
            return;
        }

        foreach (var action in actionLog)
        {
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' action time={action.Time:O} name={action.Action} relevance={action.Relevance:F3} result={action.Result}");
        }
    }
}
