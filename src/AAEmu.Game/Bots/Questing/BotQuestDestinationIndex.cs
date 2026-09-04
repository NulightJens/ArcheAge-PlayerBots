using System.Numerics;
using System.Runtime.CompilerServices;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.World;
#if PLAYERBOTS_AAEMU_3_0
using IndexedWorld = AAEmu.Game.Models.Game.World.World;
#else
using IndexedWorld = AAEmu.Game.Models.Game.World.WorldInstance;
#endif

namespace AAEmu.Game.Bots.Questing;

/// <summary>Indexes quest markers and static spawns once per world.</summary>
internal sealed class BotQuestDestinationIndex
{
    private readonly ConditionalWeakTable<IndexedWorld, WorldIndex> _worlds = new();

    public static BotQuestDestinationIndex Instance { get; } = new();

    internal IReadOnlyList<IndexedNpcSpawn> GetNpcSpawns(IndexedWorld world, uint npcTemplateId) =>
        world != null && npcTemplateId != 0 &&
        GetWorldIndex(world).NpcSpawns.TryGetValue(npcTemplateId, out var spawns)
            ? spawns
            : [];

    internal IReadOnlyList<IndexedQuestStart> GetNpcQuestStarts(IndexedWorld world) =>
        world == null ? [] : GetWorldIndex(world).QuestStarts.Value;

    internal IReadOnlyList<SphereQuest> GetQuestSpheres(IndexedWorld world, uint componentId)
    {
        if (world == null || componentId == 0)
            return [];
#if PLAYERBOTS_AAEMU_3_0
        return SphereQuestManager.Instance.GetQuestSpheres(componentId) ?? [];
#else
        return world.SphereQuestManager?.GetQuestSpheres(componentId) ?? [];
#endif
    }

    private WorldIndex GetWorldIndex(IndexedWorld world) =>
        _worlds.GetValue(world, BuildWorldIndex);

    private static WorldIndex BuildWorldIndex(IndexedWorld world)
    {
#if PLAYERBOTS_AAEMU_3_0
        // The 3.0 host has no immutable static-spawn snapshot seam.
        return WorldIndex.Empty;
#else
        var mutableSpawns = new Dictionary<uint, List<IndexedNpcSpawn>>();
        try
        {
            foreach (var spawner in world.SpawnManager.GetAllSpawners().Values.SelectMany(group => group))
            {
                if (spawner?.Position == null || spawner.UnitId == 0)
                    continue;

                var position = spawner.Position.AsPositionVector();
                if (!IsFinite(position))
                    continue;

                if (!mutableSpawns.TryGetValue(spawner.UnitId, out var entries))
                {
                    entries = [];
                    mutableSpawns.Add(spawner.UnitId, entries);
                }

                entries.Add(new IndexedNpcSpawn(spawner.UnitId, position));
            }
        }
        catch
        {
            return WorldIndex.Empty;
        }

        var npcSpawns = mutableSpawns.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(entry => entry.Position.X)
                .ThenBy(entry => entry.Position.Y)
                .ThenBy(entry => entry.Position.Z)
                .ToArray());
        return new WorldIndex(
            npcSpawns,
            new Lazy<IndexedQuestStart[]>(
                () => BuildQuestStarts(npcSpawns),
                LazyThreadSafetyMode.ExecutionAndPublication));
#endif
    }

    private static IndexedQuestStart[] BuildQuestStarts(
        IReadOnlyDictionary<uint, IndexedNpcSpawn[]> npcSpawns)
    {
        var questStarts = new List<IndexedQuestStart>();
        foreach (var (npcTemplateId, spawns) in npcSpawns)
        {
            IReadOnlyList<QuestTemplate> quests;
            try
            {
                quests = QuestManager.Instance.GetPlayerBotNpcQuestStarts(npcTemplateId) ?? [];
            }
            catch
            {
                continue;
            }

            foreach (var quest in quests.Where(quest => quest != null).OrderBy(quest => quest.Id))
            foreach (var spawn in spawns)
                questStarts.Add(new IndexedQuestStart(npcTemplateId, quest, spawn.Position));
        }

        return questStarts.ToArray();
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    internal readonly record struct IndexedNpcSpawn(uint NpcTemplateId, Vector3 Position);

    internal readonly record struct IndexedQuestStart(
        uint NpcTemplateId,
        QuestTemplate Quest,
        Vector3 Position);

    private sealed record WorldIndex(
        IReadOnlyDictionary<uint, IndexedNpcSpawn[]> NpcSpawns,
        Lazy<IndexedQuestStart[]> QuestStarts)
    {
        public static WorldIndex Empty { get; } =
            new(
                new Dictionary<uint, IndexedNpcSpawn[]>(),
                new Lazy<IndexedQuestStart[]>(() => []));
    }
}
