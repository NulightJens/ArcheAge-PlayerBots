using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Compatibility;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Bots;

namespace AAEmu.Game.Bots.Questing;

public enum BotQuestGiverKind
{
    Npc,
    Doodad
}

public enum BotQuestReportKind
{
    Npc,
    Doodad,
    Journal
}

internal enum BotQuestObjectiveShape
{
    MonsterHunt,
    ItemGather,
    Unsupported,
    Ambiguous,
    Invalid
}

internal readonly record struct BotQuestStartCandidate(
    BotQuestGiverKind Kind,
    BaseUnit Giver,
    QuestTemplate Quest,
    float Distance);

internal readonly record struct BotQuestStaticStartDestination(
    uint NpcTemplateId,
    QuestTemplate Quest,
    Vector3 Position,
    float Distance);

internal readonly record struct BotQuestMonsterHuntObjective(
    uint TargetNpcTemplateId,
    uint ComponentId,
    byte ObjectiveIndex,
    int Current,
    int Required);

internal readonly record struct BotQuestItemGatherObjective(
    uint ItemId,
    uint ComponentId,
    byte ObjectiveIndex,
    int Current,
    int Required,
    bool Cleanup);

internal readonly record struct BotQuestLootAttempt(
    bool Looted,
    string Reason,
    int MatchingItems,
    int RemainingCorpseItems);

internal readonly record struct BotQuestReportEndpoint(
    BotQuestReportKind Kind,
    uint TemplateId);

internal readonly record struct BotQuestWorldObject(
    BotQuestReportKind Kind,
    BaseUnit Object,
    float Distance);

internal readonly record struct BotQuestStaticReportDestination(
    BotQuestReportKind Kind,
    uint TemplateId,
    Vector3 Position,
    float Distance);

internal readonly record struct BotQuestStaticObjectiveDestination(
    uint NpcTemplateId,
    Vector3 Position,
    float Radius,
    float Distance,
    bool MapMarked);

internal sealed record BotQuestSnapshot(
    uint QuestId,
    bool MainStory,
    bool Ready,
    BotQuestObjectiveShape ObjectiveShape,
    BotQuestMonsterHuntObjective? MonsterHunt,
    BotQuestItemGatherObjective? ItemGather,
    BotQuestReportEndpoint[] ReportEndpoints,
    int[] RewardIndices,
    string Reason);

internal interface IBotQuestAuthority
{
    IReadOnlyList<BotQuestStartCandidate> FindDoodadQuestStarts(
        BotRuntime runtime,
        float radius,
        DateTimeOffset now);

    IReadOnlyList<BotQuestStaticStartDestination> FindStaticNpcQuestStarts(
        BotRuntime runtime,
        float maximumDistance) => [];

    bool AcceptQuest(Character bot, BotQuestGiverKind kind, uint questId, uint giverObjectId);

    IReadOnlyList<BotQuestSnapshot> ReadActiveQuests(Character bot);

    IReadOnlyList<Npc> FindMonsterTargets(
        BotRuntime runtime,
        uint npcTemplateId,
        float radius,
        DateTimeOffset now);

    IReadOnlyList<Npc> FindItemGatherTargets(
        BotRuntime runtime,
        uint questId,
        uint itemId,
        float radius,
        DateTimeOffset now);

    IReadOnlyList<BotQuestStaticObjectiveDestination> FindStaticMonsterDestinations(
        BotRuntime runtime,
        BotQuestMonsterHuntObjective objective,
        float maximumDistance) => [];

    IReadOnlyList<BotQuestStaticObjectiveDestination> FindStaticItemGatherDestinations(
        BotRuntime runtime,
        uint questId,
        BotQuestItemGatherObjective objective,
        float maximumDistance) => [];

    BotQuestLootAttempt TryLootGatherItem(
        Character bot,
        uint questId,
        uint itemId,
        Npc corpse,
        float interactionRadius);

    IReadOnlyList<BotQuestWorldObject> FindReportObjects(
        BotRuntime runtime,
        BotQuestReportEndpoint endpoint,
        float radius,
        DateTimeOffset now);

    IReadOnlyList<BotQuestStaticReportDestination> FindStaticReportDestinations(
        BotRuntime runtime,
        BotQuestReportEndpoint endpoint,
        float maximumDistance);

