using System.Numerics;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Utils;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

public class BotMovementBroadcasterTests
{
    private FakeTimeProvider _time;
    private CharacterMock _bot;
    private List<UnitMoveType> _sent;
    private BotMovementBroadcaster _broadcaster;

    [Before(Test)]
    public void Setup()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        _bot = MakeBot(2, Vector3.Zero);
        _sent = [];
        _broadcaster = new BotMovementBroadcaster(_bot, _time)
        {
            MoveTypeSink = _sent.Add
        };
        _time.Advance(TimeSpan.FromMilliseconds(60));
    }

    [Test]
    public async Task SendMove_SpeedAboveThreshold_SetsActorFlags4AndMovingFlag()
    {
        _broadcaster.SendMove(new Vector3(1, 0, 0), new Vector3(4, 0, 0), false);

        await Assert.That(_sent[0].ActorFlags).IsEqualTo((byte)4);
        await Assert.That(_sent[0].Flags).IsEqualTo(MoveTypeFlags.Moving);
    }

    [Test]
    public async Task SendMove_SpeedExactly3_IsWalking_ActorFlags5()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(3, 0, 0), false);

        await Assert.That(_sent[0].ActorFlags).IsEqualTo((byte)5);
    }

    [Test]
    public async Task SendMove_EncodesVelocityAsMillimetresPerSecond()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(1.234f, -0.5f, 0), false);

        await Assert.That(_sent[0].VelX).IsEqualTo((short)1234);
        await Assert.That(_sent[0].VelY).IsEqualTo((short)-500);
        await Assert.That(_sent[0].VelZ).IsEqualTo((short)0);
    }

    [Test]
    public async Task SendMove_VelocityAbove32_767mps_ClampsToInt16Max()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(40, 0, 0), false);

        await Assert.That(_sent[0].VelX).IsEqualTo(short.MaxValue);
    }

    [Test]
    public async Task SendMove_InBattle_UsesCombatStanceAndAlertness()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(4, 0, 0), true);

        await Assert.That(_sent[0].Stance).IsEqualTo(GameStanceType.Combat);
        await Assert.That(_sent[0].Alertness).IsEqualTo(MoveTypeAlertness.Combat);
    }

    [Test]
    public async Task SendMove_NotInBattle_UsesRelaxedStanceIdleAlertness()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(4, 0, 0), false);

        await Assert.That(_sent[0].Stance).IsEqualTo(GameStanceType.Relaxed);
        await Assert.That(_sent[0].Alertness).IsEqualTo(MoveTypeAlertness.Idle);
    }

    [Test]
    public async Task SendMove_DeltaMovementIsForward127()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(4, 0, 0), false);

        await Assert.That(_sent[0].DeltaMovement[0]).IsEqualTo((sbyte)0);
        await Assert.That(_sent[0].DeltaMovement[1]).IsEqualTo((sbyte)127);
        await Assert.That(_sent[0].DeltaMovement[2]).IsEqualTo((sbyte)0);
    }

    [Test]
    public async Task SendStop_ZeroVelocityStoppingFlagsAndZeroDelta()
    {
        _broadcaster.SendStop(Vector3.Zero, false);

        await Assert.That(_sent[0].VelX).IsEqualTo((short)0);
        await Assert.That(_sent[0].VelY).IsEqualTo((short)0);
        await Assert.That(_sent[0].VelZ).IsEqualTo((short)0);
        await Assert.That(_sent[0].Flags).IsEqualTo(MoveTypeFlags.Stopping);
        await Assert.That(_sent[0].ActorFlags).IsEqualTo((byte)0);
        await Assert.That(_sent[0].DeltaMovement[0]).IsEqualTo((sbyte)0);
        await Assert.That(_sent[0].DeltaMovement[1]).IsEqualTo((sbyte)0);
        await Assert.That(_sent[0].DeltaMovement[2]).IsEqualTo((sbyte)0);
    }

    [Test]
    public async Task SendMove_IdenticalStateAfterInterval_SecondCallSuppressed()
    {
        var position = new Vector3(1, 0, 0);
        var velocity = new Vector3(4, 0, 0);
        _broadcaster.SendMove(position, velocity, false);
        _time.Advance(TimeSpan.FromMilliseconds(60));

        _broadcaster.SendMove(position, velocity, false);

        await Assert.That(_sent.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SendMove_WithinMinSendInterval_SuppressedEvenIfPositionChanged()
    {
        var velocity = new Vector3(4, 0, 0);
        _broadcaster.SendMove(Vector3.Zero, velocity, false);
        _time.Advance(TimeSpan.FromMilliseconds(10));

        _broadcaster.SendMove(new Vector3(1, 0, 0), velocity, false);

        await Assert.That(_sent.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SendMove_AfterInterval_PositionDelta0_02_Sends()
    {
        var velocity = new Vector3(4, 0, 0);
        _broadcaster.SendMove(Vector3.Zero, velocity, false);
        _time.Advance(TimeSpan.FromMilliseconds(60));

        _broadcaster.SendMove(new Vector3(0.02f, 0, 0), velocity, false);

        await Assert.That(_sent.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SendMove_AfterInterval_PositionDelta0_005_Suppressed()
    {
        var velocity = new Vector3(4, 0, 0);
        _broadcaster.SendMove(Vector3.Zero, velocity, false);
        _time.Advance(TimeSpan.FromMilliseconds(60));

        _broadcaster.SendMove(new Vector3(0.005f, 0, 0), velocity, false);

        await Assert.That(_sent.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SendStop_WithinMinSendInterval_StillSends()
    {
        _broadcaster.SendMove(Vector3.Zero, new Vector3(4, 0, 0), false);
        _time.Advance(TimeSpan.FromMilliseconds(10));

        _broadcaster.SendStop(Vector3.Zero, false);

        await Assert.That(_sent.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SendFall_SamePositionNewFallVelocity_Sends()
    {
        _broadcaster.SendFall(Vector3.Zero, 0.981f, false);
        _time.Advance(TimeSpan.FromMilliseconds(60));

        _broadcaster.SendFall(Vector3.Zero, 1.962f, false);

        await Assert.That(_sent.Count).IsEqualTo(2);
        await Assert.That(_sent[1].VelZ).IsEqualTo((short)1962);
    }

    [Test]
    public async Task SendFall_VelZIsPositiveFallVelocityNotNegative()
    {
        _broadcaster.SendFall(Vector3.Zero, 2f, false);

        await Assert.That(_sent[0].VelZ).IsEqualTo((short)2000);
    }

    [Test]
    public async Task SendJump_UsesNativeJumpFlagsAndInvertsWorldVerticalVelocity()
    {
        _broadcaster.SendJump(Vector3.Zero, new Vector3(3f, -1f, 4.5f), false);

        await Assert.That(_sent[0].Flags).IsEqualTo(MoveTypeFlags.Jumping);
        await Assert.That(_sent[0].ActorFlags).IsEqualTo((byte)MoveTypeActorFlags.Jumping);
        await Assert.That(_sent[0].VelX).IsEqualTo((short)3000);
        await Assert.That(_sent[0].VelY).IsEqualTo((short)-1000);
        await Assert.That(_sent[0].VelZ).IsEqualTo((short)-4500);
        await Assert.That(_sent[0].DeltaMovement).IsEquivalentTo(new sbyte[] { 0, 127, 0 });
    }

    [Test]
    public async Task SendJump_WithoutHorizontalMovement_DoesNotAdvertiseRunInput()
    {
        _broadcaster.SendJump(Vector3.Zero, new Vector3(0f, 0f, 4.5f), false);

        await Assert.That(_sent[0].Flags).IsEqualTo(MoveTypeFlags.Jumping);
        await Assert.That(_sent[0].ActorFlags).IsEqualTo((byte)MoveTypeActorFlags.Jumping);
        await Assert.That(_sent[0].VelZ).IsEqualTo((short)-4500);
        await Assert.That(_sent[0].DeltaMovement).IsEquivalentTo(new sbyte[] { 0, 0, 0 });
    }

    [Test]
    public async Task Golden_Broadcaster_StraightRunEast_ThreeTicksThenStop()
    {
        _bot.Transform.Local.SetRotationDegree(0, 0, 90);
        _broadcaster.SendMove(new Vector3(0.54f, 0, 0), new Vector3(5.4f, 0, 0), false);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        _broadcaster.SendMove(new Vector3(1.08f, 0, 0), new Vector3(5.4f, 0, 0), false);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        _broadcaster.SendMove(new Vector3(1.62f, 0, 0), new Vector3(5.4f, 0, 0), false);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        _broadcaster.SendStop(new Vector3(1.62f, 0, 0), false);

        await Assert.That(_sent.Count).IsEqualTo(4);
        // facing 90 deg (yaw = pi/2) encodes as (0, 0, 31), not 32: the float round-trip in ToRollPitchYawSBytesMovement
        // (DegToRad, -2pi, RadToDeg, +360) lands at 89.99997 deg and ConvertRadianToDirection truncates 31.99999 -> 31.
        // SendMove/SendStop never touch facing, so all three ticks and the stop carry the same bytes.
        var expectedRotations = new[]
        {
            new sbyte[] { 0, 0, 31 },
            new sbyte[] { 0, 0, 31 },
            new sbyte[] { 0, 0, 31 }
        };
        for (var index = 0; index < 3; index++)
        {
            await Assert.That(_sent[index].X).IsEqualTo(0.54f * (index + 1)).Within(1e-4f);
            await Assert.That(_sent[index].VelX).IsEqualTo((short)5400);
            await Assert.That(_sent[index].VelY).IsEqualTo((short)0);
            await Assert.That(_sent[index].VelZ).IsEqualTo((short)0);
            await Assert.That(_sent[index].Flags).IsEqualTo(MoveTypeFlags.Moving);
            await Assert.That(_sent[index].ActorFlags).IsEqualTo((byte)4);
            await Assert.That(_sent[index].DeltaMovement).IsEquivalentTo(new sbyte[] { 0, 127, 0 });
            await Assert.That(_sent[index].Stance).IsEqualTo(GameStanceType.Relaxed);
            await Assert.That(_sent[index].Alertness).IsEqualTo(MoveTypeAlertness.Idle);
            await Assert.That(_sent[index].RotationX).IsEqualTo(expectedRotations[index][0]);
            await Assert.That(_sent[index].RotationY).IsEqualTo(expectedRotations[index][1]);
            await Assert.That(_sent[index].RotationZ).IsEqualTo(expectedRotations[index][2]);
        }

        var stop = _sent[3];
        await Assert.That(stop.X).IsEqualTo(1.62f).Within(1e-4f);
        await Assert.That(stop.VelX).IsEqualTo((short)0);
        await Assert.That(stop.VelY).IsEqualTo((short)0);
        await Assert.That(stop.VelZ).IsEqualTo((short)0);
        await Assert.That(stop.Flags).IsEqualTo(MoveTypeFlags.Stopping);
        await Assert.That(stop.ActorFlags).IsEqualTo((byte)0);
        await Assert.That(stop.DeltaMovement).IsEquivalentTo(new sbyte[] { 0, 0, 0 });
        await Assert.That(stop.Stance).IsEqualTo(GameStanceType.Relaxed);
        await Assert.That(stop.Alertness).IsEqualTo(MoveTypeAlertness.Idle);
        await Assert.That(stop.RotationX).IsEqualTo((sbyte)0);
        await Assert.That(stop.RotationY).IsEqualTo((sbyte)0);
        await Assert.That(stop.RotationZ).IsEqualTo((sbyte)31);
    }

    [Test]
    public async Task Golden_Broadcaster_CombatFallSequence()
    {
        _broadcaster.SendFall(Vector3.Zero, 0.981f, true);
        _time.Advance(TimeSpan.FromMilliseconds(60));
        _broadcaster.SendFall(Vector3.Zero, 1.962f, true);
        _broadcaster.SendStop(Vector3.Zero, true);

        await Assert.That(_sent.Count).IsEqualTo(3);
        await Assert.That(_sent.Select(move => move.VelZ)).IsEquivalentTo(new short[] { 981, 1962, 0 });
        await Assert.That(_sent.Select(move => move.Stance)).IsEquivalentTo(new[]
        {
            GameStanceType.Combat,
            GameStanceType.Combat,
            GameStanceType.Combat
        });
        await Assert.That(_sent.Select(move => move.Alertness)).IsEquivalentTo(new[]
        {
            MoveTypeAlertness.Combat,
            MoveTypeAlertness.Combat,
            MoveTypeAlertness.Combat
        });
        await Assert.That(_sent.Select(move => move.ActorFlags)).IsEquivalentTo(new byte[] { 0, 0, 0 });
    }

    [Test]
    public async Task SendTeleport_ThenSendFaceTargetAtSamePosition_IsSent()
    {
        _broadcaster.SendTeleport(Vector3.Zero, false);
        _time.Advance(TimeSpan.FromMilliseconds(60));

        _broadcaster.SendFaceTarget(Vector3.Zero, 90, false);

        await Assert.That(_sent.Count).IsEqualTo(2);
    }

    [Test]
    public async Task SendTeleport_MovesLocalTransformToTarget()
    {
        var target = new Vector3(5, 6, 7);
        _bot.Transform.Local.SetPosition(target);

        _broadcaster.SendTeleport(target, false);

        await Assert.That(_bot.Transform.Local.Position).IsEqualTo(target);
    }

    [Test]
    public async Task SendFaceTarget_SetsLocalYawDegrees()
    {
        _broadcaster.SendFaceTarget(Vector3.Zero, 90f, false);

        await Assert.That(_bot.Transform.Local.Rotation.Z).IsEqualTo(90f.DegToRad()).Within(1e-5f);
    }

    [Test]
    public async Task BuildMoveType_TimeIsMillisecondsSinceUtcMidnight()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 25, 1, 2, 3, 4, TimeSpan.Zero));
        var bot = MakeBot(2, Vector3.Zero);
        var sent = new List<UnitMoveType>();
        var broadcaster = new BotMovementBroadcaster(bot, time)
        {
            MoveTypeSink = sent.Add
        };

        broadcaster.SendStop(Vector3.Zero, false);

        await Assert.That(sent[0].Time).IsEqualTo(3_723_004u);
    }

    [Test]
    public async Task BuildMoveType_TimeAtLastMillisecondOfDay_DoesNotWrap()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 25, 23, 59, 59, 999, TimeSpan.Zero));
        var bot = MakeBot(2, Vector3.Zero);
        var sent = new List<UnitMoveType>();
        var broadcaster = new BotMovementBroadcaster(bot, time)
        {
            MoveTypeSink = sent.Add
        };

        broadcaster.SendStop(Vector3.Zero, false);

        await Assert.That(sent[0].Time).IsEqualTo(86_399_999u);
    }

    [Test]
    public async Task BuildMoveType_RotationBytes_Facing90Deg_Pinned()
    {
        _bot.Transform.Local.SetRotationDegree(0, 0, 90);
        _broadcaster.SendStop(Vector3.Zero, false);

        await Assert.That(_sent[0].RotationX).IsEqualTo((sbyte)0);
        await Assert.That(_sent[0].RotationY).IsEqualTo((sbyte)0);
        await Assert.That(_sent[0].RotationZ).IsEqualTo((sbyte)31);
    }

    [Test]
    public async Task SendRelaxedStance_AlsoBroadcastsPostureChangedPacket()
    {
        var bot = new PacketRecordingCharacter { ObjId = 1002 };
        var broadcaster = new BotMovementBroadcaster(bot, _time);

        broadcaster.SendRelaxedStance(Vector3.Zero);

        await Assert.That(bot.Sent.OfType<SCUnitModelPostureChangedPacket>().Count()).IsEqualTo(1);
        await Assert.That(bot.Sent.OfType<SCOneUnitMovementPacket>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task RequiresRegionRefresh_ChangesOnlyAcrossSixtyFourMeterRegionBoundary()
    {
        await Assert.That(BotMovementBroadcaster.RequiresRegionRefresh(
            244, 237, new Vector3(15656f, 15173.2f, 121.3f))).IsFalse();
        await Assert.That(BotMovementBroadcaster.RequiresRegionRefresh(
            220, 215, new Vector3(15656f, 15173.2f, 121.3f))).IsTrue();
        await Assert.That(BotMovementBroadcaster.RequiresRegionRefresh(
            0, 0, new Vector3(63.999f, 63.999f, 0f))).IsFalse();
        await Assert.That(BotMovementBroadcaster.RequiresRegionRefresh(
            0, 0, new Vector3(64f, 63.999f, 0f))).IsTrue();
    }

    [Test]
    public void Constructor_NullBot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BotMovementBroadcaster(null, _time));
    }

    private static CharacterMock MakeBot(uint id, Vector3 position)
    {
        var bot = new CharacterMock { Id = id, ObjId = 1000 + id, Name = $"bot{id}" };
        bot.Transform.Local.SetPosition(position);
        return bot;
    }
}
