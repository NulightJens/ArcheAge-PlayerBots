using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Human-facing index for the PlayerBots command surface.
/// </summary>
public sealed class BotHelpCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["bot", "bots", "bothelp"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "[start|party|combat|config|diagnostics|limitations]";

    public string GetCommandHelpText() =>
        "Shows the PlayerBots quick start and topic-based command reference.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args == null || args.Length > 1)
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var topic = args.Length == 0 ? "start" : args[0].ToLowerInvariant();
        switch (topic)
        {
            case "start":
                Send(messageOutput,
                    "PlayerBots quick start: /addbot <characterId> | /botstate <id> grind | /botstate <id> idle | /removebot <id>.",
                    "More help: /bot party, /bot combat, /bot config, /bot diagnostics, /bot limitations.");
                return;
            case "party":
                Send(messageOutput,
                    "Native party: invite the bot, then /botcontrol <id> role <tank|healer|attacker> and /botcontrol <id> <follow|stay|attack|passive>.",
                    "GM direct follow: /botfollow <id|all> <playerName> [rearGap] [columns|auto] [spacing]; stop with /botfollow <id|all> stop.");
                return;
            case "combat":
                Send(messageOutput,
                    "Combat state: /botstate <id> grind [killGoal] | idle | free. Controlled target: /botattackobject <id|all> <npcObjId>.",
                    "Rotations: /botrotation <id|all> show|reload, or /botrotation <id> set <rotationId>. Duels: /botduel <id1> <id2>.");
                return;
            case "config":
            case "develop":
                Send(messageOutput,
                    "Runtime settings: edit Configurations/BotConfig.json beside AAEmu.Game, then run /reloadbotconfig.",
                    "Content: edit Data/BotArchetypes.json or Data/BotRotations/*.json, then run /reloadbotarchetype or /botrotation all reload.");
                return;
            case "diagnostics":
                Send(messageOutput,
                    "Inspect: /botstate <id>, /botdebug <id>, /botactions <id>, /botvalues <id> [filter], /botrotation <id> show.",
                    "Measure: /botmetrics reset before a window, then /botmetrics snapshot. Activity changes use /botmetrics activity <0-100> and are runtime-only.");
                return;
            case "scale":
                Send(messageOutput,
                    "Scale controls: /botmetrics activity <0-100>, /botmetrics reset, /botmetrics snapshot.",
                    "Use modules/archeage-playerbots/scripts/scale for retained measurements; approve a server resource budget before claiming capacity.");
                return;
            case "limitations":
                Send(messageOutput,
                    "Compatibility: ArcheAge 1.2 r208022 is supported. The 3.0.4.2 r336598 track is experimental and server-start-validated only.",
                    "Movement uses direct pursuit and native collision, not navmesh navigation. Jump presentation and stealth search remain experimental.");
                return;
            default:
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
        }
    }

    private void Send(IMessageOutput messageOutput, params string[] lines)
    {
        foreach (var line in lines)
            CommandManager.SendNormalText(this, messageOutput, line);
    }
}
