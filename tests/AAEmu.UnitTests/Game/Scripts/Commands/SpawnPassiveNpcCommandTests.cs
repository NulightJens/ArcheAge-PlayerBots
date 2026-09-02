using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using System.Globalization;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Game.World.Zones;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

[NotInParallel]
public class SpawnPassiveNpcCommandTests
{
    private ServiceProvider _serviceProvider;
    private Func<uint, Character> _previousActiveBotResolver;
    private Func<uint, float, float, float, float> _previousGroundHeightResolver;

    [Before(Test)]
    public void Setup()
    {
        _previousActiveBotResolver = SpawnPassiveNpcCommand.ActiveBotResolver;
        _previousGroundHeightResolver = SpawnPassiveNpcCommand.GroundHeightResolver;
    }

    [After(Test)]
    public void Teardown()
    {
        SpawnPassiveNpcCommand.ActiveBotResolver = _previousActiveBotResolver;
        SpawnPassiveNpcCommand.GroundHeightResolver = _previousGroundHeightResolver;
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        SingletonContainer.ServiceProvider = null;
        BotTestFixture.ResetSingleton<NpcManager>();
        BotTestFixture.ResetSingleton<WorldManager>();
        BotTestFixture.ResetSingleton<ZoneManager>();
        BotTestFixture.ResetSingleton<CharacterManager>();
        BotTestFixture.ResetSingleton<BotManager>();
    }

    [Test]
    public async Task TryParse_TemplateOnly_UsesSafeDefaultDistance()
    {
        var parsed = SpawnPassiveNpcCommand.TryParse(["11180"], out var templateId, out var distance);

        await Assert.That(parsed).IsTrue();
        await Assert.That(templateId).IsEqualTo(11180u);
        await Assert.That(distance).IsEqualTo(SpawnPassiveNpcCommand.DefaultDistance);
    }

    [Test]
    public async Task TryParse_DecimalDistanceUnderGermanCulture_UsesInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var parsed = SpawnPassiveNpcCommand.TryParse(["11180", "14.5"], out _, out var distance);

