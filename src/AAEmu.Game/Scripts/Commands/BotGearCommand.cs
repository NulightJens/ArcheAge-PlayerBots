using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Inspects or refreshes a bot's server-owned equipment without pretending the GM owns its inventory.
/// </summary>
public sealed class BotGearCommand : ICommand
{
    private static readonly HashSet<byte> RealEquipmentSlots =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18];

    public string[] CommandNames { get; set; } = ["botgear", "botequip"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId> [show|equip|inspect]";

    public string GetCommandHelpText() =>
        "Shows bot equipment, equips the best compatible items already in its bag, or opens the client's read-only " +
        "character-detail sheet. Use /kit <botName> <kitName> first; bot kits are auto-equipped when possible.";

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

        var operation = args.Length > 1 ? args[1].ToLowerInvariant() : "show";
        switch (operation)
        {
            case "show":
                Show(bot, messageOutput);
                return;
            case "equip":
            case "refresh":
            case "best":
                Equip(bot, messageOutput);
                return;
            case "inspect":
            case "open":
                Inspect(character, bot, messageOutput);
                return;
            default:
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
        }
    }

    private void Show(Character bot, IMessageOutput messageOutput)
    {
        var equipped = bot.Inventory?.Equipment?.Items?
            .Select((item, index) => new { item, index })
            .Where(entry => entry.item != null && RealEquipmentSlots.Contains((byte)entry.index))
            .ToArray() ?? [];
        var bagEquipment = bot.Inventory?.Bag?.Items?
            .Count(item => item is EquipItem) ?? 0;

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' (Id: {bot.Id}) abilities={TreeSummary(bot)} equipped={equipped.Length} bagItems={bot.Inventory?.Bag?.Items?.Count(item => item != null) ?? 0}.");
        foreach (var entry in equipped)
        {
            CommandManager.SendNormalText(this, messageOutput,
                $"  slot={entry.index} item={entry.item.TemplateId} grade={entry.item.Grade} instance={entry.item.Id}");
        }
        CommandManager.SendNormalText(this, messageOutput,
            $"  bagEquipmentCandidates={bagEquipment}. Use /botgear {bot.Id} equip after adding a kit.");
    }

    private void Equip(Character bot, IMessageOutput messageOutput)
    {
        var state = BotArchetypeManager.Instance.GetState(bot);
        if (state is not { IsInitialized: true })
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Bot '{bot.Name}' has no initialized archetype. Run /setclass {bot.Id} <archetype> first.");
            return;
        }

        BotArchetypeManager.Instance.ForceReevaluate(bot);
        bot.SaveDirectlyToDatabase();
        var equipped = bot.Inventory?.Equipment?.Items?
            .Select((item, index) => new { item, index })
            .Count(entry => entry.item != null && RealEquipmentSlots.Contains((byte)entry.index)) ?? 0;
        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' equipment refreshed from its bag for {state.ArchetypeName ?? state.PlannedArchetype}; equipped={equipped}.");
    }

    private void Inspect(Character requester, Character bot, IMessageOutput messageOutput)
    {
        if (requester?.Connection == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                "Inspect requires an in-game GM client; the Web API system actor has no client window.");
            return;
        }

        requester.SendPacket(new SCCharDetailPacket(bot, true));
        CommandManager.SendNormalText(this, messageOutput,
            $"Opened the read-only character detail sheet for '{bot.Name}'. Equip changes must use /kit and /botgear because the client cannot edit another character's inventory.");
    }

    private static string TreeSummary(Character bot) =>
        $"{SetBotClass.TreeName(bot.Ability1)}/{SetBotClass.TreeName(bot.Ability2)}/{SetBotClass.TreeName(bot.Ability3)}";
}
