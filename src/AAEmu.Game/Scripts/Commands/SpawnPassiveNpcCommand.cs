using System.Globalization;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
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
        if (!TryParse(args, out var templateId, out var distance))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        if (character?.Transform == null || character.ParentWorld == null)
        {
            CommandManager.SendErrorText(this, messageOutput, "The command character is not in a world instance.");
            return;
        }

        if (!NpcManager.Instance.Exist(templateId))
        {
            CommandManager.SendErrorText(this, messageOutput, $"NPC {templateId} does not exist.");
            return;
        }

        using var spawnTransform = character.Transform.CloneDetached();
        spawnTransform.Local.AddDistanceToFront(distance);
        var spawnPosition = spawnTransform.CloneAsSpawnPosition();
        var groundZ = WorldManager.Instance.GetHeight(
            spawnPosition.ZoneId,
            spawnPosition.X,
            spawnPosition.Y,
            spawnPosition.Z);
        if (float.IsFinite(groundZ) && groundZ != 0f)
            spawnPosition.Z = groundZ;

        var angle = (float)MathUtil.CalculateAngleFrom(spawnTransform, character.Transform);
        spawnPosition.Yaw = angle.DegToRad();
        spawnPosition.Pitch = 0f;
        spawnPosition.Roll = 0f;

        var spawnerTemplate = new NpcSpawnerTemplate(0, templateId);
        var spawner = new NpcSpawner
        {
            ParentWorld = character.ParentWorld,
            Id = 0,
            SpawnerId = 0,
            UnitId = templateId,
            Position = spawnPosition,
            Template = spawnerTemplate
        };
        spawner.SpawnableNpcs = [.. spawnerTemplate.Npcs];

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
            "retaliation=disabled, displacement=disabled, respawn=disabled.");
    }

    internal static bool TryParse(string[] args, out uint templateId, out float distance)
    {
        templateId = 0;
        distance = DefaultDistance;
        if (args == null || args.Length is < 1 or > 2 ||
            !uint.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out templateId) ||
            templateId == 0)
            return false;

        if (args.Length == 2 &&
            (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out distance) ||
             !float.IsFinite(distance) || distance < 5f || distance > 100f))
            return false;

        return true;
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
