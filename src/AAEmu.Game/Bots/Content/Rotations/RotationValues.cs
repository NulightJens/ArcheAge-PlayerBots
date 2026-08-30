using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Content.Rotations.Values;

public interface IRotationValue
{
    string Name { get; }
    object Get(BotContext context);
}

public sealed class SelfValue : IRotationValue
{
    public string Name => "self";
    public object Get(BotContext context) => context.Bot;
}

public sealed class TargetValue : IRotationValue
{
    public string Name => "target";
    public object Get(BotContext context) => context.Runtime.CombatState.Target ?? context.Bot.CurrentTarget;
}

public sealed class AttackersValue : IRotationValue
{
    public string Name => "attackers";
    public object Get(BotContext context) => context.Bot.AggroTable.Values
        .Where(entry => entry?.Owner != null)
        .Select(entry => entry.Owner)
        .Cast<Unit>()
        .ToArray();
}

public sealed class StatValue(string name, bool onSelf = true) : IRotationValue
{
    public string Name => "stat";
    public object Get(BotContext context)
    {
        return name?.ToLowerInvariant() switch
        {
            "hp" or "health" => onSelf ? context.Bot.Hp : context.Runtime.CombatState.Target?.Hp ?? 0,
            "mp" or "mana" => onSelf ? context.Bot.Mp : context.Runtime.CombatState.Target?.Mp ?? 0,
            "labor" => onSelf ? context.Bot.LaborPower : 0,
            _ => 0
        };
    }
}

public sealed class DistanceValue(string to = "target") : IRotationValue
{
    public string Name => "distance";
    public object Get(BotContext context)
    {
        var target = context.Runtime.CombatState.Target;
        return target?.Transform == null ? float.MaxValue :
            Vector3.Distance(context.Bot.Transform.World.Position, target.Transform.World.Position);
    }
}

public sealed class EnemyCountValue(float radius) : IRotationValue
{
    public string Name => "enemyCount";
    public object Get(BotContext context) => WorldManager.GetAround<Unit>(context.Bot, radius, true)
        .Count(unit => unit != context.Bot && !unit.IsDead && context.Bot.CanAttack(unit));
}

public sealed class AoePositionValue : IRotationValue
{
    public string Name => "aoePosition";
    public object Get(BotContext context) => context.Runtime.CombatState.Target?.Transform?.World.Position ??
                                              context.Bot.Transform?.World.Position ?? Vector3.Zero;
}

public sealed class ComboStateValue : IRotationValue
{
    public string Name => "comboState";
    public bool GetValue(BotContext context) => context.Runtime.CombatState.IsComboLocked;
    public object Get(BotContext context) => GetValue(context);
}

public sealed class StalkerActiveValue : IRotationValue
{
    public string Name => "stalkerActive";
    public bool GetValue(BotContext context) => context.Runtime.CombatState.IsStalking;
    public object Get(BotContext context) => GetValue(context);
}

public sealed class RotationValueResolver
{
    private readonly record struct EnemyCountCacheEntry(DateTime ComputedAt, int Count);

    private readonly IRotationValue _distance = new DistanceValue();
    private readonly IRotationValue _enemyCount = new EnemyCountValue(10f);
    private readonly IRotationValue _aoePosition = new AoePositionValue();
    private readonly IRotationValue _comboState = new ComboStateValue();
    private readonly IRotationValue _stalkerActive = new StalkerActiveValue();
    private readonly IRotationValue _selfHealth = new StatValue("health");
    private readonly IRotationValue _selfMana = new StatValue("mana");
    private readonly IRotationValue _selfLabor = new StatValue("labor");
    private readonly IRotationValue _targetHealth = new StatValue("health", false);
    private readonly IRotationValue _targetMana = new StatValue("mana", false);
    private readonly Func<Character, float, IEnumerable<Unit>> _enemyScanner;
    private DateTime _cacheAt = DateTime.MinValue;
    private Character _cacheBot;
    private Unit _cacheTarget;
    private float _cachedDistance;
    private bool _hasDistance;
    private Dictionary<float, EnemyCountCacheEntry> _enemyCountCache;
    private Character _enemyCacheBot;
    private uint _enemyCacheWorldId;
    private uint _enemyCacheInstanceId;

    public RotationValueResolver(Func<Character, float, IEnumerable<Unit>> enemyScanner = null)
    {
        _enemyScanner = enemyScanner ?? ScanEnemies;
    }