    bool ReportQuest(
        Character bot,
        uint questId,
        BotQuestReportKind kind,
        uint worldObjectId,
        int rewardIndex);
}

/// <summary>Reads and advances quests only through AAEmu's guarded APIs.</summary>
internal sealed class BotQuestAuthority : IBotQuestAuthority
{
    private static BotQuestDestinationIndex Destinations { get; } = BotQuestDestinationIndex.Instance;
    private static object GatherSourceSync { get; } = new();
    private static Dictionary<(uint QuestId, uint ItemId), HashSet<uint>> GatherSourceCache { get; } = [];

    public IReadOnlyList<BotQuestStartCandidate> FindDoodadQuestStarts(
        BotRuntime runtime,
        float radius,
        DateTimeOffset now)
    {
#if PLAYERBOTS_AAEMU_3_0
        return [];
#else
        var bot = runtime?.Bot;
        var world = bot?.ParentWorld;
        var position = bot?.Transform?.World?.Position;
        if (world == null || !position.HasValue || radius <= 0 || !IsFinite(position.Value))
            return [];

        var candidates = new List<BotQuestStartCandidate>();
        foreach (var doodad in world.GetPlayerBotDoodadsNear(bot, radius))
        {
            if (doodad == null || !doodad.TryGetPlayerBotCurrentQuest(out var questId))
                continue;

            var quest = QuestManager.Instance.GetTemplate(questId);
            if (quest == null || !HasExactDoodadStarter(quest, doodad.TemplateId) ||
                !TryMeasure(bot, doodad, radius, out var distance))
            {
                continue;
            }

            candidates.Add(new BotQuestStartCandidate(
                BotQuestGiverKind.Doodad,
                doodad,
                quest,
                distance));
        }

        return candidates;
#endif
    }

    public bool AcceptQuest(Character bot, BotQuestGiverKind kind, uint questId, uint giverObjectId) =>
        kind switch
        {
            BotQuestGiverKind.Npc => bot.Quests.AddQuestFromNpc(questId, giverObjectId),
            BotQuestGiverKind.Doodad => bot.Quests.AddQuestFromDoodad(questId, giverObjectId),
            _ => false
        };

    public IReadOnlyList<BotQuestSnapshot> ReadActiveQuests(Character bot)
    {
        if (bot?.Quests?.ActiveQuests == null)
            return [];

        var snapshots = new List<BotQuestSnapshot>();
        foreach (var (questId, quest) in bot.Quests.ActiveQuests.OrderBy(pair => pair.Key))
        {
            if (quest == null || quest.Template is not QuestTemplate template || questId == 0)
                continue;

            var objective = InterpretObjective(quest);
            var endpoints = ReadReportEndpoints(quest);
            var rewards = ReadRewardIndices(quest);
            snapshots.Add(new BotQuestSnapshot(
                questId,
                BotQuestIntakeController.IsMainStory(template),
                quest.Status == QuestStatus.Ready || quest.Step == QuestComponentKind.Ready,
                objective.Shape,
                objective.Objective,
                objective.ItemGather,
                endpoints,
                rewards,
                objective.Reason));
        }

        return snapshots;
    }

