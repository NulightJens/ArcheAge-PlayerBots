using System.Numerics;
using AAEmu.Game.Bots.Content.Rotations.Values;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Faction;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.StaticValues;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

public sealed class RotationValueResolverTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task EnemyCount_SameNormalizedRadiusUsesOneScanWithinTtl()
    {
        var fixture = CreateFixture();
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return [];
        });

        values.EnemyCount(fixture.Context(Now), 8f);
        values.EnemyCount(fixture.Context(Now.AddMilliseconds(300)), 8.0004f);
        values.EnemyCount(fixture.Context(Now.AddMilliseconds(1999)), 8f);

        await Assert.That(scans).IsEqualTo(1);
    }

    [Test]
    public async Task EnemyCount_AlternatingRadiiRetainIndependentCacheEntries()
    {
        var fixture = CreateFixture();
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return [];
        });

        values.EnemyCount(fixture.Context(Now), 8f);
        values.EnemyCount(fixture.Context(Now), 10f);
        values.EnemyCount(fixture.Context(Now.AddMilliseconds(300)), 8f);
        values.EnemyCount(fixture.Context(Now.AddMilliseconds(600)), 10f);

        await Assert.That(scans).IsEqualTo(2);
    }

    [Test]
    public async Task EnemyCount_RefreshesAtTtlExpiry()
    {
        var fixture = CreateFixture();
        var first = UnitAt(10, 1);
        var second = UnitAt(11, 2);
        IReadOnlyList<Unit> units = [first];
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return units;
        });

        var initial = values.EnemyCount(fixture.Context(Now), 8f);
        units = [first, second];
        var beforeExpiry = values.EnemyCount(fixture.Context(Now.AddMilliseconds(1999)), 8f);
        var atExpiry = values.EnemyCount(fixture.Context(Now.AddMilliseconds(2000)), 8f);

        await Assert.That(initial).IsEqualTo(1);
        await Assert.That(beforeExpiry).IsEqualTo(1);
        await Assert.That(atExpiry).IsEqualTo(2);
        await Assert.That(scans).IsEqualTo(2);
    }

    [Test]
    public async Task EnemyCount_ZeroTtlDedupesOnlyTheSameContextTime()
    {
        var fixture = CreateFixture(scanTtlMs: 0);
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return [];
        });

        values.EnemyCount(fixture.Context(Now), 8f);
        values.EnemyCount(fixture.Context(Now), 8f);
        values.EnemyCount(fixture.Context(Now.AddMilliseconds(1)), 8f);

        await Assert.That(scans).IsEqualTo(2);
    }

    [Test]
    public async Task EnemyCount_ClockRollbackForcesRefresh()
    {
        var fixture = CreateFixture();
        var first = UnitAt(10, 1);
        var second = UnitAt(11, 2);
        IReadOnlyList<Unit> units = [first];
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return units;
        });

        var initial = values.EnemyCount(fixture.Context(Now), 8f);
        units = [first, second];
        var afterRollback = values.EnemyCount(fixture.Context(Now.AddMilliseconds(-1)), 8f);

        await Assert.That(initial).IsEqualTo(1);
        await Assert.That(afterRollback).IsEqualTo(2);
        await Assert.That(scans).IsEqualTo(2);
    }

    [Test]
    public async Task EnemyCount_ResolverInstancesDoNotShareEntries()
    {
        var fixture = CreateFixture();
        var scans = 0;
        Func<Character, float, IEnumerable<Unit>> scanner = (_, _) =>
        {
            scans++;
            return [];
        };
        var first = new RotationValueResolver(scanner);
        var second = new RotationValueResolver(scanner);

        first.EnemyCount(fixture.Context(Now), 8f);
        second.EnemyCount(fixture.Context(Now), 8f);

        await Assert.That(scans).IsEqualTo(2);
    }

    [Test]
    public async Task EnemyCount_TargetChangeDoesNotInvalidateSpatialCache()
    {
        var fixture = CreateFixture();
        var first = UnitAt(10, 1);
        var second = UnitAt(11, 2);
        IReadOnlyList<Unit> units = [first];
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return units;
        });

        var initial = values.EnemyCount(fixture.Context(Now), 8f);
        fixture.Runtime.CombatState.Target = UnitAt(20, 3);
        units = [first, second];
        var afterTargetChange = values.EnemyCount(fixture.Context(Now.AddMilliseconds(300)), 8f);

        await Assert.That(initial).IsEqualTo(1);
        await Assert.That(afterTargetChange).IsEqualTo(1);
        await Assert.That(scans).IsEqualTo(1);
    }

    [Test]
    public async Task EnemyCount_WorldInstanceChangeInvalidatesSpatialCache()
    {
        var fixture = CreateFixture();
        var scans = 0;
        var values = new RotationValueResolver((_, _) =>
        {
            scans++;
            return [];
        });

        values.EnemyCount(fixture.Context(Now), 8f);
        BotTestFixture.SetPrivateField(fixture.Bot.Transform, "_instanceId", 2u);
        values.EnemyCount(fixture.Context(Now.AddMilliseconds(300)), 8f);

        await Assert.That(scans).IsEqualTo(2);
    }

    [Test]
    public async Task EnemyCount_PreservesRadiusAliveSelfAndAttackableSemantics()
    {
        var fixture = CreateFixture();
        fixture.Bot.Faction = new SystemFaction { Id = FactionsEnum.Friendly };
        var nearOne = UnitAt(10, 3);
        var nearTwo = UnitAt(11, 7.9f);
        var outer = UnitAt(12, 9);
        var dead = UnitAt(13, 2, alive: false);
        var unattackable = UnitAt(14, 2);
        unattackable.ObjId = fixture.Bot.ObjId;
        unattackable.Faction = new SystemFaction { Id = FactionsEnum.Friendly };
        Unit[] units = [fixture.Bot, nearOne, nearTwo, outer, dead, unattackable];
        var scans = 0;
        var values = new RotationValueResolver((bot, radius) =>
        {
            scans++;
            return units.Where(unit => Vector3.Distance(bot.Transform.World.Position,
                unit.Transform.World.Position) <= radius);
        });

        var withinEight = values.EnemyCount(fixture.Context(Now), 8f);
        var withinTen = values.EnemyCount(fixture.Context(Now), 10f);

        await Assert.That(withinEight).IsEqualTo(2);
        await Assert.That(withinTen).IsEqualTo(3);
        await Assert.That(scans).IsEqualTo(2);
    }

    private static Fixture CreateFixture(double scanTtlMs = 2000)
    {
        var bot = UnitAt(1, 0);
        var config = new BotConfig { UseEngine = false, ScanTtlMs = scanTtlMs };
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState(), config: config);
        return new(bot, runtime, config);
    }

    private static CharacterMock UnitAt(uint id, float x, bool alive = true)
    {
        var unit = BotTestFixture.MakeBot(id, new Vector3(x, 0, 0));
        unit.Hp = unit.MaxHp = alive ? 100 : 0;
        return unit;
    }

    private sealed record Fixture(CharacterMock Bot, BotRuntime Runtime, BotConfig Config)
    {
        public BotContext Context(DateTime now) =>
            new(Bot, Runtime, Runtime.Blackboard, now, Config, BotEngineKind.Combat);
    }
}