    public object Get(string name, BotContext context, string argument = null, bool onSelf = true)
    {
        return name?.ToLowerInvariant() switch
        {
            "distance" => Distance(context),
            "enemycount" => EnemyCount(context, argument == null ? 10f : float.Parse(argument, System.Globalization.CultureInfo.InvariantCulture)),
            "stat" => Stat(context, argument, onSelf),
            "aoeposition" => _aoePosition.Get(context),
            "combostate" => ComboState(context),
            "stalkeractive" => _stalkerActive.Get(context),
            _ => null
        };
    }

    public float Distance(BotContext context)
    {
        EnsureContext(context);
        if (_cacheTarget?.Transform == null || context.Bot.Transform == null)
            return float.MaxValue;
        if (_hasDistance && _cacheAt == context.Now && ReferenceEquals(_cacheBot, context.Bot) &&
            ReferenceEquals(_cacheTarget, context.Runtime.CombatState.Target))
            return _cachedDistance;
        _cachedDistance = (float)Vector3.Distance(context.Bot.Transform.World.Position,
            _cacheTarget.Transform.World.Position);
        _hasDistance = true;
        return _cachedDistance;
    }

    public int EnemyCount(BotContext context, float radius)
    {
        EnsureEnemyContext(context);
        var normalizedRadius = NormalizeRadius(radius);
        if (_enemyCountCache != null &&
            _enemyCountCache.TryGetValue(normalizedRadius, out var cached) &&
            IsEnemyCountFresh(cached, context))
            return cached.Count;

        context.Runtime.HostMetrics?.RecordWorldScan(BotWorldScanKind.EnemyCount);
        var count = _enemyScanner(context.Bot, normalizedRadius)
            .Count(unit => unit != context.Bot && !unit.IsDead && context.Bot.CanAttack(unit));
        _enemyCountCache ??= [];
        _enemyCountCache[normalizedRadius] = new(context.Now, count);
        return count;
    }

    public object AoePosition(BotContext context) => _aoePosition.Get(context);

    public bool ComboState(BotContext context) => ((ComboStateValue)_comboState).GetValue(context);

    public bool StalkerActive(BotContext context) => ((StalkerActiveValue)_stalkerActive).GetValue(context);

    public object Stat(BotContext context, string name, bool onSelf)
    {
        if (!onSelf)
            return string.Equals(name, "hp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "health", StringComparison.OrdinalIgnoreCase)
                ? _targetHealth.Get(context)
                : _targetMana.Get(context);
        if (string.Equals(name, "labor", StringComparison.OrdinalIgnoreCase))
            return _selfLabor.Get(context);
        return string.Equals(name, "mp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "mana", StringComparison.OrdinalIgnoreCase)
            ? _selfMana.Get(context)
            : _selfHealth.Get(context);
    }

    private void EnsureContext(BotContext context)
    {
        if (_cacheAt == context.Now && ReferenceEquals(_cacheBot, context.Bot) &&
            ReferenceEquals(_cacheTarget, context.Runtime.CombatState.Target))
            return;
        _cacheAt = context.Now;
        _cacheBot = context.Bot;
        _cacheTarget = context.Runtime.CombatState.Target;
        _hasDistance = false;
    }

    private void EnsureEnemyContext(BotContext context)
    {
        var worldId = context.Bot.Transform?.WorldId ?? uint.MaxValue;
        var instanceId = context.Bot.Transform?.InstanceId ?? uint.MaxValue;
        if (ReferenceEquals(_enemyCacheBot, context.Bot) && _enemyCacheWorldId == worldId &&
            _enemyCacheInstanceId == instanceId)
            return;

        _enemyCountCache?.Clear();
        _enemyCacheBot = context.Bot;
        _enemyCacheWorldId = worldId;
        _enemyCacheInstanceId = instanceId;
    }

    private static bool IsEnemyCountFresh(EnemyCountCacheEntry cached, BotContext context)
    {
        var ageMs = (context.Now - cached.ComputedAt).TotalMilliseconds;
        if (ageMs < 0)
            return false;
        if (ageMs == 0)
            return true;
        return context.Config.ScanTtlMs > 0 && ageMs < context.Config.ScanTtlMs;
    }

    private static float NormalizeRadius(float radius) =>
        MathF.Round(radius, 3, MidpointRounding.AwayFromZero);

    private static List<Unit> ScanEnemies(Character bot, float radius) =>
        WorldManager.GetAround<Unit>(bot, radius, true);
}