    public IReadOnlyList<Npc> FindMonsterTargets(
        BotRuntime runtime,
        uint npcTemplateId,
        float radius,
        DateTimeOffset now)
    {
        if (runtime?.Bot == null || npcTemplateId == 0 || radius <= 0)
            return [];

        List<uint> nearbyIds;
        try
        {
            if (!runtime.Blackboard.TryGet(
                    BotValues.NearbyHostileNpcIds,
                    now.UtcDateTime,
                    out nearbyIds) || nearbyIds == null)
            {
                return [];
            }
        }
        catch
        {
            return [];
        }

        var bot = runtime.Bot;
        var candidates = new List<(Npc Npc, float Distance)>();
        foreach (var objectId in nearbyIds.Distinct())
        {
            var npc = bot.ParentWorld?.GetNpc(objectId);
            if (npc == null || npc.TemplateId != npcTemplateId || npc.IsDead || npc.Hp <= 0 ||
                !bot.CanAttack(npc) || !TryMeasure(bot, npc, radius, out var distance))
            {
                continue;
            }

            candidates.Add((npc, distance));
        }

        return candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Npc.ObjId)
            .Select(candidate => candidate.Npc)
            .ToArray();
    }

    public IReadOnlyList<BotQuestStaticStartDestination> FindStaticNpcQuestStarts(
        BotRuntime runtime,
        float maximumDistance)
    {
        var bot = runtime?.Bot;
        var world = bot?.ParentWorld;
        var botPosition = bot?.Transform?.World?.Position;
        if (world == null || !botPosition.HasValue || maximumDistance <= 0f ||
            !float.IsFinite(maximumDistance) || !IsFinite(botPosition.Value))
        {
            return [];
        }

        var destinations = new List<BotQuestStaticStartDestination>();
        foreach (var indexed in Destinations.GetNpcQuestStarts(world))
        {
            var destination = indexed.Position;
            if (!IsFinite(destination))
                continue;

            var distance = Vector3.Distance(botPosition.Value, destination);
            if (!float.IsFinite(distance) || distance > maximumDistance)
                continue;

            try
            {
                var surfaceZ = world.GetHeight(destination.X, destination.Y);
                if (float.IsFinite(surfaceZ) && surfaceZ > 0f)
                    destination.Z = surfaceZ;
            }
            catch
            {
                // Keep the finite spawn height; movement validates the route boundary.
            }

            distance = Vector3.Distance(botPosition.Value, destination);
            if (!float.IsFinite(distance) || distance > maximumDistance)
                continue;

            if (indexed.Quest != null)
                destinations.Add(new BotQuestStaticStartDestination(
                    indexed.NpcTemplateId,
                    indexed.Quest,
                    destination,
                    distance));
        }

        return destinations
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.NpcTemplateId)
            .ThenBy(candidate => candidate.Quest.Id)
            .ToArray();
    }

    public IReadOnlyList<BotQuestStaticObjectiveDestination> FindStaticMonsterDestinations(
        BotRuntime runtime,
        BotQuestMonsterHuntObjective objective,
        float maximumDistance) =>
        FindStaticObjectiveDestinations(
            runtime,
            objective.ComponentId,
            objective.TargetNpcTemplateId == 0
                ? new HashSet<uint>()
                : new HashSet<uint> { objective.TargetNpcTemplateId },
            maximumDistance);

    public IReadOnlyList<BotQuestStaticObjectiveDestination> FindStaticItemGatherDestinations(
        BotRuntime runtime,
        uint questId,
        BotQuestItemGatherObjective objective,
        float maximumDistance)
    {
        IReadOnlySet<uint> sourceTemplates;
        try
        {
            sourceTemplates = ResolveGatherSourceTemplates(questId, objective.ItemId);
        }
        catch
        {
            return [];
        }

        return FindStaticObjectiveDestinations(
            runtime,
            objective.ComponentId,
            sourceTemplates,
            maximumDistance);
    }

    public IReadOnlyList<Npc> FindItemGatherTargets(
        BotRuntime runtime,
        uint questId,
        uint itemId,
        float radius,
        DateTimeOffset now)
    {
        if (runtime?.Bot == null || questId == 0 || itemId == 0 || radius <= 0)
            return [];

        HashSet<uint> sourceTemplates;
        try
        {
            sourceTemplates = ResolveGatherSourceTemplates(questId, itemId);
        }
        catch
        {
            return [];
        }
        if (sourceTemplates.Count == 0)
            return [];

        List<uint> nearbyIds;
        try
        {
            if (!runtime.Blackboard.TryGet(
                    BotValues.NearbyHostileNpcIds,
                    now.UtcDateTime,
                    out nearbyIds) || nearbyIds == null)
            {
                return [];
            }
        }
        catch
        {
            return [];
        }

        var bot = runtime.Bot;
        var candidates = new List<(Npc Npc, float Distance)>();
        foreach (var objectId in nearbyIds.Distinct())
        {
            var npc = bot.ParentWorld?.GetNpc(objectId);
            if (npc == null || !sourceTemplates.Contains(npc.TemplateId) || npc.IsDead || npc.Hp <= 0 ||
                !bot.CanAttack(npc) || !TryMeasure(bot, npc, radius, out var distance))
            {
                continue;
            }

            candidates.Add((npc, distance));
        }

        return candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Npc.ObjId)
            .Select(candidate => candidate.Npc)
            .ToArray();
    }

    public BotQuestLootAttempt TryLootGatherItem(
        Character bot,
        uint questId,
        uint itemId,
        Npc corpse,
        float interactionRadius)
    {
        if (bot == null || questId == 0 || itemId == 0 || corpse == null ||
            interactionRadius <= 0 || !float.IsFinite(interactionRadius))
        {
            return new BotQuestLootAttempt(false, "invalid_loot_request", 0, 0);
        }
        if (!corpse.IsDead && corpse.Hp > 0)
            return new BotQuestLootAttempt(false, "target_not_dead", 0, 0);
        if (!IsSameWorld(bot, corpse))
            return new BotQuestLootAttempt(false, "corpse_world_mismatch", 0, 0);
        if (!TryMeasure(bot, corpse, interactionRadius, out _))
            return new BotQuestLootAttempt(false, "corpse_out_of_range", 0, 0);
        if (corpse.CharacterTagging == null || corpse.CharacterTagging.TagTeam != 0 ||
            !ReferenceEquals(corpse.CharacterTagging.Tagger, bot))
        {
            return new BotQuestLootAttempt(false, "corpse_not_solo_owned", 0, 0);
        }

        var itemTemplate = ItemManager.Instance.GetTemplate(itemId);
        if (itemTemplate?.LootQuestId != questId)
            return new BotQuestLootAttempt(false, "item_quest_mismatch", 0, 0);

        var corpseLoot = PlayerBotsQuestLootAdapter.GetCorpseLoot(corpse);
        var matching = corpseLoot
            .Where(item => item?.Template != null &&
                           item.TemplateId == itemId &&
                           item.Template.LootQuestId == questId)
            .ToArray();
        if (matching.Length != 1)
            return new BotQuestLootAttempt(false, "quest_loot_entry_count", matching.Length, corpseLoot.Count);

        var looted = PlayerBotsQuestLootAdapter.TryTakeCorpseLoot(
            bot,
            corpse,
            matching[0],
            out var remainingCorpseItems);
        return new BotQuestLootAttempt(
            looted,
            looted ? "native_loot_taken" : "native_loot_rejected",
            matching.Length,
            remainingCorpseItems);
    }

    public IReadOnlyList<BotQuestWorldObject> FindReportObjects(
        BotRuntime runtime,
        BotQuestReportEndpoint endpoint,
        float radius,
        DateTimeOffset now)
    {
        var bot = runtime?.Bot;
        if (bot?.ParentWorld == null || endpoint.TemplateId == 0 || radius <= 0)
            return [];

        var candidates = new List<BotQuestWorldObject>();
        if (endpoint.Kind == BotQuestReportKind.Npc)
        {
            List<uint> nearbyIds;
            try
            {
                if (!runtime.Blackboard.TryGet(
                        BotValues.NearbyNpcIds,
                        now.UtcDateTime,
                        out nearbyIds) || nearbyIds == null)
                {
                    return [];
                }
            }
            catch
            {
                return [];
            }

            foreach (var objectId in nearbyIds.Distinct())
            {
                var npc = bot.ParentWorld.GetNpc(objectId);
                if (npc == null || npc.TemplateId != endpoint.TemplateId || npc.IsDead || npc.Hp <= 0 ||
                    !TryMeasure(bot, npc, radius, out var distance))
                {
                    continue;
                }

                candidates.Add(new BotQuestWorldObject(endpoint.Kind, npc, distance));
            }
        }
        else if (endpoint.Kind == BotQuestReportKind.Doodad)
        {
#if PLAYERBOTS_AAEMU_3_0
            return [];
#else
            var position = bot.Transform?.World?.Position;
            if (!position.HasValue || !IsFinite(position.Value))
                return [];

            foreach (var doodad in bot.ParentWorld.GetPlayerBotDoodadsNear(bot, radius))
            {
                if (doodad == null || doodad.TemplateId != endpoint.TemplateId ||
                    !TryMeasure(bot, doodad, radius, out var distance))
                {
                    continue;
                }

                candidates.Add(new BotQuestWorldObject(endpoint.Kind, doodad, distance));
            }
#endif
        }

        return candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Object.ObjId)
            .ToArray();
    }

    public IReadOnlyList<BotQuestStaticReportDestination> FindStaticReportDestinations(
        BotRuntime runtime,
        BotQuestReportEndpoint endpoint,
        float maximumDistance)
    {
        var bot = runtime?.Bot;
        var world = bot?.ParentWorld;
        var botPosition = bot?.Transform?.World?.Position;
        if (world == null || !botPosition.HasValue || endpoint.Kind != BotQuestReportKind.Npc ||
            endpoint.TemplateId == 0 || maximumDistance <= 0f || !float.IsFinite(maximumDistance))
        {
            return [];
        }

        var destinations = new List<BotQuestStaticReportDestination>();
        foreach (var indexed in Destinations.GetNpcSpawns(world, endpoint.TemplateId))
        {
            var destination = indexed.Position;
            if (!IsFinite(destination))
                continue;

            try
            {
                var surfaceZ = world.GetHeight(destination.X, destination.Y);
                if (float.IsFinite(surfaceZ) && surfaceZ > 0f)
                    destination.Z = surfaceZ;
            }
            catch
            {
                // Keep the finite spawn height; the movement boundary still validates it.
            }

            var distance = Vector3.Distance(botPosition.Value, destination);
            if (!float.IsFinite(distance) || distance > maximumDistance)
                continue;

            destinations.Add(new BotQuestStaticReportDestination(
                endpoint.Kind,
                endpoint.TemplateId,
                destination,
                distance));
        }

        return destinations
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Position.X)
            .ThenBy(candidate => candidate.Position.Y)
            .ToArray();
    }

    public bool ReportQuest(
        Character bot,
        uint questId,
        BotQuestReportKind kind,
        uint worldObjectId,
        int rewardIndex)
    {
#if PLAYERBOTS_AAEMU_3_0
        return false;
#else
        var npcObjectId = kind == BotQuestReportKind.Npc ? worldObjectId : 0;
        var doodadObjectId = kind == BotQuestReportKind.Doodad ? worldObjectId : 0;
        return QuestManager.Instance.TryReportPlayerBotQuest(
            bot,
            questId,
            npcObjectId,
            doodadObjectId,
            rewardIndex);
#endif
    }

    internal static (
        BotQuestObjectiveShape Shape,
        BotQuestMonsterHuntObjective? Objective,
        BotQuestItemGatherObjective? ItemGather,
        string Reason)
        InterpretObjective(Quest quest)
    {
        if (quest == null || !quest.QuestSteps.TryGetValue(QuestComponentKind.Progress, out var progress))
            return (BotQuestObjectiveShape.Unsupported, null, null, "no_progress_step");

        var objectiveActs = progress.Components.Values
            .Where(component => component.IsCurrentlyActive)
            .SelectMany(component => component.Acts)
            .Where(act => act?.Template?.CountsAsAnObjective == true &&
                          act.Template.ThisComponentObjectiveIndex != byte.MaxValue)
            .ToArray();
        if (objectiveActs.Length == 0)
            return (BotQuestObjectiveShape.Unsupported, null, null, "no_active_objective");
        if (objectiveActs.Length != 1)
            return (BotQuestObjectiveShape.Ambiguous, null, null, "multiple_active_objectives");

        var act = objectiveActs[0];
        if (act.Template is QuestActObjMonsterHunt monster)
        {
            if (monster.NpcId == 0 || monster.Count <= 0 ||
                !TryGetObjectiveCount(quest, monster.ThisComponentObjectiveIndex, out var current))
            {
                return (BotQuestObjectiveShape.Invalid, null, null, "invalid_monster_hunt_objective");
            }

            return (
                BotQuestObjectiveShape.MonsterHunt,
                new BotQuestMonsterHuntObjective(
                    monster.NpcId,
                    act.QuestComponent.Template.Id,
                    monster.ThisComponentObjectiveIndex,
                    current,
                    monster.Count),
                null,
                "monster_hunt");
        }

        if (act.Template is QuestActObjItemGather gather)
        {
            if (gather.ItemId == 0 || gather.Count <= 0 ||
                !TryGetObjectiveCount(quest, gather.ThisComponentObjectiveIndex, out var current))
            {
                return (BotQuestObjectiveShape.Invalid, null, null, "invalid_item_gather_objective");
            }

            return (
                BotQuestObjectiveShape.ItemGather,
                null,
                new BotQuestItemGatherObjective(
                    gather.ItemId,
                    act.QuestComponent.Template.Id,
                    gather.ThisComponentObjectiveIndex,
                    current,
                    gather.Count,
                    gather.Cleanup),
                "item_gather");
        }

        return (BotQuestObjectiveShape.Unsupported, null, null,
            $"unsupported_{act.Template.GetType().Name}");
    }

    internal static int SelectRewardIndex(IReadOnlyList<int> rewardIndices) =>
        rewardIndices == null || rewardIndices.Count == 0
            ? 0
            : rewardIndices.Where(index => index >= 0).DefaultIfEmpty(-1).Min();

    private static BotQuestReportEndpoint[] ReadReportEndpoints(Quest quest)
    {
        if (!quest.QuestSteps.TryGetValue(QuestComponentKind.Ready, out var ready))
            return [];

        return ready.Components.Values
            .Where(component => component.IsCurrentlyActive)
            .SelectMany(component => component.Acts)
            .Select(act => act.Template switch
            {
                QuestActConReportNpc npc when npc.NpcId != 0 =>
                    new BotQuestReportEndpoint(BotQuestReportKind.Npc, npc.NpcId),
                QuestActConReportDoodad doodad when doodad.DoodadId != 0 =>
                    new BotQuestReportEndpoint(BotQuestReportKind.Doodad, doodad.DoodadId),
                QuestActConReportJournal =>
                    new BotQuestReportEndpoint(BotQuestReportKind.Journal, 0),
                _ => (BotQuestReportEndpoint?)null
            })
            .Where(endpoint => endpoint.HasValue)
            .Select(endpoint => endpoint.Value)
            .Distinct()
            .OrderBy(endpoint => endpoint.Kind)
            .ThenBy(endpoint => endpoint.TemplateId)
            .ToArray();
    }

    private static int[] ReadRewardIndices(Quest quest)
    {
        if (!quest.QuestSteps.TryGetValue(QuestComponentKind.Reward, out var reward))
            return [];

        return reward.Components.Values
            .Where(component => component.IsCurrentlyActive)
            .SelectMany(component => component.Acts)
            .Select(act => act.Template)
            .OfType<QuestActSupplySelectiveItem>()
            .Select(act => act.ThisSelectiveIndex)
            .Where(index => index >= 0)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();
    }

    private static bool HasExactDoodadStarter(QuestTemplate quest, uint doodadTemplateId) =>
        quest.GetComponents(QuestComponentKind.Start)
            .SelectMany(component => component.ActTemplates)
            .OfType<QuestActConAcceptDoodad>()
            .Any(act => act.DoodadId == doodadTemplateId);

    private static IReadOnlyList<BotQuestStaticObjectiveDestination> FindStaticObjectiveDestinations(
        BotRuntime runtime,
        uint componentId,
        IReadOnlySet<uint> targetTemplates,
        float maximumDistance)
    {
        var bot = runtime?.Bot;
        var world = bot?.ParentWorld;
        var botPosition = bot?.Transform?.World?.Position;
        if (world == null || !botPosition.HasValue || maximumDistance <= 0f ||
            !float.IsFinite(maximumDistance) || !IsFinite(botPosition.Value))
        {
            return [];
        }

        var worldName = GetWorldName(bot);
        var spheres = Destinations.GetQuestSpheres(world, componentId)
            .Where(sphere => sphere != null && sphere.ComponentId == componentId &&
                             IsFinite(sphere.Xyz) && float.IsFinite(sphere.Radius) && sphere.Radius > 0f &&
                             (string.IsNullOrWhiteSpace(sphere.WorldId) ||
                              string.Equals(sphere.WorldId, worldName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var candidates = new List<BotQuestStaticObjectiveDestination>();

        foreach (var targetTemplate in targetTemplates.Where(templateId => templateId != 0))
        foreach (var spawn in Destinations.GetNpcSpawns(world, targetTemplate))
        {
            if (!IsFinite(spawn.Position))
                continue;

            var distance = Vector3.Distance(botPosition.Value, spawn.Position);
            if (!float.IsFinite(distance) || distance > maximumDistance)
                continue;

            var containingSphere = spheres
                .Where(sphere => sphere.Contains(spawn.Position))
                .OrderBy(sphere => sphere.Radius)
                .FirstOrDefault();
            candidates.Add(new BotQuestStaticObjectiveDestination(
                targetTemplate,
                spawn.Position,
                containingSphere?.Radius ?? 0f,
                distance,
                containingSphere != null));
        }

        // A marker can guide discovery when no exact static spawn is known.
        foreach (var sphere in spheres)
        {
            var distance = Vector3.Distance(botPosition.Value, sphere.Xyz);
            if (!float.IsFinite(distance) || distance > maximumDistance)
                continue;
            candidates.Add(new BotQuestStaticObjectiveDestination(
                0,
                sphere.Xyz,
                sphere.Radius,
                distance,
                true));
        }

        return candidates
            .GroupBy(candidate => (
                candidate.NpcTemplateId,
                X: MathF.Round(candidate.Position.X, 2),
                Y: MathF.Round(candidate.Position.Y, 2),
                Z: MathF.Round(candidate.Position.Z, 2)))
            .Select(group => group.First())
            .OrderBy(candidate => candidate.MapMarked && candidate.NpcTemplateId != 0 ? 0 :
                candidate.MapMarked ? 1 : 2)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.NpcTemplateId)
            .ToArray();
    }

    private static HashSet<uint> ResolveGatherSourceTemplates(uint questId, uint itemId)
    {
#if PLAYERBOTS_AAEMU_3_0
        return [];
#else
        var key = (questId, itemId);
        lock (GatherSourceSync)
        {
            if (GatherSourceCache.TryGetValue(key, out var cached))
                return cached;

            var sources = new HashSet<uint>();
            var itemTemplate = ItemManager.Instance.GetTemplate(itemId);
            if (itemTemplate?.LootQuestId == questId)
            {
                foreach (var npcTemplateId in NpcManager.Instance.GetAllTemplates().Keys)
                {
                    var drops = ItemManager.Instance.GetLootPackIdByNpcId(npcTemplateId);
                    if (drops.Any(drop =>
                            LootGameData.Instance.GetPack(drop.LootPackId)?.Loots?
                                .Any(loot => loot.ItemId == itemId) == true))
                    {
                        sources.Add(npcTemplateId);
                    }
                }
            }

            GatherSourceCache[key] = sources;
            return sources;
        }
#endif
    }

    private static bool TryGetObjectiveCount(Quest quest, byte objectiveIndex, out int current)
    {
#if PLAYERBOTS_AAEMU_3_0
        current = 0;
        return false;
#else
        return quest.TryGetPlayerBotObjectiveCount(objectiveIndex, out current);
#endif
    }

    private static string GetWorldName(Character bot)
    {
#if PLAYERBOTS_AAEMU_3_0
        return bot?.ParentWorld?.Name;
#else
        return bot?.ParentWorld?.Template?.Name;
#endif
    }

    private static bool IsSameWorld(Character bot, BaseUnit target) =>
        bot?.ParentWorld != null && target?.ParentWorld != null &&
        ReferenceEquals(bot.ParentWorld, target.ParentWorld) &&
        bot.Transform?.World != null && target.Transform?.World != null;

    private static bool TryMeasure(Character bot, BaseUnit target, float radius, out float distance)
    {
        distance = float.MaxValue;
        if (bot?.ParentWorld == null || target == null || target.TemplateId == 0 ||
            !ReferenceEquals(bot.ParentWorld, target.ParentWorld) ||
            bot.Transform?.World == null || target.Transform?.World == null)
        {
            return false;
        }

        var botPosition = bot.Transform.World.Position;
        var targetPosition = target.Transform.World.Position;
        if (!IsFinite(botPosition) || !IsFinite(targetPosition))
            return false;

        float surfaceZ;
        try
        {
            surfaceZ = bot.ParentWorld.GetHeight(targetPosition.X, targetPosition.Y);
        }
        catch
        {
            return false;
        }

        if (!float.IsFinite(surfaceZ))
            return false;

        distance = Vector3.Distance(botPosition, targetPosition);
        return BotCombatTask.IsWithinNavigableQuestTargetVolume(
            botPosition,
            targetPosition,
            surfaceZ,
            radius);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
