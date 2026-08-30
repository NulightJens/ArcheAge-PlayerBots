using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Blackboard;

[NotInParallel]
public class WorldValuesTests
{
    [Test]
    public async Task Create_ReturnsNpcIdsAndFiltersDeadNpcsAndAttackers()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var live = MakeNpc(11, 10, 100);
        var attacker = MakeNpc(12, 20, 100);
        attacker.CurrentTarget = bot;
        var dead = MakeNpc(13, 30, 0);
        var scans = new List<Npc> { live, attacker, dead };
        var config = new BotConfig();
        var board = WorldValues.Create(bot, (_, _) => scans, (_, _) => [], config: config);
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        await Assert.That(board.Get(BotValues.NearbyNpcIds, now)).IsEquivalentTo(new uint[] { 11, 12, 13 });
        await Assert.That(board.Get(BotValues.NearbyHostileNpcIds, now)).IsEquivalentTo(new uint[] { 11, 12 });
        await Assert.That(board.Get(BotValues.AttackerIds, now)).IsEquivalentTo(new uint[] { 12 });
    }

    [Test]
    public async Task Create_NearestRealPlayerIgnoresBotsAndReturnsMaxWhenNone()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        bot.IsBot = true;
        var nearbyBot = BotTestFixture.MakeBot(2, new Vector3(5, 0, 0));
        nearbyBot.IsBot = true;
        var realPlayer = BotTestFixture.MakeBot(3, new Vector3(20, 0, 0));
        realPlayer.IsBot = false;
        var characters = new List<Character> { bot, nearbyBot, realPlayer };
        var board = WorldValues.Create(bot, (_, _) => [], (_, _) => characters, config: new BotConfig());
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        await Assert.That(board.Get(BotValues.NearestRealPlayerDistance, now)).IsEqualTo(20f);

        var noPlayers = WorldValues.Create(bot, (_, _) => [], (_, _) => [bot, nearbyBot], config: new BotConfig());
        await Assert.That(noPlayers.Get(BotValues.NearestRealPlayerDistance, now)).IsEqualTo(float.MaxValue);
    }

    [Test]
    public async Task Create_UsesInjectedScanSeamsAndTtl()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var calls = 0;
        var metrics = new BotHostMetrics();
        var config = new BotConfig { ScanTtlMs = 2000 };
        var board = WorldValues.Create(bot, (_, _) =>
        {
            calls++;
            return [MakeNpc(11, 10, 100)];
        }, (_, _) => [], config: config, metrics: metrics);
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        _ = board.Get(BotValues.NearbyNpcIds, now);
        _ = board.Get(BotValues.NearbyHostileNpcIds, now);
        _ = board.Get(BotValues.AttackerIds, now.AddMilliseconds(1999));
        _ = board.Get(BotValues.NearbyHostileNpcIds, now.AddMilliseconds(2000));
        _ = board.Get(BotValues.AttackerIds, now.AddMilliseconds(2000));

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(metrics.Snapshot().NpcScans).IsEqualTo(2L);
    }

    [Test]
    public async Task Create_DerivedNpcValuesRecomputeAgainstCachedScanOnEveryRead()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var attacker = MakeNpc(11, 10, 100);
        attacker.CurrentTarget = bot;
        var scans = 0;
        var board = WorldValues.Create(bot, (_, _) =>
        {
            scans++;
            return [attacker];
        }, (_, _) => [], config: new BotConfig { ScanTtlMs = 2000 });
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        await Assert.That(board.Get(BotValues.AttackerIds, now)).IsEquivalentTo(new uint[] { 11 });
        attacker.CurrentTarget = null;
        await Assert.That(board.Get(BotValues.AttackerIds, now.AddMilliseconds(1))).IsEmpty();
        await Assert.That(scans).IsEqualTo(1);
    }

    [Test]
    public async Task Create_FiltersStealthedNpcsFromNearbyHostileIds()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var visible = MakeNpc(11, 10, 100);
        var stealthed = MakeNpc(12, 20, 100);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);
        stealthed.Buffs = buffs.Object;
        var board = WorldValues.Create(bot, (_, _) => [visible, stealthed], (_, _) => [], config: new BotConfig());
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        var hostileIds = board.Get(BotValues.NearbyHostileNpcIds, now);

        await Assert.That(hostileIds).IsEquivalentTo(new uint[] { 11 });
    }

    [Test]
    public async Task Create_InvalidatingNpcValues_InvalidatesSharedScanAndAllDerivedValues()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var current = new List<Npc> { MakeNpc(11, 10, 100) };
        var scans = 0;
        var board = WorldValues.Create(bot, (_, _) =>
        {
            scans++;
            return current;
        }, (_, _) => [], config: new BotConfig());
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        _ = board.Get(BotValues.NearbyNpcIds, now);
        _ = board.Get(BotValues.NearbyHostileNpcIds, now);
        _ = board.Get(BotValues.AttackerIds, now);
        current[0] = MakeNpc(12, 10, 100);

        board.Invalidate(BotValues.AttackerIds);

        await Assert.That(board.Get(BotValues.NearbyNpcIds, now)).IsEquivalentTo(new uint[] { 12 });
        await Assert.That(board.Get(BotValues.NearbyHostileNpcIds, now)).IsEquivalentTo(new uint[] { 12 });
        await Assert.That(board.Get(BotValues.AttackerIds, now)).IsEmpty();
        await Assert.That(scans).IsEqualTo(2);
    }

    private static Npc MakeNpc(uint objId, float x, int hp)
    {
        var npc = new Npc { ObjId = objId, Hp = hp, MaxHp = 100 };
        npc.Transform.Local.SetPosition(new Vector3(x, 0, 0));
        return npc;
    }
}
