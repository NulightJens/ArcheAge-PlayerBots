using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Human-facing deterministic class refresh for clientless characters.
/// </summary>
public sealed class SetBotClass : ICommand
{
    public string[] CommandNames { get; set; } = ["setclass", "botsetclass", "setarchetype"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "[botId] <archetype> [level]";

    public string GetCommandHelpText() =>
        "Sets all three bot ability trees from a named archetype, synchronizes skills/gear, persists the result, " +
        "and respawns the bot so nearby clients receive a fresh character sheet. With no arguments, lists archetypes.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var manager = BotArchetypeManager.Instance;
        var archetypeNames = manager.GetArchetypeNames();
        if (args.Length == 0)
        {
            CommandManager.SendNormalText(this, messageOutput,
                $"Available bot archetypes: {string.Join(", ", archetypeNames)}");
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var argumentIndex = 0;
        Character bot = null;
        if (uint.TryParse(args[0], out var explicitId))
        {
            bot = BotManager.Instance.GetBot(explicitId);
            argumentIndex = 1;
        }
        else if (character?.CurrentTarget is Character target && BotManager.Instance.GetBot(target.Id) != null)
        {
            bot = BotManager.Instance.GetBot(target.Id);
        }

        if (bot == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                "Target a live bot or pass its character id: /setclass [botId] <archetype> [level]");
            return;
        }

        if (bot.IsInDuel)
        {
            CommandManager.SendErrorText(this, messageOutput,
                "The bot is in a duel; end the duel before changing its class.");
            return;
        }

        if (argumentIndex >= args.Length)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Missing archetype. Available: {string.Join(", ", archetypeNames)}");
            return;
        }

        var archetype = args[argumentIndex];
        byte level = 0;
        if (args.Length > argumentIndex + 1 &&
            (!byte.TryParse(args[argumentIndex + 1], out level) || level < 2))
        {
            CommandManager.SendErrorText(this, messageOutput, "Level must be a number from 2 to the server level cap.");
            return;
        }

        if (args.Length > argumentIndex + 2)
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        if (!manager.SetArchetype(bot, archetype, level, out var resolvedName))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Unknown archetype '{archetype}'. Available: {string.Join(", ", archetypeNames)}");
            return;
        }

        var botId = bot.Id;
        var botName = bot.Name;
        var despawned = BotManager.Instance.DespawnBot(botId);
        var refreshed = despawned ? BotManager.Instance.SpawnBot(botId) : null;
        var result = refreshed ?? bot;
        var trees = $"{TreeName(result.Ability1)}/{TreeName(result.Ability2)}/{TreeName(result.Ability3)}";
        var skillCount = result.Skills?.Skills?.Count ?? 0;
        var passiveCount = result.Skills?.PassiveBuffs?.Count ?? 0;

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{botName}' (Id: {botId}) is now {resolvedName} [{trees}], level {result.Level}, " +
            $"skills {skillCount}, passives {passiveCount}" +
            (refreshed != null ? "; persisted and respawned." : "; persisted, but automatic respawn failed."));
    }

    internal static string TreeName(AbilityType ability) => ability switch
    {
        AbilityType.Fight => "Battlerage",
        AbilityType.Illusion => "Witchcraft",
        AbilityType.Adamant => "Defense",
        AbilityType.Will => "Auramancy",
        AbilityType.Death => "Occultism",
        AbilityType.Wild => "Archery",
        AbilityType.Magic => "Sorcery",
        AbilityType.Vocation => "Shadowplay",
        AbilityType.Romance => "Songcraft",
        AbilityType.Love => "Vitalism",
        _ => ability.ToString()
    };
}