            await Assert.That(parsed).IsTrue();
            await Assert.That(distance).IsEqualTo(14.5f);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task CommandSurface_PreservesAliasesAndShowsCompleteGrammar()
    {
        var command = new SpawnPassiveNpcCommand();

        await Assert.That(string.Join(',', command.CommandNames))
            .IsEqualTo("spawnpassive,passivenpc,passiveboss");
        await Assert.That(command.GetCommandLineHelp())
            .IsEqualTo("<npcTemplateId> [distance] [anchorBotId] [yawOffsetDegrees]");
        await Assert.That(command.GetCommandHelpText()).IsEqualTo(
            "Spawns a killable, non-retaliating NPC on the terrain in front of you. " +
            "The passive AI applies only to that spawned instance and its respawn is disabled.");
        await Assert.That(SpawnPassiveNpcCommand.AnchorAudit(null)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task TryParse_ExactThreeArguments_SelectsUnsignedAnchorBot()
    {
        var parsed = SpawnPassiveNpcCommand.TryParse(
            ["11180", "14.5", "20001"],
            out var templateId,
            out var distance,
            out var anchorBotId,
            out var error);

        await Assert.That(parsed).IsTrue();
        await Assert.That(templateId).IsEqualTo(11180u);
        await Assert.That(distance).IsEqualTo(14.5f);
        await Assert.That(anchorBotId).IsEqualTo(20001u);
        await Assert.That(error).IsNull();
        await Assert.That(SpawnPassiveNpcCommand.TryParse(
            ["11180", "14.5", "20001"], out _, out _)).IsFalse();
    }

    [Test]
    public async Task TryParse_FourthYawOffsetUnderGermanCulture_UsesInvariantFiniteInclusiveDegrees()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            foreach (var (text, expected) in new[]
                     {
                         ("0", 0f),
                         ("45", 45f),
                         ("-45", -45f),
                         ("180", 180f),
                         ("-180", -180f)
                     })
            {
                var parsed = SpawnPassiveNpcCommand.TryParse(
                    ["11180", "14.5", "20001", text],
                    out var templateId,
                    out var distance,
                    out var anchorBotId,
                    out var yawOffsetDegrees,
                    out var error);

                await Assert.That(parsed).IsTrue();
                await Assert.That(templateId).IsEqualTo(11180u);
                await Assert.That(distance).IsEqualTo(14.5f);
                await Assert.That(anchorBotId).IsEqualTo(20001u);
                await Assert.That(yawOffsetDegrees).IsEqualTo(expected);
                await Assert.That(error).IsNull();
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task TryParse_InvalidYawOffset_ReturnsPreciseError()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            foreach (var value in new[]
                     {
                         null,
                         string.Empty,
                         "NaN",
                         "Infinity",
                         "-Infinity",
                         "malformed",
                         "45,5",
                         "-180.1",
                         "180.1"
                     })
            {
                var parsed = SpawnPassiveNpcCommand.TryParse(
                    ["11180", "12", "20001", value],
                    out _,
                    out _,
                    out _,
                    out _,
                    out var error);

                await Assert.That(parsed).IsFalse();
                await Assert.That(error).IsEqualTo(SpawnPassiveNpcCommand.InvalidYawOffsetError);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task TryParse_InvalidSuppliedAnchorId_ReturnsPreciseError()
    {
        foreach (var value in new[] { null, string.Empty, "0", "-1", "abc", "1.0" })
        {
            var parsed = SpawnPassiveNpcCommand.TryParse(
                ["11180", "12", value],
                out _,
                out _,
                out var anchorBotId,
                out var error);

            await Assert.That(parsed).IsFalse();
            await Assert.That(anchorBotId).IsNull();
            await Assert.That(error).IsEqualTo(SpawnPassiveNpcCommand.InvalidAnchorIdError);
        }
    }

    [Test]
    public async Task TryParse_InvalidArityOrLegacyFields_KeepsHelpPath()
    {
        var invalidArity = SpawnPassiveNpcCommand.TryParse(
            ["11180", "12", "20001", "extra"], out _, out _, out _, out var arityError);
        var invalidTemplate = SpawnPassiveNpcCommand.TryParse(
            ["invalid", "12", "20001"], out _, out _, out _, out var templateError);
        var invalidDistance = SpawnPassiveNpcCommand.TryParse(
            ["11180", "4.9", "20001"], out _, out _, out _, out var distanceError);
        var invalidNewArity = SpawnPassiveNpcCommand.TryParse(
            ["11180", "12", "20001", "45", "extra"],
            out _,
            out _,
            out _,
            out _,
            out var newArityError);

        await Assert.That(invalidArity).IsFalse();
        await Assert.That(invalidTemplate).IsFalse();
        await Assert.That(invalidDistance).IsFalse();
        await Assert.That(invalidNewArity).IsFalse();
        await Assert.That(arityError).IsNull();
        await Assert.That(templateError).IsNull();
        await Assert.That(distanceError).IsNull();
        await Assert.That(newArityError).IsNull();
    }

    [Test]
    public async Task TryParse_LegacyBoundsRemainInclusive()
    {
        var minimum = SpawnPassiveNpcCommand.TryParse(["11180", "5"], out _, out var minimumDistance);
        var maximum = SpawnPassiveNpcCommand.TryParse(["11180", "100"], out _, out var maximumDistance);

        await Assert.That(minimum).IsTrue();
        await Assert.That(maximum).IsTrue();
        await Assert.That(minimumDistance).IsEqualTo(5f);
        await Assert.That(maximumDistance).IsEqualTo(100f);
    }

    [Arguments("0")]
    [Arguments("abc")]
    [Arguments("NaN")]
    [Arguments("4.9")]
    [Arguments("100.1")]
    [Test]
    public async Task TryParse_InvalidInput_IsRejected(string value)
    {
        string[] args = value is "0" or "abc" ? [value] : ["11180", value];

        await Assert.That(SpawnPassiveNpcCommand.TryParse(args, out _, out _)).IsFalse();
    }

    [Test]
    public async Task Execute_LegacyPathNeverResolvesBotAndKeepsExactWorldGuard()
    {
        var resolverCalls = 0;
        SpawnPassiveNpcCommand.ActiveBotResolver = _ =>
        {
            resolverCalls++;
            throw new InvalidOperationException("Legacy execution must not resolve a bot.");
        };
        var messages = CaptureMessages(out var output);

        new SpawnPassiveNpcCommand().Execute(
            new Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180"],
            output);

        await Assert.That(resolverCalls).IsEqualTo(0);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
    }

    [Test]
    public async Task Execute_InvalidAnchorIdFailsBeforeResolutionWithPreciseError()
    {
        var resolverCalls = 0;
        SpawnPassiveNpcCommand.ActiveBotResolver = _ =>
        {
            resolverCalls++;
            return null;
        };
        var messages = CaptureMessages(out var output);

        new SpawnPassiveNpcCommand().Execute(
            new Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180", "12", "0"],
            output);

        await Assert.That(resolverCalls).IsEqualTo(0);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000Anchor bot ID must be a nonzero unsigned integer.|r");
    }

    [Test]
    public async Task Execute_InvalidYawOffsetFailsBeforeResolutionWithoutStateMutation()
    {
        var (world, bot) = CreateQualifiedAnchor();
        var previousTarget = new Npc { ObjId = 99001 };
        bot.CurrentTarget = previousTarget;
        bot.IsInBattle = true;
        var originalPosition = bot.Transform.World.Position;
        var originalRotation = bot.Transform.World.Rotation;
        var resolverCalls = 0;
        var groundCalls = 0;
        SpawnPassiveNpcCommand.ActiveBotResolver = _ =>
        {
            resolverCalls++;
            return bot;
        };
        SpawnPassiveNpcCommand.GroundHeightResolver = (_, _, _, _) =>
        {
            groundCalls++;
            return 0f;
        };
        var messages = CaptureMessages(out var output);

        new SpawnPassiveNpcCommand().Execute(
            new Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180", "12", "20001", "45,5"],
            output);

        await Assert.That(resolverCalls).IsEqualTo(0);
        await Assert.That(groundCalls).IsEqualTo(0);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000Yaw offset degrees must be a finite invariant number from -180 through 180.|r");
        await Assert.That(bot.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(bot.CurrentTarget).IsSameReferenceAs(previousTarget);
        await Assert.That(bot.IsInBattle).IsTrue();
        await Assert.That(bot.Transform.World.Position).IsEqualTo(originalPosition);
        await Assert.That(bot.Transform.World.Rotation).IsEqualTo(originalRotation);
    }

    [Test]
    public async Task Execute_AbsentBotFailsBeforeWorldTemplateOrNpcCreation()
    {
        var resolverCalls = 0;
        SpawnPassiveNpcCommand.ActiveBotResolver = _ =>
        {
            resolverCalls++;
            return null;
        };
        var messages = CaptureMessages(out var output);

        new SpawnPassiveNpcCommand().Execute(
            new Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180", "12", "20001"],
            output);

        await Assert.That(resolverCalls).IsEqualTo(1);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000Active bot anchor 20001 is not active.|r");
    }

    [Test]
    public async Task ResolveAnchor_WorldlessBotFailsClosed()
    {
        var bot = BotTestFixture.MakeBot(20001, Vector3.Zero);

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(error).IsEqualTo("Active bot anchor 20001 is not in a world instance.");
    }

    [Test]
    public async Task ResolveAnchor_ZoneZeroBotFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor(zoneId: 0);

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(error).IsEqualTo("Active bot anchor 20001 has no qualified zone.");
    }

    [Test]
    public async Task ResolveAnchor_NonFiniteBotFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor();
        bot.Transform.Local.SetPosition(float.NaN, 202.5f, 303.5f);

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(error).IsEqualTo("Active bot anchor 20001 has a non-finite transform.");
    }

    [Test]
    public async Task ResolveAnchor_MismatchedBotFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor(botId: 20002);

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            20001, _ => bot, out var anchor, out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(error).IsEqualTo("Active bot anchor 20001 resolved to bot 20002.");
    }

    [Test]
    public async Task ResolveAnchor_InconsistentInstanceFailsClosed()
    {
        var (world, bot) = CreateQualifiedAnchor();
        BotTestFixture.SetPrivateField(bot.Transform, "_instanceId", world.Id + 1);

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 has an inconsistent world or instance boundary.");
    }

    [Test]
    public async Task ResolveAnchor_InconsistentWorldIdFailsClosed()
    {
        var (world, bot) = CreateQualifiedAnchor();
        BotTestFixture.SetPrivateField(bot.Transform, "<WorldId>k__BackingField", world.Template.Id + 1);

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 has an inconsistent world or instance boundary.");
    }

    [Test]
    public async Task ResolveAnchor_MissingWorldTemplateFailsClosedWithPreciseError()
    {
        var (world, bot) = CreateQualifiedAnchor();
        var template = world.Template;
        try
        {
            BotTestFixture.SetPrivateField<WorldTemplate>(world, "<Template>k__BackingField", null);

            var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
                bot.Id, _ => bot, out var anchor, out var error);
            anchor?.Dispose();

            await Assert.That(resolved).IsFalse();
            await Assert.That(anchor).IsNull();
            await Assert.That(error)
                .IsEqualTo("Active bot anchor 20001 does not have a world template.");
        }
        finally
        {
            BotTestFixture.SetPrivateField(world, "<Template>k__BackingField", template);
        }
    }

    [Test]
    public async Task ResolveAnchor_ConcurrentDepartureFailsAsStale()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var calls = 0;

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id,
            _ => ++calls == 1 ? bot : null,
            out var anchor,
            out var error);
        anchor?.Dispose();

