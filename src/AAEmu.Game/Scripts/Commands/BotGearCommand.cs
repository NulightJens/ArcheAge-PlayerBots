using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
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

    public string GetCommandLineHelp() =>
        "[botId] [show, equip, inspect] OR [botId] create <grade> <prefix> <armor> <weapon>";

    public string GetCommandHelpText() =>
        "Target a live bot or pass its id. Show prints localized equipment names; equip evaluates bag items; " +
        "inspect synchronizes the client detail view; create builds and equips a Magnificent loadout. " +
        "Example: /botgear create celestial flame leather nodachi";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var argumentIndex = 0;
        Character bot = null;
        if (BotCommandArgs.TryBotId(args, 0, out var explicitId, out _))
        {
            bot = BotManager.Instance.GetBot(explicitId);
            argumentIndex = 1;
        }
        else if (character?.CurrentTarget is Character target)
        {
            bot = BotManager.Instance.GetBot(target.Id);
        }
        if (bot == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                "Target a live bot or pass its character id. Example: /botgear 2 show");
            return;
        }

        var operation = args.Length > argumentIndex ? args[argumentIndex].ToLowerInvariant() : "show";
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
            case "create":
                Create(bot, args.Skip(argumentIndex + 1).ToArray(), messageOutput);
                return;
            default:
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
        }
    }

    private void Show(Character bot, IMessageOutput messageOutput)
    {
        var equipped = bot.Inventory?.Equipment?.Items?
            .Where(item => item != null && item.Slot >= 0 && RealEquipmentSlots.Contains((byte)item.Slot))
            .OrderBy(item => item.Slot)
            .ToArray() ?? [];
        var bagEquipment = bot.Inventory?.Bag?.Items?
            .Count(item => item is EquipItem) ?? 0;

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' (Id: {bot.Id}) abilities={TreeSummary(bot)} equipped={equipped.Length} bagItems={bot.Inventory?.Bag?.Items?.Count(item => item != null) ?? 0} gearVisibility=public.");
        foreach (var item in equipped)
        {
            var name = ItemName(item);
            var grade = GradeName(item.Grade);
            CommandManager.SendNormalText(this, messageOutput,
                $"  {EquipmentSlotName(item)}: [{grade}] {name} (template {item.TemplateId}, instance {item.Id})");
        }
        CommandManager.SendNormalText(this, messageOutput,
            $"  bagEquipmentCandidates={bagEquipment}. Use /botgear {bot.Id} equip after adding a kit.");
    }

    private void Create(Character bot, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length != 4)
        {
            CommandManager.SendErrorText(this, messageOutput,
                "Create syntax: /botgear [botId] create <grade> <prefix> <cloth, leather, or plate> <weapon>. " +
                "Example: /botgear create celestial flame leather nodachi");
            return;
        }

        if (!BotGearCatalog.TryResolve(args[0], args[1], args[2], args[3], out var loadout, out var error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        var freeSlots = bot.Inventory?.Bag?.FreeSlotCount ?? 0;
        if (freeSlots < loadout.Items.Count)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Bot '{bot.Name}' needs {loadout.Items.Count} free bag slots for this loadout but has {freeSlots}. No items were created.");
            return;
        }

        var created = new List<BotGearCatalogItem>(loadout.Items.Count);
        foreach (var item in loadout.Items)
        {
            if (!bot.Inventory.Bag.AcquireDefaultItem(ItemTaskType.Gm, item.TemplateId, 1, (byte)loadout.Grade))
            {
                CommandManager.SendErrorText(this, messageOutput,
                    $"Stopped after creating {created.Count} items because {item.Name} could not be added. Existing items were retained.");
                return;
            }
            created.Add(item);
        }

        var state = BotArchetypeManager.Instance.GetState(bot);
        if (state is { IsInitialized: true })
            BotArchetypeManager.Instance.ForceReevaluate(bot);
        bot.SaveDirectlyToDatabase();
        bot.BroadcastPacket(new SCUnitStatePacket(bot), true);

        var equipped = bot.Inventory.Equipment.Items
            .Where(item => item != null)
            .ToArray();
        var equippedFromLoadout = equipped.Count(item =>
            item.Grade == (byte)loadout.Grade && loadout.Items.Any(expected => expected.TemplateId == item.TemplateId));
        var armorPrefix = loadout.Armor[0].Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];

        CommandManager.SendNormalText(this, messageOutput,
            $"Created {created.Count} {loadout.Grade} items for '{bot.Name}': prefix={loadout.RequestedProfile}, " +
            $"armor={loadout.RequestedArmorType} ({armorPrefix}), weapon={loadout.MainWeapon.Name}, " +
            $"bow={loadout.Bow.Name}, instrument={loadout.Instrument.Name}, jewelry={loadout.Necklace.Name}. " +
            $"Equipped matching slots={equippedFromLoadout}; saved and visually refreshed.");
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
            .Count(item => item != null && item.Slot >= 0 && RealEquipmentSlots.Contains((byte)item.Slot)) ?? 0;
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

        requester.SendPacket(new SCUnitStatePacket(bot));
        BotEquipmentVisibility.SendPublicTo(bot, requester);
        requester.SendPacket(new SCCharDetailPacket(bot, true));
        CommandManager.SendNormalText(this, messageOutput,
            $"Published and synchronized the read-only character detail for '{bot.Name}'. Remote drag-and-drop editing is not supported.");
    }

    private static string ItemName(Item item)
    {
        return LocalizationManager.Instance.Get(
            "items",
            "name",
            item.TemplateId,
            item.Template?.Name ?? $"Item {item.TemplateId}");
    }

    private static string GradeName(byte grade)
    {
        return Enum.IsDefined(typeof(ItemGrade), grade) ? ((ItemGrade)grade).ToString() : grade.ToString();
    }

    internal static string EquipmentSlotName(Item item)
    {
        return item != null && item.Slot >= 0 && Enum.IsDefined(typeof(EquipmentItemSlot), (byte)item.Slot)
            ? ((EquipmentItemSlot)item.Slot).ToString()
            : $"Slot {item?.Slot ?? -1}";
    }

    private static string TreeSummary(Character bot) =>
        $"{SetBotClass.TreeName(bot.Ability1)}/{SetBotClass.TreeName(bot.Ability2)}/{SetBotClass.TreeName(bot.Ability3)}";
}
