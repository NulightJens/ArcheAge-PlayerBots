using Newtonsoft.Json;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed class RotationSkillRow
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public int Cost { get; set; }
    public int ManaCost { get; set; }
    public int CooldownTime { get; set; }
    public int CastingTime { get; set; }
    public int MinRange { get; set; }
    public int MaxRange { get; set; }
    public int TargetTypeId { get; set; }
    public int TargetRelationId { get; set; }
    public int TargetAreaRadius { get; set; }
    public int TargetAreaCount { get; set; }
    public int AbilityId { get; set; }
    public int AbilityLevel { get; set; }
    public int? PlotId { get; set; }
    public IReadOnlyList<string> EffectKinds { get; set; } = [];
    public int DamageMax { get; set; }
    public double DamageDps { get; set; }
    public int HealMax { get; set; }
    public double HealDps { get; set; }
    public int BuffDuration { get; set; }
    public double BuffTick { get; set; }

    [JsonIgnore]
    public bool HasEffects => EffectKinds.Count > 0;

    [JsonIgnore]
    public bool IsPlotWithoutEffects => PlotId.HasValue && !HasEffects;
}

public sealed class RotationSkillFixture
{
    public RotationSkillFixtureSource Source { get; set; } = new();
    public List<RotationSkillRow> Rows { get; set; } = [];
}

public sealed class RotationSkillFixtureSource
{
    public string Path { get; set; }
    public string Sha256 { get; set; }
}

public sealed record RotationSynthesizedRow(uint SkillId, string Classification, string Reason, float Relevance, float Weight);

public sealed class RotationSynthesisResult
{
    public BotRotationDefinition Definition { get; init; }
    public IReadOnlyList<RotationSynthesizedRow> Rows { get; init; }
    public IReadOnlyList<uint> SkippedSkillIds { get; init; }
    public int PlotSkillsWithoutEffects { get; init; }
    public int PlotSkillsWithEffects { get; init; }
}

