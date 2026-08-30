using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.Items.Templates;

namespace AAEmu.Game.Scripts.Commands;

internal sealed record BotGearCatalogItem(uint TemplateId, string Name, EquipItemTemplate Template);

internal sealed record BotGearLoadout(
    ItemGrade Grade,
    string RequestedProfile,
    string RequestedArmorType,
    string RequestedWeaponType,
    IReadOnlyList<BotGearCatalogItem> Armor,
    BotGearCatalogItem Necklace,
    BotGearCatalogItem Earring,
    BotGearCatalogItem Ring,
    BotGearCatalogItem MainWeapon,
    BotGearCatalogItem Bow,
    BotGearCatalogItem Instrument)
{
    public IReadOnlyList<BotGearCatalogItem> Items =>
        Armor
            .Concat([Necklace, Earring, Earring, Ring, Ring, MainWeapon, Bow, Instrument])
            .ToArray();
}

internal static class BotGearCatalog
{
    private static readonly EquipmentItemSlot[] ArmorSlots =
    [
        EquipmentItemSlot.Head,
        EquipmentItemSlot.Chest,
        EquipmentItemSlot.Legs,
        EquipmentItemSlot.Hands,
        EquipmentItemSlot.Feet,
        EquipmentItemSlot.Waist,
        EquipmentItemSlot.Arms
    ];

    internal static bool TryParseGrade(string value, out ItemGrade grade)
    {
        return Enum.TryParse(value, true, out grade) && Enum.IsDefined(grade);
    }

    internal static string NormalizeProfile(string value)
    {
        var normalized = NormalizeToken(value);
        return normalized == "wind" ? "gale" : normalized;
    }

