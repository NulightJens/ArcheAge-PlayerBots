using System.Globalization;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Spawns a mortal NPC whose AI cannot acquire, attack, or retaliate against targets.
/// The change is scoped to the spawned instance and does not modify the NPC template.
/// </summary>
public sealed class SpawnPassiveNpcCommand : ICommand
{
    internal const float DefaultDistance = 12f;
    internal const string InvalidAnchorIdError = "Anchor bot ID must be a nonzero unsigned integer.";

    internal static Func<uint, Character> ActiveBotResolver { get; set; } =
        static botId => BotManager.Instance.GetBot(botId);
    internal static Func<uint, float, float, float, float> GroundHeightResolver { get; set; } =
        static (zoneId, x, y, z) => WorldManager.Instance.GetHeight(
            zoneId,
            x,
            y
#if !PLAYERBOTS_AAEMU_3_0
            , z
#endif
        );

    public string[] CommandNames { get; set; } = ["spawnpassive", "passivenpc", "passiveboss"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "<npcTemplateId> [distance]";
    }

    public string GetCommandHelpText()
    {
        return "Spawns a killable, non-retaliating NPC on the terrain in front of you. " +
               "The passive AI applies only to that spawned instance and its respawn is disabled.";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!TryParse(args, out var templateId, out var distance, out var anchorBotId, out var parseError))
        {
            if (parseError == null)
                CommandManager.SendDefaultHelpText(this, messageOutput);
            else
                CommandManager.SendErrorText(this, messageOutput, parseError);
            return;
        }

        PassiveNpcAnchorSnapshot anchor = null;
        try
        {
            if (anchorBotId.HasValue &&
                !TryResolveActiveBotAnchor(anchorBotId.Value, ActiveBotResolver, out anchor, out var anchorError))
            {
                CommandManager.SendErrorText(this, messageOutput, anchorError);
                return;
            }

            if (anchor == null && (character?.Transform == null || character.ParentWorld == null))
            {
                CommandManager.SendErrorText(this, messageOutput, "The command character is not in a world instance.");
                return;
            }

            if (!NpcManager.Instance.Exist(templateId))
            {
                CommandManager.SendErrorText(this, messageOutput, $"NPC {templateId} does not exist.");
                return;
            }

            var sourceTransform = anchor?.Transform ?? character.Transform;
            var spawnWorld = anchor?.World ?? character.ParentWorld;
            var spawnPosition = CreateSpawnPosition(
                sourceTransform,
                distance,
                anchor?.WorldId,
                GroundHeightResolver);

            if (anchor != null &&
                !IsAnchorStillCurrent(anchor, ActiveBotResolver, out anchorError))
            {
                CommandManager.SendErrorText(this, messageOutput, anchorError);
                return;
            }

            var spawnerTemplate = new NpcSpawnerTemplate(0, templateId);
            var spawner = new NpcSpawner
            {
#if !PLAYERBOTS_AAEMU_3_0
                ParentWorld = spawnWorld,
#endif
                Id = 0,
#if !PLAYERBOTS_AAEMU_3_0
                SpawnerId = 0,
#endif
                UnitId = templateId,
                Position = spawnPosition,
                Template = spawnerTemplate
            };
#if !PLAYERBOTS_AAEMU_3_0
            spawner.SpawnableNpcs = [.. spawnerTemplate.Npcs];
#endif

            Npc npc;
            try
            {
                npc = spawner.Spawn(0);
            }
            catch (Exception exception)
            {
                CommandManager.SendErrorText(this, messageOutput,
                    $"Passive NPC {templateId} failed to spawn: {exception.Message}");
                return;
            }

            if (npc == null)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Passive NPC {templateId} failed to spawn.");
                return;
            }

            spawner.RespawnTime = 0;
            npc.SuppressActorDisplacement = true;
            ApplyPassiveAi(npc, AIManager.Instance.AddAi);