public sealed class RotationSynthesizer
{
    public RotationSynthesisResult Synthesize(
        string id,
        string archetype,
        string role,
        string range,
        IReadOnlyList<uint> learnOrder,
        IReadOnlyDictionary<uint, RotationSkillRow> rows,
        Func<uint, string> skillKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(archetype);
        ArgumentNullException.ThrowIfNull(learnOrder);
        ArgumentNullException.ThrowIfNull(rows);
        skillKey ??= SkillKey;

        var definition = new BotRotationDefinition
        {
            Id = id,
            Archetype = archetype,
            Meta = new BotRotationMeta { Role = role, Range = range }
        };
        foreach (var skillId in learnOrder)
        {
            if (rows.TryGetValue(skillId, out var row))
                definition.Skills[skillKey(skillId)] = skillId;
        }

        var synthesized = new List<RotationSynthesizedRow>();
        var skipped = new List<uint>();
        var claimed = new HashSet<uint>();

        foreach (var row in learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
                     .Where(row => row != null)
                     .Where(IsSelfBuff)
                     .OrderByDescending(row => row.BuffDuration)
                     .ThenBy(row => row.Id))
        {
            AddRule(definition, "buffMissing", row, "castBuff", 29 - synthesized.Count, skillKey(row.Id));
            synthesized.Add(new(row.Id, "buff", "self buff duration >= 10s", 29 - synthesized.Count, 1));
            claimed.Add(row.Id);
        }

        var debuffIndex = 0;
        foreach (var row in learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
                     .Where(row => row != null)
                     .Where(IsHostileDebuff)
                     .OrderByDescending(row => DamagePerCost(row, rows) * Math.Max(row.BuffDuration, 1))
                     .ThenBy(row => row.Id))
        {
            AddRule(definition, "debuffMissing", row, "castDebuff", 28 - debuffIndex, skillKey(row.Id));
            synthesized.Add(new(row.Id, "debuff", "hostile debuff or DoT", 28 - debuffIndex, 1));
            debuffIndex++;
            claimed.Add(row.Id);
        }

        foreach (var row in learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
                     .Where(row => row != null)
                     .Where(row => row.TargetAreaRadius > 0 && row.TargetAreaCount > 1)
                     .OrderBy(row => row.Id))
        {
            AddRule(definition, "enemyCount", row, "castAoe", 27, skillKey(row.Id),
                new Dictionary<string, object> { ["min"] = 3, ["radius"] = row.TargetAreaRadius });
            synthesized.Add(new(row.Id, "aoe", "target area radius > 0 and count > 1", 27, 1));
            claimed.Add(row.Id);
        }

        var cooldownIndex = 0;
        foreach (var row in learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
                     .Where(row => row != null && row.CooldownTime >= 5000 && IsDamage(row))
                     .OrderByDescending(DamagePerCost)
                     .ThenBy(row => row.Id))
        {
            AddRule(definition, "cooldownReady", row, "cast", 22 - cooldownIndex, skillKey(row.Id));
            synthesized.Add(new(row.Id, "cooldown", "cooldown >= 5000ms, damage per cost", 22 - cooldownIndex, 1));
            cooldownIndex++;
            claimed.Add(row.Id);
        }

        if (range is "ranged" or "spell")
        {
            var rangeRow = learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
                .Where(row => row != null && row.MaxRange > 0)
                .OrderByDescending(row => row.MaxRange)
                .ThenBy(row => row.Id)
                .FirstOrDefault();
            if (rangeRow != null)
            {
                definition.Rules.Add(new BotRotationRule
                {
                    When = When("range", new Dictionary<string, object> { ["to"] = "target", ["min"] = rangeRow.MinRange, ["max"] = rangeRow.MaxRange }),
                    Then = [new BotRotationRow { Action = "reachAndCast", Skill = skillKey(rangeRow.Id), Relevance = 31,
                        As = $"range:{skillKey(rangeRow.Id)}" }]
                });
                synthesized.Add(new(rangeRow.Id, "range", "archetype weapon range", 31, 1));
            }
        }
        else
        {
            var rangeRow = learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
                .FirstOrDefault(row => row != null);
            definition.Rules.Add(new BotRotationRule
            {
                When = When("range", new Dictionary<string, object> { ["to"] = "target", ["min"] = 0, ["max"] = 4 }),
                Then = [new BotRotationRow { Action = "reachAndCast", Skill = skillKey(rangeRow.Id), Relevance = 31,
                    As = $"range:{skillKey(rangeRow.Id)}" }]
            });
        }

        var fillerRows = learnOrder.Select(id => rows.TryGetValue(id, out var value) ? value : null)
            .Where(row => row != null && !claimed.Contains(row.Id) && row.CooldownTime == 0 && IsDamage(row))
            .OrderBy(row => row.IsPlotWithoutEffects ? row.Cost : int.MaxValue)
            .ThenByDescending(row => DamagePerCost(row, rows))
            .ThenBy(row => row.Id)
            .ToArray();
        foreach (var row in fillerRows)
        {
            definition.Default.Add(new BotRotationRow
            {
                Action = "cast",
                Skill = skillKey(row.Id),
                Relevance = 11,
                Weight = (float)Math.Max(DamagePerCost(row, rows), 1)
            });
            synthesized.Add(new(row.Id, "filler", "zero cooldown damage", 11, (float)Math.Max(DamagePerCost(row, rows), 1)));
            claimed.Add(row.Id);
        }

        var plotWithoutEffects = learnOrder.Count(skillId => rows.TryGetValue(skillId, out var row) && row.IsPlotWithoutEffects);
        var plotWithEffects = learnOrder.Count(skillId => rows.TryGetValue(skillId, out var row) && row.PlotId.HasValue && row.HasEffects);
        skipped.AddRange(learnOrder.Where(skillId => !rows.ContainsKey(skillId)));
        return new RotationSynthesisResult
        {
            Definition = definition,
            Rows = synthesized,
            SkippedSkillIds = skipped,
            PlotSkillsWithoutEffects = plotWithoutEffects,
            PlotSkillsWithEffects = plotWithEffects
        };
    }

    public static string Serialize(BotRotationDefinition definition)
    {
        return JsonConvert.SerializeObject(definition, Formatting.Indented) + Environment.NewLine;
    }

    public static string Classify(RotationSkillRow row)
    {
        if (row == null)
            return "skipped";
        if (row.IsPlotWithoutEffects)
            return "damage";
        if (row.EffectKinds.Contains("HealEffect", StringComparer.OrdinalIgnoreCase))
            return "heal";
        if (row.EffectKinds.Contains("DamageEffect", StringComparer.OrdinalIgnoreCase))
            return "damage";
        if (row.EffectKinds.Contains("BuffEffect", StringComparer.OrdinalIgnoreCase))
            return row.TargetRelationId <= 1 ? "buff" : "debuff";
        return "utility";
    }

    public static double DamagePerCost(RotationSkillRow row)
    {
        if (row == null)
            return 0;
        if (row.DamageMax == 99999)
            return 0;
        var damage = row.DamageMax > 0 ? row.DamageMax : row.DamageDps;
        return damage / Math.Max(Math.Max(row.Cost, row.ManaCost), 1);
    }