        await Assert.That(resolved).IsFalse();
        await Assert.That(anchor).IsNull();
        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 became stale while its transform was captured.");
    }

    [Test]
    public async Task ResolveAnchor_QualifiedBotSnapshotsWithoutMutationOrTargetControl()
    {
        var (world, bot) = CreateQualifiedAnchor();
        var previousTarget = new Npc { ObjId = 99001 };
        bot.CurrentTarget = previousTarget;
        bot.IsInBattle = true;
        var originalPosition = bot.Transform.World.Position;
        var originalRotation = bot.Transform.World.Rotation;

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        using var anchorScope = anchor;

        await Assert.That(resolved).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(anchor.BotId).IsEqualTo(bot.Id);
        await Assert.That(anchor.World).IsSameReferenceAs(world);
        await Assert.That(anchor.WorldId).IsEqualTo(world.Template.Id);
        await Assert.That(anchor.ZoneId).IsEqualTo(601u);
        await Assert.That(anchor.InstanceId).IsEqualTo(world.Id);
        await Assert.That(anchor.Transform.GameObject).IsNull();
        await Assert.That(anchor.Transform.Parent).IsNull();
        await Assert.That(bot.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(bot.CurrentTarget).IsSameReferenceAs(previousTarget);
        await Assert.That(bot.IsInBattle).IsTrue();
        await Assert.That(bot.Transform.World.Position).IsEqualTo(originalPosition);
        await Assert.That(bot.Transform.World.Rotation).IsEqualTo(originalRotation);

        var detachedPosition = anchor.Transform.World.Position;
        bot.Transform.Local.SetPosition(999f, 998f, 997f);
        await Assert.That(anchor.Transform.World.Position).IsEqualTo(detachedPosition);
    }

    [Test]
    public async Task ResolveAnchor_DefaultWorldTemplateZeroSnapshotsWithoutMutationOrTargetControl()
    {
        var (world, bot) = CreateQualifiedAnchor(worldTemplateId: 0);
        var previousTarget = new Npc { ObjId = 99001 };
        bot.CurrentTarget = previousTarget;
        bot.IsInBattle = true;
        var originalPosition = bot.Transform.World.Position;
        var originalRotation = bot.Transform.World.Rotation;

        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        using var anchorScope = anchor;

        await Assert.That(resolved).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(world.Template.Id).IsEqualTo(0u);
        await Assert.That(anchor.WorldId).IsEqualTo(0u);
        await Assert.That(anchor.ZoneId).IsEqualTo(601u);
        await Assert.That(anchor.InstanceId).IsEqualTo(43u);
        await Assert.That(anchor.Transform.GameObject).IsNull();
        await Assert.That(anchor.Transform.Parent).IsNull();
        await Assert.That(float.IsFinite(anchor.Transform.World.Position.X)).IsTrue();
        await Assert.That(float.IsFinite(anchor.Transform.World.Position.Y)).IsTrue();
        await Assert.That(float.IsFinite(anchor.Transform.World.Position.Z)).IsTrue();
        await Assert.That(float.IsFinite(anchor.Transform.World.Rotation.X)).IsTrue();
        await Assert.That(float.IsFinite(anchor.Transform.World.Rotation.Y)).IsTrue();
        await Assert.That(float.IsFinite(anchor.Transform.World.Rotation.Z)).IsTrue();
        await Assert.That(bot.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(bot.CurrentTarget).IsSameReferenceAs(previousTarget);
        await Assert.That(bot.IsInBattle).IsTrue();
        await Assert.That(bot.Transform.World.Position).IsEqualTo(originalPosition);
        await Assert.That(bot.Transform.World.Rotation).IsEqualTo(originalRotation);

        var detachedPosition = anchor.Transform.World.Position;
        bot.Transform.Local.SetPosition(999f, 998f, 997f);
        await Assert.That(anchor.Transform.World.Position).IsEqualTo(detachedPosition);
    }

    [Test]
    public async Task CreateSpawnPosition_UsesDetachedAnchorAndPreservesTerrainWorldZoneAndInstanceAudit()
    {
        var (world, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        var sourcePosition = anchor.Transform.World.Position;
        var sourceRotation = anchor.Transform.World.Rotation;
        (uint zoneId, float x, float y, float z)? heightRequest = null;

        var spawnPosition = SpawnPassiveNpcCommand.CreateSpawnPosition(
            anchor.Transform,
            12f,
            anchor.WorldId,
            (zoneId, x, y, z) =>
            {
                heightRequest = (zoneId, x, y, z);
                return 333.25f;
            });

        await Assert.That(resolved).IsTrue();
        await Assert.That(spawnPosition.WorldId).IsEqualTo(world.Template.Id);
        await Assert.That(spawnPosition.ZoneId).IsEqualTo(anchor.ZoneId);
        await Assert.That(spawnPosition.Z).IsEqualTo(333.25f);
        await Assert.That(spawnPosition.X)
            .IsEqualTo(sourcePosition.X - 12f * MathF.Sin(sourceRotation.Z)).Within(1e-4f);
        await Assert.That(spawnPosition.Y)
            .IsEqualTo(sourcePosition.Y + 12f * MathF.Cos(sourceRotation.Z)).Within(1e-4f);
        await Assert.That(heightRequest.Value.zoneId).IsEqualTo(anchor.ZoneId);
        await Assert.That(anchor.Transform.World.Position).IsEqualTo(sourcePosition);
        await Assert.That(anchor.Transform.World.Rotation).IsEqualTo(sourceRotation);
        await Assert.That(SpawnPassiveNpcCommand.AnchorAudit(anchor))
            .IsEqualTo("anchorBotId=20001, anchorZone=601, anchorInstance=43, ");
    }

    [Test]
    public async Task CreateSpawnPosition_DefaultWorldTemplateZeroPreservesWorldZoneInstanceAndDetachedSource()
    {
        var (world, bot) = CreateQualifiedAnchor(worldTemplateId: 0);
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out var error);
        using var anchorScope = anchor;
        var sourcePosition = anchor.Transform.World.Position;
        var sourceRotation = anchor.Transform.World.Rotation;
        (uint zoneId, float x, float y, float z)? heightRequest = null;

        var spawnPosition = SpawnPassiveNpcCommand.CreateSpawnPosition(
            anchor.Transform,
            12f,
            anchor.WorldId,
            (zoneId, x, y, z) =>
            {
                heightRequest = (zoneId, x, y, z);
                return 333.25f;
            });

        await Assert.That(resolved).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(world.Template.Id).IsEqualTo(0u);
        await Assert.That(anchor.WorldId).IsEqualTo(0u);
        await Assert.That(spawnPosition.WorldId).IsEqualTo(0u);
        await Assert.That(spawnPosition.ZoneId).IsEqualTo(anchor.ZoneId);
        await Assert.That(anchor.InstanceId).IsEqualTo(world.Id);
        await Assert.That(spawnPosition.Z).IsEqualTo(333.25f);
        await Assert.That(float.IsFinite(spawnPosition.X)).IsTrue();
        await Assert.That(float.IsFinite(spawnPosition.Y)).IsTrue();
        await Assert.That(float.IsFinite(spawnPosition.Z)).IsTrue();
        await Assert.That(heightRequest.Value.zoneId).IsEqualTo(anchor.ZoneId);
        await Assert.That(anchor.Transform.World.Position).IsEqualTo(sourcePosition);
        await Assert.That(anchor.Transform.World.Rotation).IsEqualTo(sourceRotation);
        await Assert.That(SpawnPassiveNpcCommand.AnchorAudit(anchor))
            .IsEqualTo("anchorBotId=20001, anchorZone=601, anchorInstance=43, ");
    }

    [Test]
    public async Task CreateSpawnPosition_OmittedAndExplicitZeroYawOffsetAreBitExact()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        var sourcePosition = anchor.Transform.World.Position;
        var sourceRotation = anchor.Transform.World.Rotation;

        var omitted = SpawnPassiveNpcCommand.CreateSpawnPosition(
            anchor.Transform,
            12f,
            anchor.WorldId,
            static (_, _, _, _) => 0f);
        var explicitZero = SpawnPassiveNpcCommand.CreateSpawnPosition(
            anchor.Transform,
            12f,
            0f,
            anchor.WorldId,
            static (_, _, _, _) => 0f);

        await Assert.That(resolved).IsTrue();
        await Assert.That(BitConverter.SingleToInt32Bits(explicitZero.X))
            .IsEqualTo(BitConverter.SingleToInt32Bits(omitted.X));
        await Assert.That(BitConverter.SingleToInt32Bits(explicitZero.Y))
            .IsEqualTo(BitConverter.SingleToInt32Bits(omitted.Y));
        await Assert.That(BitConverter.SingleToInt32Bits(explicitZero.Z))
            .IsEqualTo(BitConverter.SingleToInt32Bits(omitted.Z));
        await Assert.That(BitConverter.SingleToInt32Bits(explicitZero.Roll))
            .IsEqualTo(BitConverter.SingleToInt32Bits(omitted.Roll));
        await Assert.That(BitConverter.SingleToInt32Bits(explicitZero.Pitch))
            .IsEqualTo(BitConverter.SingleToInt32Bits(omitted.Pitch));
        await Assert.That(BitConverter.SingleToInt32Bits(explicitZero.Yaw))
            .IsEqualTo(BitConverter.SingleToInt32Bits(omitted.Yaw));
        await Assert.That(explicitZero.WorldId).IsEqualTo(omitted.WorldId);
        await Assert.That(explicitZero.ZoneId).IsEqualTo(omitted.ZoneId);
        await Assert.That(anchor.Transform.World.Position).IsEqualTo(sourcePosition);
        await Assert.That(anchor.Transform.World.Rotation).IsEqualTo(sourceRotation);
    }

    [Arguments(45f)]
    [Arguments(-45f)]
    [Test]
    public async Task CreateSpawnPosition_SignedYawOffsetMatchesPinnedSinglePrecisionDetachedGeometry(
        float yawOffsetDegrees)
    {
        var (world, bot) = CreateQualifiedAnchor();
        var previousTarget = new Npc { ObjId = 99001 };
        bot.CurrentTarget = previousTarget;
        bot.IsInBattle = true;
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        var sourcePosition = anchor.Transform.World.Position;
        var sourceRotation = anchor.Transform.World.Rotation;
        (uint zoneId, float x, float y, float z)? heightRequest = null;
        var placementYaw = sourceRotation.Z + yawOffsetDegrees.DegToRad();
        var expectedX = sourcePosition.X - 12f * MathF.Sin(placementYaw);
        var expectedY = sourcePosition.Y + 12f * MathF.Cos(placementYaw);
        var expectedFacing = ((float)MathUtil.CalculateAngleFrom(
            expectedX,
            expectedY,
            sourcePosition.X,
            sourcePosition.Y)).DegToRad();

        var spawnPosition = SpawnPassiveNpcCommand.CreateSpawnPosition(
            anchor.Transform,
            12f,
            yawOffsetDegrees,
            anchor.WorldId,
            (zoneId, x, y, z) =>
            {
                heightRequest = (zoneId, x, y, z);
                return 333.25f;
            });

        await Assert.That(resolved).IsTrue();
        await Assert.That(BitConverter.SingleToInt32Bits(spawnPosition.X))
            .IsEqualTo(BitConverter.SingleToInt32Bits(expectedX));
        await Assert.That(BitConverter.SingleToInt32Bits(spawnPosition.Y))
            .IsEqualTo(BitConverter.SingleToInt32Bits(expectedY));
        await Assert.That(BitConverter.SingleToInt32Bits(spawnPosition.Yaw))
            .IsEqualTo(BitConverter.SingleToInt32Bits(expectedFacing));
        await Assert.That(spawnPosition.Z).IsEqualTo(333.25f);
        await Assert.That(spawnPosition.WorldId).IsEqualTo(world.Template.Id);
        await Assert.That(spawnPosition.ZoneId).IsEqualTo(anchor.ZoneId);
        await Assert.That(anchor.InstanceId).IsEqualTo(world.Id);
        await Assert.That(heightRequest.Value.zoneId).IsEqualTo(anchor.ZoneId);
        await Assert.That(BitConverter.SingleToInt32Bits(heightRequest.Value.x))
            .IsEqualTo(BitConverter.SingleToInt32Bits(expectedX));
        await Assert.That(BitConverter.SingleToInt32Bits(heightRequest.Value.y))
            .IsEqualTo(BitConverter.SingleToInt32Bits(expectedY));
        await Assert.That(BitConverter.SingleToInt32Bits(heightRequest.Value.z))
            .IsEqualTo(BitConverter.SingleToInt32Bits(sourcePosition.Z));
        await Assert.That(bot.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(bot.CurrentTarget).IsSameReferenceAs(previousTarget);
        await Assert.That(bot.IsInBattle).IsTrue();
        await Assert.That(bot.Transform.World.Position).IsEqualTo(sourcePosition);
        await Assert.That(bot.Transform.World.Rotation).IsEqualTo(sourceRotation);
        await Assert.That(anchor.Transform.World.Position).IsEqualTo(sourcePosition);
        await Assert.That(anchor.Transform.World.Rotation).IsEqualTo(sourceRotation);
        await Assert.That(SpawnPassiveNpcCommand.AnchorAudit(anchor))
            .IsEqualTo("anchorBotId=20001, anchorZone=601, anchorInstance=43, ");
    }

    [Test]
    public async Task IsAnchorStillCurrent_MissingWorldTemplateFailsClosedWithPreciseError()
    {
        var (world, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        var template = world.Template;
        bool current;
        string error;
        try
        {
            BotTestFixture.SetPrivateField<WorldTemplate>(world, "<Template>k__BackingField", null);
            current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out error);
        }
        finally
        {
            BotTestFixture.SetPrivateField(world, "<Template>k__BackingField", template);
        }

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 does not have a world template.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_ResolverIdentityChangeFailsAsStale()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        var replacement = BotTestFixture.MakeBot(bot.Id, Vector3.Zero);

        var current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(
            anchor, _ => replacement, out var error);

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 became stale while its transform was captured.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_ParentWorldChangeFailsAsStale()
    {
        var (world, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        bool current;
        string error;
        try
        {
            BotTestFixture.SetPrivateField<WorldInstance>(bot, "_parentWorld", null);
            current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out error);
        }
        finally
        {
            BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        }

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 became stale while its transform was captured.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_TransformSwapFailsAsStale()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        var originalTransform = bot.Transform;
        using var replacementTransform = new Transform(bot);
        bool current;
        string error;
        try
        {
            bot.Transform = replacementTransform;
            current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out error);
        }
        finally
        {
            bot.Transform = originalTransform;
        }

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 became stale while its transform was captured.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_LiveZoneChangeFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", anchor.ZoneId + 1);

        var current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out var error);

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 has an inconsistent world or instance boundary.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_LiveInstanceChangeFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        BotTestFixture.SetPrivateField(bot.Transform, "_instanceId", anchor.InstanceId + 1);

        var current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out var error);

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 has an inconsistent world or instance boundary.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_LiveWorldIdChangeFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        BotTestFixture.SetPrivateField(bot.Transform, "<WorldId>k__BackingField", anchor.WorldId + 1);

        var current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out var error);

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error)
            .IsEqualTo("Active bot anchor 20001 has an inconsistent world or instance boundary.");
    }

    [Test]
    public async Task IsAnchorStillCurrent_LiveNonFiniteTransformFailsClosed()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var resolved = SpawnPassiveNpcCommand.TryResolveActiveBotAnchor(
            bot.Id, _ => bot, out var anchor, out _);
        using var anchorScope = anchor;
        bot.Transform.Local.SetPosition(float.PositiveInfinity, 202.5f, 303.5f);

        var current = SpawnPassiveNpcCommand.IsAnchorStillCurrent(anchor, _ => bot, out var error);

        await Assert.That(resolved).IsTrue();
        await Assert.That(current).IsFalse();
        await Assert.That(error).IsEqualTo("Active bot anchor 20001 has a non-finite transform.");
    }

    [Test]
    public async Task Execute_AnchorThatDepartsAfterPlacementFailsBeforeSpawnerCreation()
    {
        var (_, bot) = CreateQualifiedAnchor();
        var npcManager = NpcManager.Instance;
        npcManager.GetAllTemplates()[11180] = new NpcTemplate { Id = 11180 };
        SpawnPassiveNpcCommand.GroundHeightResolver = static (_, _, _, _) => 0f;
        var calls = 0;
        SpawnPassiveNpcCommand.ActiveBotResolver = _ => ++calls <= 2 ? bot : null;
        var messages = CaptureMessages(out var output);

        new SpawnPassiveNpcCommand().Execute(
            new Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180", "12", "20001"],
            output);

        await Assert.That(calls).IsEqualTo(3);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000Active bot anchor 20001 became stale while its transform was captured.|r");
    }

    [Test]
    public async Task ApplyPassiveAi_DetachesExistingAiAndRegistersDummyBehavior()
    {
        var npc = new Npc();
        var previousAi = new DummyAiCharacter { Owner = npc };
        previousAi.Start();
        previousAi.GoToIdle();
        npc.Ai = previousAi;
        NpcAi registeredAi = null;

        var passiveAi = SpawnPassiveNpcCommand.ApplyPassiveAi(npc, ai => registeredAi = ai);

        await Assert.That(previousAi.Owner).IsNull();
        await Assert.That(npc.Ai).IsSameReferenceAs(passiveAi);
        await Assert.That(registeredAi).IsSameReferenceAs(passiveAi);
        await Assert.That(passiveAi.Owner).IsSameReferenceAs(npc);
        await Assert.That(passiveAi.GetCurrentBehavior()).IsTypeOf<DummyBehavior>();
        await Assert.That(npc.CurrentTarget).IsNull();
    }

    [Test]
    public async Task Execute_SystemActorWithoutQualifiedBot_StopsAtWorldGuardForT057ZoneZeroOrigin()
    {
        var t057Origin = new WorldSpawnPosition { ZoneId = 0, X = 0f, Y = 0f, Z = 0f };
        var worldManager = CreateWorldManager();
        var world = new WorldInstance(
            new WorldTemplate { Id = 1, Name = "main_world", SpawnPosition = t057Origin },
            0,
            true,
            WorldManager.DefaultInstanceId);
        SetMainWorld(worldManager, world);
        var npcManager = CreateNpcManager();
        var botManager = new BotManager(_ => null);
        RegisterSingletons(worldManager, npcManager, botManager);
        var actor = SystemActor.Create();
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(actor, ["11180"], output.Object);

        await Assert.That(actor.ParentWorld).IsNull();
        await Assert.That(actor.Transform.ZoneId).IsEqualTo(0u);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
        await Assert.That(messages).DoesNotContain("|cFFFFFFFF[spawnpassive]|r |cFFFF0000NPC 11180 does not exist.|r");
    }

    [Test]
    public async Task Execute_SystemActorWithQualifiedAnchor_AdvancesPastWorldGuard()
    {
        var worldManager = CreateWorldManager();
        var world = new WorldInstance(new WorldTemplate { Id = 9, Name = "bot_world" }, 0, true, 43);
        SetMainWorld(worldManager, world);
        var npcManager = CreateNpcManager();
        var botManager = new BotManager(_ => null);
        RegisterSingletons(worldManager, npcManager, botManager);
        var anchorPosition = new WorldSpawnPosition
        {
            WorldId = 9,
            ZoneId = 601,
            X = 101.5f,
            Y = 202.5f,
            Z = 303.5f,
            Roll = 0.1f,
            Pitch = 0.2f,
            Yaw = 0.3f
        };
        var bot = BotTestFixture.MakeBot(10, Vector3.Zero);
        bot.Transform.InstanceId = world.Id;
        BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", anchorPosition.ZoneId);
        bot.Transform.Local.SetPosition(anchorPosition.X, anchorPosition.Y, anchorPosition.Z);
        bot.Transform.Local.SetRotation(anchorPosition.Roll, anchorPosition.Pitch, anchorPosition.Yaw);
        bot.ParentWorld = world;
        BotTestFixture.GetDictionary<AAEmu.Game.Models.Game.Char.Character>(botManager, "ActiveBots")[bot.Id] = bot;
        var actor = SystemActor.Create();
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(actor, ["11180"], output.Object);

        await Assert.That(actor.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(actor.Transform.InstanceId).IsEqualTo(world.Id);
        await Assert.That(actor.Transform.ZoneId).IsEqualTo(anchorPosition.ZoneId);
        await Assert.That(messages).Contains("|cFFFFFFFF[spawnpassive]|r |cFFFF0000NPC 11180 does not exist.|r");
        await Assert.That(messages).DoesNotContain(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
    }

    [Test]
    public async Task Execute_CharacterWithoutWorld_KeepsExactWorldGuard()
    {
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(
            new AAEmu.Game.Models.Game.Char.Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180"],
            output.Object);

        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
    }

    private (WorldInstance World, Character Bot) CreateQualifiedAnchor(
        uint botId = 20001,
        uint zoneId = 601,
        uint worldTemplateId = 9,
        uint instanceId = 43)
    {
        var worldManager = CreateWorldManager();
        var world = new WorldInstance(
            new WorldTemplate { Id = worldTemplateId, Name = "bot_world" },
            0,
            true,
            instanceId);
        SetMainWorld(worldManager, world);
        var npcManager = CreateNpcManager();
        var botManager = new BotManager(_ => null);
        RegisterSingletons(worldManager, npcManager, botManager);

        var bot = BotTestFixture.MakeBot(botId, Vector3.Zero);
        bot.Transform.InstanceId = world.Id;
        BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", zoneId);
        bot.Transform.Local.SetPosition(101.5f, 202.5f, 303.5f);
        bot.Transform.Local.SetRotation(0.1f, 0.2f, 0.3f);
        bot.ParentWorld = world;
        BotTestFixture.GetDictionary<Character>(botManager, "ActiveBots")[bot.Id] = bot;
        return (world, bot);
    }

    private static List<string> CaptureMessages(out IMessageOutput output)
    {
        var messages = new List<string>();
        var mock = Mock.Of<IMessageOutput>();
        mock.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));
        output = mock.Object;
        return messages;
    }

    private void RegisterSingletons(WorldManager worldManager, NpcManager npcManager, BotManager botManager)
    {
        BotTestFixture.ResetSingleton<NpcManager>();
        BotTestFixture.ResetSingleton<WorldManager>();
        BotTestFixture.ResetSingleton<ZoneManager>();
        BotTestFixture.ResetSingleton<CharacterManager>();
        BotTestFixture.ResetSingleton<BotManager>();
        var zoneManager = new ZoneManager(worldManager);
        BotTestFixture.SetPrivateField(zoneManager, "_zones", new Dictionary<uint, Zone>());
        var taskManager = Mock.Of<ITaskManager>();
        var characterManager = new CharacterManager(
            worldManager,
            Mock.Of<IAccountManager>().Object,
            Mock.Of<INameManager>().Object,
            Mock.Of<ICharacterIdManager>().Object,
            Mock.Of<IFactionManager>().Object,
            Mock.Of<ISkillManager>().Object,
            Mock.Of<IItemManager>().Object,
            Mock.Of<IHousingManager>().Object,
            Mock.Of<IFamilyManager>().Object,
            Mock.Of<IMailManager>().Object,
            taskManager.Object);
        var services = new ServiceCollection();
        services.AddSingleton(worldManager);
        services.AddSingleton(zoneManager);
        services.AddSingleton<IZoneManager>(zoneManager);
        services.AddSingleton(characterManager);
        services.AddSingleton(npcManager);
        services.AddSingleton(botManager);
        _serviceProvider = services.BuildServiceProvider();
        SingletonContainer.ServiceProvider = _serviceProvider;
    }

    private static NpcManager CreateNpcManager()
    {
        return new NpcManager(
            Mock.Of<IObjectIdManager>().Object,
            Mock.Of<IModelManager>().Object,
            Mock.Of<IFactionManager>().Object,
            Mock.Of<IItemManager>().Object,
            Mock.Of<IAIManager>().Object);
    }

    private static WorldManager CreateWorldManager()
    {
        return new WorldManager(
            Mock.Of<ITickManager>().Object,
            Mock.Of<IWorldIdManager>().Object,
            new Lazy<IZoneManager>(() => Mock.Of<IZoneManager>().Object),
            new Lazy<IIndunManager>(() => Mock.Of<IIndunManager>().Object),
            new Lazy<IFamilyManager>(() => Mock.Of<IFamilyManager>().Object));
    }

    private static void SetMainWorld(WorldManager worldManager, WorldInstance world)
    {
        var worlds = (ConcurrentDictionary<uint, WorldInstance>)typeof(WorldManager)
            .GetField("_worlds", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(worldManager);
        worlds[world.Id] = world;
        worldManager.MainWorld = world;
    }
}
