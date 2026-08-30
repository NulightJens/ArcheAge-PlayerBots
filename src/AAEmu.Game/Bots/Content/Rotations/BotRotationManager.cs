using System.Collections.Concurrent;
using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Bots;
using Newtonsoft.Json;
using NLog;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed class BotRotationManager : Singleton<BotRotationManager>, ILoadable
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private static readonly HashSet<string> s_actionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "cast", "castMelee", "castBuff", "castDebuff", "castAoe", "castHeal", "reachAndCast", "maintainRange", "move", "autoAttack"
    };
    private static readonly HashSet<string> s_triggerKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "buffMissing", "debuffMissing", "hasAura", "hasNoAura", "canCast", "cooldownReady", "resource",
        "healthBand", "enemyCount", "range", "targetCasting", "controlled", "timer", "all", "any",
        "comboActive", "chainStep", "pvp", "hasCleansableDebuff", "stunned", "not", "groupCooldown",
        "partyLowest", "hasTarget"
    };
    private static readonly HashSet<string> s_rangeModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "melee", "spellRange", "behind", "facing", "away", "flee"
    };

    private readonly ConcurrentDictionary<string, BotRotationDefinition> _rotations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<uint, bool> _skillExists;
    private readonly Func<string, IReadOnlyCollection<uint>> _learnOrder;
    private readonly Func<uint, SkillTemplate> _templateResolver;
    private int _version;

    public BotRotationManager()
        : this(null, null, null)
    {
    }

    public BotRotationManager(Func<uint, bool> skillExists, Func<string, IReadOnlyCollection<uint>> learnOrder,
        Func<uint, SkillTemplate> templateResolver = null)
    {
        _skillExists = skillExists;
        _learnOrder = learnOrder;
        _templateResolver = templateResolver;
    }

    public IReadOnlyList<BotRotationValidationError> LastErrors { get; private set; } = [];
    public IReadOnlyDictionary<string, BotRotationDefinition> Rotations => _rotations;
    public int Version => Volatile.Read(ref _version);

    public void Load()
    {
        var directory = Path.Combine(FileManager.AppPath, "Data", "BotRotations");
        if (!Directory.Exists(directory))
        {
            Logger.Warn($"BotRotations directory not found at {directory}; rotations fall back to legacy tick.");
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            try
            {
                LoadRotations(File.ReadAllText(path), id);
            }
            catch (Exception error)
            {
                Logger.Warn(error, $"Bot rotation '{id}' could not be read; falling back to legacy tick.");
            }
        }
    }

    public bool Reload()
    {
        _rotations.Clear();
        Load();
        foreach (var runtime in BotHost.Instance.GetRuntimeSnapshot())
        {
            runtime.AttachedRotationId = null;
            runtime.AttachedRotationVersion = 0;
            runtime.AttachedRotationArchetype = null;
            var archetype = BotArchetypeManager.Instance.GetState(runtime.Bot)?.ArchetypeName ??
                             runtime.CombatState.ActiveArchetype;
            if (!string.IsNullOrWhiteSpace(archetype))
                EnsureAttached(runtime, archetype);
        }

        return true;
    }

    public bool LoadRotations(string json, string id)
    {
        var errors = new List<BotRotationValidationError>();
        BotRotationDefinition rotation;
        try
        {
            rotation = JsonConvert.DeserializeObject<BotRotationDefinition>(json);
        }
        catch (Exception error)
        {
            errors.Add(new("InvalidJson", error.Message));
            LastErrors = errors;
            return false;
        }

        if (rotation == null)
            errors.Add(new("InvalidJson", "rotation document is null"));
        else
        {
            rotation.Id ??= id;
            Validate(rotation, errors);
        }

        LastErrors = errors;
        if (errors.Count != 0 || rotation == null)
        {
            Logger.Warn($"Bot rotation '{id}' skipped: {string.Join(", ", errors.Select(error => error.Code))}.");
            return false;
        }

        _rotations[id] = rotation;
        Interlocked.Increment(ref _version);
        return true;
    }

    public BotRotationDefinition GetRotation(string id)
    {
        return id != null && _rotations.TryGetValue(id, out var rotation) ? rotation : null;
    }

    public BotRotationDefinition GetRotationForArchetype(string archetype)
    {
        if (string.IsNullOrWhiteSpace(archetype))
            return null;
        return _rotations.Values.FirstOrDefault(rotation => string.Equals(rotation.Archetype, archetype, StringComparison.OrdinalIgnoreCase));
    }

    public void Remove(string id)
    {
        if (id != null)
            _rotations.TryRemove(id, out _);
    }

    public RotationStrategy Compile(string id, IBotMover mover = null, Func<int> roll = null,
        Func<uint, SkillTemplate> templateResolver = null, Func<BotCastRequest, SkillResult> cast = null)
    {
        var rotation = GetRotation(id);
        return rotation == null
            ? null
            : new BotRotationCompiler(roll, templateResolver ?? _templateResolver, mover, cast).Compile(rotation);
    }

    public bool EnsureAttached(BotRuntime runtime, string archetypeName)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(archetypeName))
            return false;

        if (runtime.AttachedRotationVersion == Version &&
            runtime.AttachedRotationArchetype is not null &&
            string.Equals(runtime.AttachedRotationArchetype, archetypeName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (_rotations.IsEmpty)
            Load();

        var definition = BotArchetypeManager.Instance.GetEffectiveDefinition(new BotArchetypeState
        {
            ArchetypeName = archetypeName
        });
        var rotationId = runtime.RotationOverrideId ?? definition?.RotationId ?? GetRotationForArchetype(archetypeName)?.Id;
        if (string.IsNullOrWhiteSpace(rotationId))
            return false;

        if (runtime.AttachedRotationId == rotationId && runtime.AttachedRotationVersion == Version)
            return true;

        var attached = Attach(runtime, rotationId);
        if (attached)
            runtime.AttachedRotationArchetype = archetypeName;
        return attached;
    }

    public bool SetRotation(BotRuntime runtime, string rotationId)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(rotationId) || GetRotation(rotationId) == null)
            return false;

        runtime.RotationOverrideId = rotationId;
        runtime.AttachedRotationId = null;
        runtime.AttachedRotationVersion = 0;
        return Attach(runtime, rotationId);
    }

    private bool Attach(BotRuntime runtime, string rotationId)
    {
        var strategy = Compile(rotationId, runtime.Mover);
        if (strategy == null)
            return false;

        var engine = runtime.Engines[(int)BotEngineKind.Combat];
        if (engine == null)
            return false;

        foreach (var action in strategy.Actions)
            engine.RegisterAction(action);
        engine.RegisterAction(strategy.Filler);
        engine.AddStrategy(strategy);
        runtime.AttachedRotationId = rotationId;
        runtime.AttachedRotationVersion = Version;
        return true;
    }

    internal static bool IsActionKind(string kind) => kind != null && s_actionKinds.Contains(kind);
    internal static bool IsTriggerKind(string kind) => kind != null && s_triggerKinds.Contains(kind);

    private void Validate(BotRotationDefinition rotation, List<BotRotationValidationError> errors)
    {
        var actionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var learnOrder = ResolveLearnOrder(rotation.Archetype);

        foreach (var key in rotation.ExtensionData.Keys)
        {
            if (string.Equals(key, "replay", StringComparison.OrdinalIgnoreCase))
                Add(errors, "UnknownReplayKey", "replay is no longer supported");
            else
                Add(errors, "UnknownTopLevelKey", key);
        }

        foreach (var pair in rotation.Skills ?? [])
        {
            if (!SkillExists(pair.Value))
                Add(errors, "SkillIdNotInTemplates", $"{pair.Key}={pair.Value}");
            if (learnOrder != null && !learnOrder.Contains(pair.Value))
                Add(errors, "SkillNotInArchetypeLearnOrder", $"{pair.Key}={pair.Value}");
        }

        var homeAnchorSkill = rotation.Meta?.HomeAnchorSkill;
        if (!string.IsNullOrWhiteSpace(homeAnchorSkill) &&
            !(rotation.Skills?.ContainsKey(homeAnchorSkill) ?? false))
            Add(errors, "UnknownHomeAnchorSkillKey", homeAnchorSkill);

        foreach (var row in rotation.Default ?? [])
            ValidateRow(row, rotation, actionNames, errors);

        foreach (var rule in rotation.Rules ?? [])
        {
            ValidateWhen(rule?.When, rotation, errors, rule?.Then);
            foreach (var row in rule?.Then ?? [])
                ValidateRow(row, rotation, actionNames, errors);
            if (rule?.Then == null || rule.Then.Count == 0)
                Add(errors, "EmptyThen", "rule has no then rows");
        }
    }

    private void ValidateRow(BotRotationRow row, BotRotationDefinition rotation, HashSet<string> actionNames,
        List<BotRotationValidationError> errors)
    {
        if (row == null || !IsActionKind(row.Action))
            Add(errors, "UnknownActionKind", row?.Action ?? "null");

        var isMoveMode = string.Equals(row?.Action, "move", StringComparison.OrdinalIgnoreCase) &&
                         row.Skill is "melee" or "spellRange" or "behind" or "facing" or "away" or "flee";
        if (!string.IsNullOrWhiteSpace(row?.Skill) && !isMoveMode && !rotation.Skills.ContainsKey(row.Skill))
            Add(errors, "UnknownSkillKey", row.Skill);

        if (row != null && (row.Relevance <= 10f || row.Relevance >= 99f))
            Add(errors, "RelevanceOutOfBand", row.Relevance.ToString("0.##"));

        if (row?.When != null)
            ValidateWhen(row.When, rotation, errors, [row]);
        foreach (var key in row?.ExtensionData?.Keys ?? [])
            Add(errors, "UnknownRowKey", key);
        if (row?.Combo != null)
        {
            if (ResolveSkill(rotation, row.Combo.Opener) == null)
                Add(errors, "UnknownSkillKey", row.Combo.Opener ?? "null");
            if (ResolveSkill(rotation, row.Combo.FollowUp) == null)
                Add(errors, "UnknownSkillKey", row.Combo.FollowUp ?? "null");
        }
        if (row?.IgnoreGlobalDelay == true &&
            !string.Equals(row.Action, "castMelee", StringComparison.OrdinalIgnoreCase))
            Add(errors, "InvalidIgnoreGlobalDelay", row.Action ?? "null");
        if (row?.Chain != null)
        {
            foreach (var key in row.Chain.ExtensionData?.Keys ?? [])
                Add(errors, "UnknownChainKey", key);
            if (!string.Equals(row.Chain.Id, "tripleSlash", StringComparison.OrdinalIgnoreCase))
                Add(errors, "UnknownChainId", row.Chain.Id ?? "null");
            if (row.Chain.Stage is < 0 or > 2)
                Add(errors, "InvalidChainStage", row.Chain.Stage.ToString());
        }

        var effectiveName = string.IsNullOrWhiteSpace(row?.As) ? DefaultActionName(row, rotation) : row.As;
        if (!string.IsNullOrWhiteSpace(effectiveName) && !actionNames.Add(effectiveName))
            Add(errors, "DuplicateActionName", effectiveName);
    }

    private void ValidateWhen(BotRotationWhen when, BotRotationDefinition rotation,
        List<BotRotationValidationError> errors, IEnumerable<BotRotationRow> rows)
    {
        if (when == null)
        {
            if (rows != null)
                Add(errors, "UnknownTriggerKind", "null");
            return;
        }

        if (!IsTriggerKind(when.Kind))
            Add(errors, "UnknownTriggerKind", when.Kind ?? "null");
        if (string.Equals(when.Kind, "range", StringComparison.OrdinalIgnoreCase))
        {
            var hasRangeSkill = false;
            foreach (var argumentName in new[] { "skill", "spell", "opener" })
            {
                if (!when.Arguments.TryGetValue(argumentName, out var token))
                    continue;
                var skillKey = token.ToObject<string>();
                if (string.IsNullOrWhiteSpace(skillKey))
                    continue;
                hasRangeSkill = true;
                if (!rotation.Skills.ContainsKey(skillKey) && !s_rangeModes.Contains(skillKey))
                    Add(errors, "UnknownSkillKey", skillKey);
            }

            if (!hasRangeSkill &&
                !(rows?.Any(row => !string.IsNullOrWhiteSpace(row?.Skill) &&
                                   rotation.Skills.ContainsKey(row.Skill)) ?? false))
                Add(errors, "MissingRangeSkill", "range rule has no skill in when or then");
        }
        if (string.Equals(when.Kind, "not", StringComparison.OrdinalIgnoreCase) && when.Children?.Count != 1)
            Add(errors, "InvalidTriggerChildren", "not requires exactly one child");
        foreach (var child in when.Children ?? [])
            ValidateWhen(child, rotation, errors, rows: null);
    }

    private bool SkillExists(uint id)
    {
        if (_skillExists != null)
            return _skillExists(id);
        try
        {
            return SkillManager.Instance.GetSkillTemplate(id) != null;
        }
        catch
        {
            return false;
        }
    }

    private IReadOnlyCollection<uint> ResolveLearnOrder(string archetype)
    {
        if (_learnOrder != null)
            return _learnOrder(archetype);
        return BotArchetypeManager.DefaultDefinitions()
            .FirstOrDefault(definition => string.Equals(definition.Name, archetype, StringComparison.OrdinalIgnoreCase))
            ?.SkillLearnOrder;
    }

    private static void Add(List<BotRotationValidationError> errors, string code, string detail)
    {
        if (errors.Any(error => error.Code == code && error.Message == detail))
            return;
        errors.Add(new(code, detail));
    }

    private static string DefaultActionName(BotRotationRow row, BotRotationDefinition rotation)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Action))
            return null;
        if (row.Action.Equals("autoAttack", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(row.Skill) ? "autoattack" : $"autoattack:{row.Skill}";
        if (row.Action.Equals("maintainRange", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(row.Skill) ? "maintain-range" : $"maintain-range:{row.Skill}";
        return string.IsNullOrWhiteSpace(row.Skill) ? row.Action : $"cast:{row.Skill}";
    }

    private static string GetSkillArgument(BotRotationWhen when)
    {
        foreach (var name in new[] { "skill", "spell", "opener" })
            if (when.Arguments.TryGetValue(name, out var token))
                return token.ToObject<string>();
        return null;
    }

    private static uint? ResolveSkill(BotRotationDefinition rotation, string key) =>
        !string.IsNullOrWhiteSpace(key) && rotation.Skills.TryGetValue(key, out var id) ? id : null;
}
