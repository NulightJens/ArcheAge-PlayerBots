#if !PLAYERBOTS_AAEMU_3_0
using System.Collections.Immutable;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Static;

namespace AAEmu.Game.Bots.Host;

public sealed record BotHostObjectiveSnapshot(string Label, int Current, int Required);
public sealed record BotHostQuestSnapshot(uint Id, string Status, ImmutableArray<BotHostObjectiveSnapshot> Objectives);

public sealed record BotHostTickSnapshot(uint Id, string Name, string Archetype, string Role, int Level,
    uint World, uint Instance, uint Zone, float X, float Y, float Z,
    int Hp, int MaxHp, int Mp, int MaxMp, uint PartyId, string Action, string Reason, string State,
    long SampledAt, ImmutableArray<BotHostQuestSnapshot> Quests)
{
    // Called while the host owns the runtime; the result retains no mutable game objects.
    public static BotHostTickSnapshot Capture(BotRuntime runtime, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var bot = runtime.Bot;
        if (runtime.Retired || !bot.IsBot || bot.Transform?.World == null) return null;
        var p = bot.Transform.World.Position;
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) return null;
        var lifecycle = runtime.QuestLifecycleController.Inspect();
        var intake = runtime.QuestIntakeController.Inspect();
        var quests = ImmutableArray.CreateBuilder<BotHostQuestSnapshot>();
        if (bot.Quests?.ActiveQuests != null)
        foreach (var (id, quest) in bot.Quests.ActiveQuests.Take(32))
        {
            if (id == 0 || quest == null) continue;
            var objectives = ImmutableArray.CreateBuilder<BotHostObjectiveSnapshot>();
            if (quest.QuestSteps.TryGetValue(QuestComponentKind.Progress, out var progress))
            foreach (var act in progress.Components.Values.Take(16).Where(c => c.IsCurrentlyActive)
                         .SelectMany(c => c.Acts.Take(16)).Take(16))
            {
                var template = act?.Template;
                if (template?.CountsAsAnObjective != true || template.ThisComponentObjectiveIndex == byte.MaxValue ||
                    !quest.TryGetPlayerBotObjectiveCount(template.ThisComponentObjectiveIndex, out var count)) continue;
                objectives.Add(new(Text(template.GetType().Name.Replace("QuestActObj", ""), 100),
                    Math.Max(0, count), Math.Max(0, template.Count)));
            }
            quests.Add(new(id, Text(quest.Status.ToString(), 64), objectives.ToImmutable()));
        }
        var archetype = BotArchetypeManager.Instance.GetState(bot);
        var state = runtime.CombatState.ForcedState != null ? "paused" : runtime.PartyQuestSuppressBrain ? "held" : "active";
        var action = runtime.CombatState.ForcedState?.ToString() ?? (bot.IsInBattle ? "Combat" :
            lifecycle.QuestId.HasValue ? lifecycle.State.ToString() : intake.State.ToString());
        var reason = state == "paused" ? "operator_forced_state" : runtime.PartyQuestSuppressBrain ?
            runtime.PartyQuestReason + ": " + runtime.PartyRegroupDetail :
            lifecycle.QuestId.HasValue ? lifecycle.DecisionReason : intake.DecisionReason;
        return new(bot.Id, Text(bot.Name, 64),
            Text(archetype?.ArchetypeName ?? archetype?.PlannedArchetype ?? runtime.AttachedRotationArchetype, 64),
            bot.Ability1.ToString() switch
            {
                "Battlerage" => "melee", "Archery" => "archer", "Sorcery" => "mage", var ability => Text(ability, 64)
            },
            bot.Level, bot.Transform.WorldId, bot.Transform.InstanceId, bot.Transform.ZoneId,
            p.X, p.Y, p.Z, Math.Max(0, bot.Hp), Math.Max(0, bot.MaxHp), Math.Max(0, bot.Mp), Math.Max(0, bot.MaxMp),
            runtime.Social.TeamId, Text(action), Text(reason, 240), state, now.ToUnixTimeMilliseconds(), quests.ToImmutable());
    }

    private static string Text(string value, int limit = 160) =>
        new((value ?? "").Where(c => !char.IsControl(c)).Take(limit).ToArray());
}
#endif