    private static double DamagePerCost(RotationSkillRow row, IReadOnlyDictionary<uint, RotationSkillRow> rows)
    {
        if (row == null)
            return 0;
        var damage = row.DamageMax > 0 ? row.DamageMax : row.DamageDps;
        if (row.DamageMax == 99999)
            damage = rows.Values.Where(candidate => candidate.Id != row.Id)
                .Select(candidate => candidate.DamageMax > 0 ? candidate.DamageMax : candidate.DamageDps)
                .Where(value => value > 0 && value < 99999)
                .OrderByDescending(value => value)
                .FirstOrDefault();
        return damage / Math.Max(Math.Max(row.Cost, row.ManaCost), 1);
    }

    private static string SkillKey(uint skillId) => skillId switch
    {
        10082 => "stealth",
        10104 => "leech",
        10135 => "hellSpear",
        10151 => "freezingEarth",
        10152 => "teleportation",
        10153 => "insulatingLens",
        10189 => "freerunner",
        10201 => "cripplingMire",
        10372 => "invincibility",
        10375 => "redoubt",
        10377 => "battleFocus",
        10399 => "shieldSlam",
        10436 => "mockingHowl",
        10481 => "toxicShot",
        10501 => "bullRush",
        10534 => "antithesis",
        10547 => "resurgence",
        10644 => "sunderEarth",
        10645 => "refreshment",
        10648 => "overwhelm",
        10664 => "meteorStrike",
        10667 => "freezingArrow",
        10670 => "arcLightning",
        10694 => "float",
        10720 => "mend",
        10752 => "flamebolt",
        11314 => "frigidTracks",
        11365 => "toughen",
        11368 => "doubleRecurve",
        11379 => "mirrorLight",
        11380 => "liberation",
        11395 => "summonCrows",
        11429 => "shrugItOff",
        11918 => "charge",
        11933 => "concussiveArrow",
        11939 => "searingRain",
        11967 => "chainLightning",
        11991 => "healthLift",
        12034 => "bondbreaker",
        12039 => "lasso",
        12046 => "revitalizingCheer",
        12048 => "boastfulRoar",
        12049 => "dropBack",
        12075 => "shadowStep",
        12133 => "snare",
        12139 => "stalkersMark",
        12759 => "manaForce",
        12796 => "magicCircle",
        13281 => "missileRain",
        13282 => "whirlwindSlash",
        13286 => "skewer",
        13564 => "piercingShot",
        14760 => "boneyard",
        14774 => "flameBarrier",
        14835 => "endlessArrows",
        14929 => "ferventHealing",
        15073 => "deadeye",
        16004 => "aranzebsBoon",
        16210 => "chargedBolt",
        16486 => "thwart",
        16783 => "infuse",
        17412 => "renewal",
        18131 => "tripleSlash",
        _ => $"id_{skillId}"
    };

    private static bool IsDamage(RotationSkillRow row) => Classify(row) == "damage";

    private static bool IsSelfBuff(RotationSkillRow row) =>
        Classify(row) == "buff" && row.BuffDuration >= 10000 && row.TargetRelationId <= 1;

    private static bool IsHostileDebuff(RotationSkillRow row) =>
        Classify(row) == "debuff" || row.EffectKinds.Contains("DamageEffect", StringComparer.OrdinalIgnoreCase) &&
        row.BuffDuration > 0 && row.BuffTick > 0 && row.TargetRelationId > 1;

    private static void AddRule(BotRotationDefinition definition, string kind, RotationSkillRow row, string action,
        float relevance, string skill, IReadOnlyDictionary<string, object> arguments = null)
    {
        var whenArguments = arguments == null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(arguments, StringComparer.OrdinalIgnoreCase);
        if (kind is "buffMissing" or "debuffMissing")
            whenArguments["spell"] = skill;
        else if (kind is "canCast" or "cooldownReady")
            whenArguments["skill"] = skill;

        definition.Rules.Add(new BotRotationRule
        {
            When = When(kind, whenArguments),
            Then = [new BotRotationRow { Action = action, Skill = skill, Relevance = relevance,
                As = $"{kind}:{skill}" }]
        });
    }

    private static BotRotationWhen When(string kind, IReadOnlyDictionary<string, object> arguments)
    {
        var when = new BotRotationWhen { Kind = kind };
        if (arguments != null)
            foreach (var argument in arguments)
                when.Arguments[argument.Key] = Newtonsoft.Json.Linq.JToken.FromObject(argument.Value);
        return when;
    }
}
