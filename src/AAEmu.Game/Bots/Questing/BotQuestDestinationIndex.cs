using System.Numerics;
using System.Runtime.CompilerServices;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Bots.Questing;

/// <summary>
/// Immutable, lazily-built world data shared by every bot. The index turns
/// client-authored quest locations and AAEmu static spawns into cheap template
/// lookups; per-bot controllers retain only their selected destination.
/// </summary>
internal sealed class BotQuestDestinationIndex
{
    private readonly ConditionalWeakTable<WorldInstance, WorldIndex> _worlds = new();

    public static BotQuestDestinationIndex Instance { get; } = new();

    internal IReadOnlyList<IndexedNpcSpawn> GetNpcSpawns(WorldInstance world, uint npcTemplateId) =>
        world != null && npcTemplateId != 0 &&
        GetWorldIndex(world).NpcSpawns.TryGetValue(npcTemplateId, out var spawns)
            ? spawns
            : [];

    internal IReadOnlyList<IndexedQuestStart> GetNpcQuestStarts(WorldInstance world) =>
        world == null ? [] : GetWorldIndex(world).QuestStarts.Value;

    internal IReadOnlyList<SphereQuest> GetQuestSpheres(WorldInstance world, uint componentId)
    {
        if (world == null || componentId == 0)
            return [];
#if PLAYERBOTS_AAEMU_3_0
        return SphereQuestManager.Instance.GetQuestSpheres(componentId) ?? [];
#else
        return world.SphereQuestManager?.GetQuestSpheres(componentId) ?? [];
#endif
    }

    private WorldIndex GetWorldIndex(WorldInstance world) =>
        _worlds.GetValue(world, BuildWorldIndex);

    private static WorldIndex BuildWorldIndex(WorldInstance world)
    {
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
