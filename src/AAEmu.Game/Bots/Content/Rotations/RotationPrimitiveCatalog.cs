namespace AAEmu.Game.Bots.Content.Rotations;

public static class RotationPrimitiveCatalog
{
    public static IReadOnlySet<string> TriggerKinds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "buffMissing", "debuffMissing", "hasAura", "hasNoAura", "canCast", "cooldownReady", "resource",
        "healthBand", "enemyCount", "range", "targetCasting", "controlled", "timer", "all", "any",
        "comboActive", "chainStep", "pvp", "hasCleansableDebuff", "stunned", "not", "groupCooldown",
        "partyLowest", "hasTarget"
    };

    public static IReadOnlySet<string> ActionKinds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cast", "castMelee", "castBuff", "castDebuff", "castAoe", "castHeal", "reachAndCast", "maintainRange", "move", "autoAttack"
    };

    public static IReadOnlySet<string> ValueKinds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "self", "target", "attackers", "stat", "distance", "enemyCount", "aoePosition", "comboState", "stalkerActive"
    };

    public static IReadOnlySet<string> DeferredKinds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "threat", "dispellable", "partyMemberDead", "needsProtection", "boostWorthwhile",
        "castAoeHeal", "castCure", "castOn",
        "attackerWithoutAura", "snareTarget", "enemyHealer", "ccTarget",
        "partyWithoutAura", "partyToDispel", "mainTank", "groupDps"
    };
}
