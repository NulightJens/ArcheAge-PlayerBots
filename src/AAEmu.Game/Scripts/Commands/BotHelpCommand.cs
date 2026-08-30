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

    public string GetCommandLineHelp() => "[start|party|combat|develop|diagnostics|scale|limitations]";

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
                    "More help: /bot party, /bot combat, /bot develop, /bot diagnostics, /bot scale, /bot limitations.");
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
            case "develop":
                Send(messageOutput,
                    "Edit Data/BotArchetypes.json and Data/BotRotations/*.json, then run /reloadbotarchetype and /botrotation all reload.",
                    "Edit Configurations/BotConfig.json, then run /reloadbotconfig. Use /botarchetype <id> force to re-evaluate equipment and learned skills.");
                return;
            case "diagnostics":
                Send(messageOutput,
                    "Inspect: /botstate <id>, /botdebug <id>, /botactions <id>, /botvalues <id> [filter], /botrotation <id> show.",
                    "Measure: /botmetrics reset before a window, then /botmetrics snapshot. Stop a case with /botstate <id> idle.",
                    "Controlled buffs: /botbuff <id> <buffId> [abLevel]; remove with /botbuff <id> -<buffId>. No selected target is required.");
                return;
            case "scale":
                Send(messageOutput,
                    "Scale controls: /botmetrics activity <0-100>, /botmetrics reset, /botmetrics snapshot.",
                    "Use Scripts/Benchmarks/ScaleGate for retained 0/10/50/100 measurements; a resource budget is required before claiming capacity.");
                return;
            case "limitations":
                Send(messageOutput,
                    "Current limits: ArcheAge 1.2 r208022 only; movement is not a navmesh proof; jump animation and stealth-search behavior remain experimental.",
                    "Native ArcheAge collision and direct pursuit are the default. Pairwise crowd repulsion and boss orbit logic are intentionally not enabled.");
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
