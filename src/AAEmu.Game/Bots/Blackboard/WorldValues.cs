using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Blackboard;

public static class WorldValues
{
    public static BotBlackboard Create(
        Character bot,
        Func<Character, float, List<Npc>> nearbyNpcs = null,
        Func<Character, float, List<Character>> nearbyCharacters = null,
        Func<Npc, bool> hostileFilter = null,
        BotConfig config = null,
        BotHostMetrics metrics = null)
    {
        ArgumentNullException.ThrowIfNull(bot);
        config ??= BotConfig.Instance;
        var npcScanner = nearbyNpcs ?? ((character, radius) => WorldManager.GetAround<Npc>(character, radius, true));
        var characterScanner = nearbyCharacters ?? ((character, radius) => WorldManager.GetAround<Character>(character, radius, true));
        nearbyNpcs = (character, radius) =>
        {
            metrics?.RecordWorldScan(BotWorldScanKind.Npc);
            return npcScanner(character, radius);
        };
        nearbyCharacters = (character, radius) =>
        {
            metrics?.RecordWorldScan(BotWorldScanKind.RealPlayer);
            return characterScanner(character, radius);
        };
        hostileFilter ??= npc => !npc.IsDead && bot.CanAttack(npc) && !IsStealthed(npc);

        var blackboard = new BotBlackboard();
        var scanTtl = TimeSpan.FromMilliseconds(config.ScanTtlMs);
        var realPlayerScanTtl = TimeSpan.FromMilliseconds(config.RealPlayerScanTtlMs);
        var nearbyNpcScan = new NearbyNpcScan(bot, nearbyNpcs, config.SearchRadius, scanTtl);
        var npcIds = new NpcIdsGroup(nearbyNpcScan);

        blackboard.Register(BotValues.NearbyNpcIds, new NpcIdsValue(
            npcIds, null, TimeSpan.Zero));
        blackboard.Register(BotValues.NearbyHostileNpcIds, new NpcIdsValue(
            npcIds, hostileFilter, TimeSpan.Zero));
        blackboard.Register(BotValues.AttackerIds, new NpcIdsValue(
            npcIds, npc => hostileFilter(npc) && npc.CurrentTarget == bot, TimeSpan.Zero));
        blackboard.Register(BotValues.HostileAreaTriggersNearby, HazardValues.Create(
            bot,
            () => AreaTriggerManager.Instance.Active,
            TimeSpan.FromMilliseconds(config.HazardScanTtlMs)));
        blackboard.Register(BotValues.NearestRealPlayerDistance, new CalculatedValue<float>(
            () => NearestRealPlayerDistance(bot, nearbyCharacters(bot, (float)config.ActivityRealPlayerRadius)), realPlayerScanTtl));
        blackboard.Register(BotValues.TargetFacingDelta, new CalculatedValue<float>(
            () => TargetFacingDelta(bot), TimeSpan.Zero));
        return blackboard;
    }

    private sealed class NearbyNpcScan : BotValue<List<Npc>>
    {
        private readonly Character _bot;
        private readonly Func<Character, float, List<Npc>> _scan;
        private readonly float _radius;

        public NearbyNpcScan(
            Character bot,
            Func<Character, float, List<Npc>> scan,
            double radius,
            TimeSpan ttl)
            : base(ttl)
        {
            _bot = bot;
            _scan = scan;
            _radius = (float)radius;
        }

        protected override List<Npc> Compute(DateTime now) => _scan(_bot, _radius);
    }

    private sealed class NpcIdsValue : BotValue<List<uint>>
    {
        private readonly NpcIdsGroup _group;
        private readonly Func<Npc, bool> _predicate;

        public NpcIdsValue(NpcIdsGroup group, Func<Npc, bool> predicate, TimeSpan ttl)
            : base(ttl)
        {
            _group = group;
            _predicate = predicate;
            group.Add(this);
        }

        protected override List<uint> Compute(DateTime now) => ToIds(_group.Scan.Get(now), _predicate);

        public override void Invalidate()
        {
            _group.Invalidate();
        }

        internal void InvalidateCache() => base.Invalidate();
    }

    private sealed class NpcIdsGroup
    {
        private readonly List<NpcIdsValue> _values = [];
        private bool _invalidating;

        public NpcIdsGroup(NearbyNpcScan scan)
        {
            Scan = scan;
        }

        public NearbyNpcScan Scan { get; }

        public void Add(NpcIdsValue value) => _values.Add(value);

        public void Invalidate()
        {
            if (_invalidating)
                return;

            _invalidating = true;
            try
            {
                Scan.Invalidate();
                foreach (var value in _values)
                    value.InvalidateCache();
            }
            finally
            {
                _invalidating = false;
            }
        }
    }

    private static List<uint> ToIds(List<Npc> npcs, Func<Npc, bool> predicate = null)
    {
        var ids = new List<uint>(npcs?.Count ?? 0);
        if (npcs == null)
            return ids;

        foreach (var npc in npcs)
        {
            if (npc != null && (predicate == null || predicate(npc)))
                ids.Add(npc.ObjId);
        }

        return ids;
    }

    private static float NearestRealPlayerDistance(Character bot, List<Character> characters)
    {
        var nearest = float.MaxValue;
        if (characters == null)
            return nearest;

        foreach (var character in characters)
        {
            if (character == null || character == bot || character.IsBot)
                continue;

            var distance = Vector3.Distance(bot.Transform.World.Position, character.Transform.World.Position);
            if (distance < nearest)
                nearest = distance;
        }

        return nearest;
    }

    private static float TargetFacingDelta(Character bot)
    {
        if (bot.Transform == null || bot.CurrentTarget is not Unit target || target.Transform == null)
            return float.MaxValue;

        var from = bot.Transform.World.Position;
        var to = target.Transform.World.Position;
        var desired = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var delta = desired - bot.Transform.World.Rotation.Z;
        while (delta > MathF.PI)
            delta -= MathF.Tau;
        while (delta < -MathF.PI)
            delta += MathF.Tau;
        return delta;
    }

    private static bool IsStealthed(Npc npc)
    {
        return npc.Buffs.HasEffectsMatchingCondition(e => e.Template.Stealth);
    }
}