    internal static string NormalizeToken(string value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    public static bool TryResolve(
        string gradeText,
        string profileText,
        string armorText,
        string weaponText,
        out BotGearLoadout loadout,
        out string error)
    {
        loadout = null;
        error = null;

        if (!TryParseGrade(gradeText, out var grade))
        {
            error = $"Unknown grade '{gradeText}'. Grades: {string.Join(", ", Enum.GetNames<ItemGrade>())}.";
            return false;
        }

        var profile = NormalizeProfile(profileText);
        if (!TryArmorType(armorText, out var armorType, out var armorName))
        {
            error = $"Unknown armor type '{armorText}'. Armor types: cloth, leather, plate.";
            return false;
        }

        var requestedWeapon = NormalizeToken(weaponText);
        if (requestedWeapon.Length == 0)
        {
            error = "A weapon type is required, for example nodachi, greatsword, greataxe, staff, or club.";
            return false;
        }

        var magnificent = ItemManager.Instance.GetAllItems()
            .OfType<EquipItemTemplate>()
            .Select(template => new BotGearCatalogItem(template.Id, LocalizedName(template), template))
            .Where(item => item.Name.StartsWith("Magnificent ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var profileTitle = ToTitle(profile);
        var necklace = FindExact<AccessoryTemplate>(magnificent, $"Magnificent {profileTitle} Necklace");
        var earring = FindExact<AccessoryTemplate>(magnificent, $"Magnificent {profileTitle} Earring");
        var ring = FindExact<AccessoryTemplate>(magnificent, $"Magnificent {profileTitle} Ring");
        if (necklace == null || earring == null || ring == null)
        {
            error = $"Prefix '{profileText}' has no complete Magnificent jewelry family. Prefixes: earth, flame, gale (wind), life, wave.";
            return false;
        }

        var mainWeapon = magnificent
            .Where(item => item.Template is WeaponTemplate)
            .Where(IsMainWeapon)
            .FirstOrDefault(item =>
                item.Name.StartsWith($"Magnificent {profileTitle} ", StringComparison.OrdinalIgnoreCase) &&
                NormalizeToken(item.Name[("Magnificent " + profileTitle + " ").Length..]) == requestedWeapon);
        if (mainWeapon == null)
        {
            var available = magnificent
                .Where(item => item.Template is WeaponTemplate && IsMainWeapon(item))
                .Where(item => item.Name.StartsWith($"Magnificent {profileTitle} ", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Name[("Magnificent " + profileTitle + " ").Length..])
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            error = available.Length == 0
                ? $"Prefix '{profileText}' has no Magnificent primary weapons in this data pack."
                : $"No Magnificent {profileTitle} {weaponText}. Available {profileTitle} weapons: {string.Join(", ", available)}.";
            return false;
        }

        var desired = GetVector(mainWeapon);
        if (desired.MagnitudeSquared == 0)
        {
            error = $"Could not determine the stat profile for {mainWeapon.Name}.";
            return false;
        }

        var armorGroups = magnificent
            .Where(item => item.Template is ArmorTemplate armor && armor.KindTemplate?.TypeId == armorType)
            .Where(item => !item.Name.Contains("Vocational", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Template.ModSetId)
            .Select(group => BuildArmorGroup(group.ToArray()))
            .Where(group => group != null)
            .ToArray();

        var exactArmor = armorGroups.FirstOrDefault(group => group.All(item =>
            item.Name.StartsWith($"Magnificent {profileTitle} ", StringComparison.OrdinalIgnoreCase)));
        var armor = exactArmor ?? armorGroups
            .Select(group => new { Group = group, Score = Similarity(desired, GetVector(group[0])) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Group[0].Name, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Group)
            .FirstOrDefault();
        if (armor == null)
        {
            error = $"No stat-compatible Magnificent {armorName} set exists for the {profileTitle} profile in this data pack.";
            return false;
        }

        var bow = SelectClosest(
            magnificent.Where(item => item.Template is WeaponTemplate && item.Name.EndsWith(" Bow", StringComparison.OrdinalIgnoreCase)),
            desired);
        var instrument = SelectClosest(
            magnificent.Where(item => item.Template is WeaponTemplate &&
                (item.Name.EndsWith(" Lute", StringComparison.OrdinalIgnoreCase) ||
                 item.Name.EndsWith(" Flute", StringComparison.OrdinalIgnoreCase))),
            desired);
        if (bow == null || instrument == null)
        {
            error = $"No stat-compatible Magnificent bow or instrument exists for the {profileTitle} profile.";
            return false;
        }

        loadout = new BotGearLoadout(
            grade,
            profileTitle,
            armorName,
            mainWeapon.Name[("Magnificent " + profileTitle + " ").Length..],
            armor,
            necklace,
            earring,
            ring,
            mainWeapon,
            bow,
            instrument);
        return true;
    }

    private static string LocalizedName(EquipItemTemplate template)
    {
        return LocalizationManager.Instance.Get("items", "name", template.Id, template.Name ?? $"Item {template.Id}");
    }

    private static BotGearCatalogItem FindExact<T>(IEnumerable<BotGearCatalogItem> items, string name)
        where T : EquipItemTemplate
    {
        return items.FirstOrDefault(item => item.Template is T &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMainWeapon(BotGearCatalogItem item)
    {
        var slots = EquipmentContainer.GetAllowedGearSlots(item.Template);
        return slots.Contains(EquipmentItemSlot.Mainhand) &&
               !slots.Contains(EquipmentItemSlot.Ranged) &&
               !slots.Contains(EquipmentItemSlot.Musical);
    }

    private static IReadOnlyList<BotGearCatalogItem> BuildArmorGroup(IReadOnlyList<BotGearCatalogItem> items)
    {
        var result = new List<BotGearCatalogItem>(ArmorSlots.Length);
        foreach (var slot in ArmorSlots)
        {
            var item = items.FirstOrDefault(candidate =>
                EquipmentContainer.GetAllowedGearSlots(candidate.Template).Contains(slot));
            if (item == null)
                return null;
            result.Add(item);
        }
        return result;
    }

    private static BotGearCatalogItem SelectClosest(IEnumerable<BotGearCatalogItem> items, AttributeVector desired)
    {
        return items
            .Select(item => new { Item = item, Score = Similarity(desired, GetVector(item)) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Item)
            .FirstOrDefault();
    }

    private static AttributeVector GetVector(BotGearCatalogItem item)
    {
        if (item?.Template == null || item.Template.ModSetId == 0)
            return default;

        try
        {
            var modifiers = ItemManager.Instance.GetAttributeModifiers(item.Template.ModSetId);
            return new AttributeVector(
                modifiers.StrWeight,
                modifiers.DexWeight,
                modifiers.StaWeight,
                modifiers.IntWeight,
                modifiers.SpiWeight);
        }
        catch (KeyNotFoundException)
        {
            return default;
        }
    }

    internal static double Similarity(AttributeVector left, AttributeVector right)
    {
        if (left.MagnitudeSquared == 0 || right.MagnitudeSquared == 0)
            return 0;
        return left.Dot(right) / Math.Sqrt(left.MagnitudeSquared * right.MagnitudeSquared);
    }

    private static bool TryArmorType(string value, out uint armorType, out string name)
    {
        switch (NormalizeToken(value))
        {
            case "cloth":
                armorType = (uint)ArmorType.Cloth;
                name = "Cloth";
                return true;
            case "leather":
                armorType = (uint)ArmorType.Leather;
                name = "Leather";
                return true;
            case "plate":
            case "metal":
                armorType = (uint)ArmorType.Metal;
                name = "Plate";
                return true;
            default:
                armorType = 0;
                name = null;
                return false;
        }
    }

    private static string ToTitle(string value)
    {
        return value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
    }

    internal readonly record struct AttributeVector(int Str, int Dex, int Sta, int Int, int Spi)
    {
        public int MagnitudeSquared => Str * Str + Dex * Dex + Sta * Sta + Int * Int + Spi * Spi;
        public int Dot(AttributeVector other) =>
            Str * other.Str + Dex * other.Dex + Sta * other.Sta + Int * other.Int + Spi * other.Spi;
    }
}