            var position = npc.Transform.World.Position;
            CommandManager.SendNormalText(this, messageOutput,
                $"Spawned passive NPC template={templateId}, objId={npc.ObjId}, level={npc.Level}, " +
                $"grade={npc.Template.NpcGradeId}, hp={npc.Hp}/{npc.MaxHp}, " +
                $"position=({position.X:F1}, {position.Y:F1}, {position.Z:F1}), " +
                AnchorAudit(anchor) +
                "retaliation=disabled, displacement=disabled, respawn=disabled.");
        }
        finally
        {
            anchor?.Dispose();
        }
    }

    internal static bool TryParse(string[] args, out uint templateId, out float distance)
    {
        if (args?.Length > 2)
        {
            templateId = 0;
            distance = DefaultDistance;
            return false;
        }

        return TryParse(args, out templateId, out distance, out _, out _);
    }

    internal static bool TryParse(
        string[] args,
        out uint templateId,
        out float distance,
        out uint? anchorBotId,
        out string error)
    {
        templateId = 0;
        distance = DefaultDistance;
        anchorBotId = null;
        error = null;
        if (args == null || args.Length is < 1 or > 3 ||
            !uint.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out templateId) ||
            templateId == 0)
            return false;

        if (args.Length >= 2 &&
            (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out distance) ||
             !float.IsFinite(distance) || distance < 5f || distance > 100f))
            return false;

        if (args.Length == 3)
        {
            if (!uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedAnchorBotId) ||
                parsedAnchorBotId == 0)
            {
                error = InvalidAnchorIdError;
                return false;
            }

            anchorBotId = parsedAnchorBotId;
        }

        return true;
    }

    internal static bool TryResolveActiveBotAnchor(
        uint anchorBotId,
        Func<uint, Character> activeBotResolver,
        out PassiveNpcAnchorSnapshot anchor,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(activeBotResolver);
        anchor = null;
        error = null;

        Character bot;
        try
        {
            bot = activeBotResolver(anchorBotId);
        }
        catch (Exception)
        {
            error = StaleAnchorError(anchorBotId);
            return false;
        }

        if (bot == null)
        {
            error = $"Active bot anchor {anchorBotId} is not active.";
            return false;
        }

        if (bot.Id != anchorBotId)
        {
            error = $"Active bot anchor {anchorBotId} resolved to bot {bot.Id}.";
            return false;
        }

        var world = bot.ParentWorld;
        var transform = bot.Transform;
        if (world == null)
        {
            error = $"Active bot anchor {anchorBotId} is not in a world instance.";
            return false;
        }

        if (transform == null)
        {
            error = $"Active bot anchor {anchorBotId} does not have a transform.";
            return false;
        }

        if (transform.ZoneId == 0)
        {
            error = $"Active bot anchor {anchorBotId} has no qualified zone.";
            return false;
        }

        if (world.Template == null)
        {
            error = MissingWorldTemplateError(anchorBotId);
            return false;
        }

        Transform transformSnapshot = null;
        try
        {
            transformSnapshot = transform.CloneDetached();
            var positionSnapshot = transformSnapshot.CloneAsSpawnPosition();
            if (!HasFiniteTransform(positionSnapshot))
            {
                error = $"Active bot anchor {anchorBotId} has a non-finite transform.";
                return false;
            }

            var worldId = world.Template.Id;
            var zoneId = transformSnapshot.ZoneId;
            var instanceId = transformSnapshot.InstanceId;
            if (world.Id != instanceId || transform.WorldId != worldId ||
                positionSnapshot.ZoneId != zoneId || zoneId == 0)
            {
                error = $"Active bot anchor {anchorBotId} has an inconsistent world or instance boundary.";
                return false;
            }

            anchor = new PassiveNpcAnchorSnapshot(
                anchorBotId,
                bot,
                world,
                transform,
                transformSnapshot,
                worldId,
                zoneId,
                instanceId);
            transformSnapshot = null;

            if (IsAnchorStillCurrent(anchor, activeBotResolver, out error))
                return true;

            anchor.Dispose();
            anchor = null;
            return false;
        }
        catch (Exception)
        {
            error = StaleAnchorError(anchorBotId);
            anchor?.Dispose();
            anchor = null;
            return false;
        }
        finally
        {
            transformSnapshot?.Dispose();
        }
    }

    internal static bool IsAnchorStillCurrent(
        PassiveNpcAnchorSnapshot anchor,
        Func<uint, Character> activeBotResolver,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(activeBotResolver);
        error = null;

        Character currentBot;
        try
        {
            currentBot = activeBotResolver(anchor.BotId);
        }
        catch (Exception)
        {
            error = StaleAnchorError(anchor.BotId);
            return false;
        }

        if (!ReferenceEquals(currentBot, anchor.Bot) ||
            !ReferenceEquals(anchor.Bot.ParentWorld, anchor.World) ||
            !ReferenceEquals(anchor.Bot.Transform, anchor.LiveTransform))
        {
            error = StaleAnchorError(anchor.BotId);
            return false;
        }

        var transform = anchor.LiveTransform;
        if (transform.ZoneId == 0)
        {
            error = $"Active bot anchor {anchor.BotId} has no qualified zone.";
            return false;
        }

        if (anchor.World.Template == null)
        {
            error = MissingWorldTemplateError(anchor.BotId);
            return false;
        }

        if (anchor.World.Template.Id != anchor.WorldId ||
            anchor.World.Id != anchor.InstanceId ||
            transform.WorldId != anchor.WorldId ||
            transform.ZoneId != anchor.ZoneId ||
            transform.InstanceId != anchor.InstanceId)
        {
            error = $"Active bot anchor {anchor.BotId} has an inconsistent world or instance boundary.";
            return false;
        }

        try
        {
            using var currentTransform = transform.CloneDetached();
            if (HasFiniteTransform(currentTransform.CloneAsSpawnPosition()))
                return true;
        }
        catch (Exception)
        {
            error = StaleAnchorError(anchor.BotId);
            return false;
        }

        error = $"Active bot anchor {anchor.BotId} has a non-finite transform.";
        return false;
    }

    internal static WorldSpawnPosition CreateSpawnPosition(
        Transform sourceTransform,
        float distance,
        uint? worldIdOverride,
        Func<uint, float, float, float, float> groundHeightResolver)
    {
        ArgumentNullException.ThrowIfNull(sourceTransform);
        ArgumentNullException.ThrowIfNull(groundHeightResolver);

        using var spawnTransform = sourceTransform.CloneDetached();
        spawnTransform.Local.AddDistanceToFront(distance);
        var spawnPosition = spawnTransform.CloneAsSpawnPosition();
        if (worldIdOverride.HasValue)
            spawnPosition.WorldId = worldIdOverride.Value;

        var groundZ = groundHeightResolver(
            spawnPosition.ZoneId,
            spawnPosition.X,
            spawnPosition.Y,
            spawnPosition.Z);
        if (float.IsFinite(groundZ) && groundZ != 0f)
            spawnPosition.Z = groundZ;

        var angle = (float)MathUtil.CalculateAngleFrom(spawnTransform, sourceTransform);
        spawnPosition.Yaw = angle.DegToRad();
        spawnPosition.Pitch = 0f;
        spawnPosition.Roll = 0f;
        return spawnPosition;
    }

    internal static string AnchorAudit(PassiveNpcAnchorSnapshot anchor)
    {
        return anchor == null
            ? string.Empty
            : $"anchorBotId={anchor.BotId}, anchorZone={anchor.ZoneId}, anchorInstance={anchor.InstanceId}, ";
    }

    private static bool HasFiniteTransform(WorldSpawnPosition position)
    {
        return float.IsFinite(position.X) &&
               float.IsFinite(position.Y) &&
               float.IsFinite(position.Z) &&
               float.IsFinite(position.Roll) &&
               float.IsFinite(position.Pitch) &&
               float.IsFinite(position.Yaw);
    }

    private static string StaleAnchorError(uint anchorBotId)
    {
        return $"Active bot anchor {anchorBotId} became stale while its transform was captured.";
    }

    private static string MissingWorldTemplateError(uint anchorBotId)
    {
        return $"Active bot anchor {anchorBotId} does not have a world template.";
    }

    internal static DummyAiCharacter ApplyPassiveAi(Npc npc, Action<NpcAi> registerAi)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(registerAi);

        if (npc.Ai != null)
        {
            npc.Ai.ShouldTick = false;
            npc.Ai.Owner = null;
        }

        foreach (var aggro in npc.AggroTable.Values.ToArray())
            npc.ClearAggroOfUnit(aggro.Owner);
        npc.CurrentTarget = null;

        var passiveAi = new DummyAiCharacter
        {
            Owner = npc,
            HomePosition = npc.Transform.World.Position,
            IdlePosition = npc.Transform.World.Position
        };
        npc.Ai = passiveAi;
        passiveAi.Start();
        passiveAi.GoToIdle();
        registerAi(passiveAi);
        return passiveAi;
    }
}

internal sealed class PassiveNpcAnchorSnapshot : IDisposable
{
    internal PassiveNpcAnchorSnapshot(
        uint botId,
        Character bot,
        WorldInstance world,
        Transform liveTransform,
        Transform transform,
        uint worldId,
        uint zoneId,
        uint instanceId)
    {
        BotId = botId;
        Bot = bot;
        World = world;
        LiveTransform = liveTransform;
        Transform = transform;
        WorldId = worldId;
        ZoneId = zoneId;
        InstanceId = instanceId;
    }

    public uint BotId { get; }
    internal Character Bot { get; }
    public WorldInstance World { get; }
    internal Transform LiveTransform { get; }
    public Transform Transform { get; }
    public uint WorldId { get; }
    public uint ZoneId { get; }
    public uint InstanceId { get; }

    public void Dispose()
    {
        Transform.Dispose();
    }
}
