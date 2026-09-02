using System.Numerics;
using System.Runtime.CompilerServices;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Questing;

public sealed class BotQuestIntakeControllerTests
{
    [Test]
    public async Task DisabledControllerLeavesTheExistingDecisionBoundaryUntouched()
    {
        var fixture = MakeFixture((100, false, new Vector3(2, 0, 0)));

        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            new BotConfig { QuestIntakeEnabled = false },
            fixture.Time.GetUtcNow());

        var view = fixture.Controller.Inspect();
        await Assert.That(claimed).IsFalse();
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Disabled);
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(fixture.AcceptedQuestIds).IsEmpty();
        await Assert.That(fixture.Runtime.LifeController.Inspect().Activity).IsNull();
    }

    [Test]
    public async Task DisablingMidApproachStopsOnlyControllerOwnedMovementAndYieldsTheTick()
    {
        var fixture = MakeFixture((200, true, new Vector3(20, 0, 0)));
        fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            new BotConfig { QuestIntakeEnabled = false },
            fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(claimed).IsFalse();
        await Assert.That(fixture.StopRequests).IsEqualTo(1);
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Disabled);
        await Assert.That(view.NpcObjectId).IsNull();
    }

    [Test]
    public async Task MainStoryOutranksANearerSideQuestAndMovementIsNotReissued()
    {
        var fixture = MakeFixture(
            (100, false, new Vector3(2, 0, 0)),
            (200, true, new Vector3(20, 0, 0), 4500));
        var config = EnabledConfig();

        var first = fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        var second = fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();
        await Assert.That(first).IsTrue();
        await Assert.That(second).IsTrue();
        await Assert.That(fixture.DestinationRequests).Count().IsEqualTo(1);
        await Assert.That(fixture.DestinationRequests.Single()).IsEqualTo(new Vector3(20, 0, 0));
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Moving);
        await Assert.That(view.QuestId).IsEqualTo(200u);
        await Assert.That(view.MainStory).IsTrue();
        await Assert.That(fixture.AcceptedQuestIds).IsEmpty();
    }

    [Test]
    public async Task ArrivalAcceptsMainThenSideThroughTheNormalAuthorityAndClearsTarget()
    {
        var fixture = MakeFixture(
            (100, false, new Vector3(12, 0, 0), 4400),
            (200, true, new Vector3(12, 0, 0), 4400),
            (150, false, new Vector3(12, 0, 0), 4400));
        var config = EnabledConfig();

        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        fixture.Runtime.Bot.Transform.Local.SetPosition(new Vector3(7, 0, 0));
        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(claimed).IsTrue();
        await Assert.That(fixture.AcceptedQuestIds).IsEquivalentTo(new uint[] { 200, 100, 150 });
        await Assert.That(fixture.AcceptedQuestIds[0]).IsEqualTo(200u);
        await Assert.That(fixture.AcceptedQuestIds[1]).IsEqualTo(100u);
        await Assert.That(fixture.AcceptedQuestIds[2]).IsEqualTo(150u);
        await Assert.That(fixture.Runtime.Bot.CurrentTarget).IsNull();
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(fixture.StopRequests).IsEqualTo(1);
        await Assert.That(view.AcceptedCount).IsEqualTo(3);
        await Assert.That(view.RejectedCount).IsEqualTo(0);
        await Assert.That(view.LastAcceptedAt).IsEqualTo(fixture.Time.GetUtcNow().AddSeconds(1));
    }

    [Test]
    public async Task RejectionBacksOffWithoutStarvingAnotherQuestOrFabricatingSuccess()
    {
        var fixture = MakeFixture(
            (200, true, new Vector3(2, 0, 0), 4400),
            (201, false, new Vector3(2, 0, 0), 4400));
        fixture.AcceptOverride = questId => questId == 201;
        var config = EnabledConfig();

        var claimed = fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        var acceptedView = fixture.Controller.Inspect();
        var backoffClaimed = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddSeconds(1));
        var backoffView = fixture.Controller.Inspect();

        await Assert.That(claimed).IsTrue();
        await Assert.That(fixture.AcceptAttempts).IsEquivalentTo(new uint[] { 200, 201 });
        await Assert.That(fixture.AcceptAttempts[0]).IsEqualTo(200u);
        await Assert.That(fixture.AcceptAttempts[1]).IsEqualTo(201u);
        await Assert.That(fixture.Runtime.Bot.Quests.HasQuest(200)).IsFalse();
        await Assert.That(fixture.Runtime.Bot.Quests.HasQuest(201)).IsTrue();
        await Assert.That(acceptedView.AcceptedCount).IsEqualTo(1);
        await Assert.That(acceptedView.RejectedCount).IsEqualTo(1);
        await Assert.That(backoffClaimed).IsTrue();
        await Assert.That(backoffView.State).IsEqualTo(BotQuestIntakeState.Backoff);
        await Assert.That(backoffView.RetryAt).IsEqualTo(fixture.Time.GetUtcNow().AddSeconds(30));
    }

    [Test]
    public async Task AuthorityMustReturnTrueAndExposeActiveQuestBeforeSuccessIsRecorded()
    {
        var fixture = MakeFixture((200, true, new Vector3(2, 0, 0)));
        fixture.AcceptWithoutState = true;

        fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());
        var view = fixture.Controller.Inspect();

        await Assert.That(view.AcceptedCount).IsEqualTo(0);
        await Assert.That(view.RejectedCount).IsEqualTo(1);
        await Assert.That(view.RetryAt).IsEqualTo(fixture.Time.GetUtcNow().AddSeconds(30));
        await Assert.That(fixture.Runtime.Bot.Quests.HasQuest(200)).IsFalse();
    }

    [Test]
    public async Task ActiveAndCompletedNonRepeatableQuestsAreExcludedButRepeatableIsEligible()
    {
        var fixture = MakeFixture(
            (100, false, new Vector3(2, 0, 0), 4400, false),
            (101, false, new Vector3(2, 0, 0), 4400, false),
            (102, false, new Vector3(2, 0, 0), 4400, true));
        AddActiveMarker(fixture.Runtime.Bot, 100);
        fixture.Runtime.Bot.Quests.SetCompletedQuestFlag(101, true);
        fixture.Runtime.Bot.Quests.SetCompletedQuestFlag(102, true);

        fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

        await Assert.That(fixture.AcceptAttempts).IsEquivalentTo(new uint[] { 102 });
        await Assert.That(fixture.Runtime.Bot.Quests.HasQuest(100)).IsTrue();
        await Assert.That(fixture.Runtime.Bot.Quests.HasQuest(101)).IsFalse();
        await Assert.That(fixture.Runtime.Bot.Quests.HasQuest(102)).IsTrue();
    }

    [Test]
    public async Task CandidateFilteringRejectsNonfiniteAndHeightmapIncompatibleFixtures()
    {
        var fixture = MakeFixture(
            (100, true, new Vector3(float.NaN, 0, 0)),
            (101, true, new Vector3(10, 0, 100)),
            (102, true, new Vector3(101, 0, 0)));
        fixture.HeightOverride = position => position.Z == 100 ? 1 : 0;

        var claimed = fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());
        var view = fixture.Controller.Inspect();

        await Assert.That(claimed).IsFalse();
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Idle);
        await Assert.That(view.DecisionReason).IsEqualTo("no_eligible_nearby_quest");
        await Assert.That(fixture.AcceptAttempts).IsEmpty();
        await Assert.That(fixture.DestinationRequests).IsEmpty();
    }

    [Test]
    public async Task NearbyScanFailureFailsClosedWithoutMovementOrAcceptance()
    {
        var fixture = MakeFixture((200, true, new Vector3(2, 0, 0)));
        fixture.Blackboard.Register(
            BotValues.NearbyNpcIds,
            new CalculatedValue<List<uint>>(
                () => throw new InvalidOperationException("simulated scan failure"),
                TimeSpan.Zero));

        var claimed = fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

        await Assert.That(claimed).IsFalse();
        await Assert.That(fixture.AcceptAttempts).IsEmpty();
        await Assert.That(fixture.DestinationRequests).IsEmpty();
        await Assert.That(fixture.Events).Contains(message => message.Contains("ev=quest_intake_scan_error"));
    }

    [Test]
    public async Task NonfiniteGeneralSearchRadiusFailsClosed()
    {
        var fixture = MakeFixture((200, true, new Vector3(2, 0, 0)));
        var config = EnabledConfig();
        config.SearchRadius = double.NaN;

        var claimed = fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());

        await Assert.That(claimed).IsFalse();
        await Assert.That(fixture.AcceptAttempts).IsEmpty();
        await Assert.That(fixture.Controller.Inspect().DecisionReason)
            .IsEqualTo("no_eligible_nearby_quest");
    }

    [Test]
    public async Task SelectedNpcDespawnStopsOnlyOwnedMovementAndInvalidatesPlan()
    {
        var fixture = MakeFixture((200, true, new Vector3(20, 0, 0)));
        var config = EnabledConfig();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        var npc = fixture.World.GetNpc(9000);
        npc.Hp = 0;

        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(claimed).IsTrue();
        await Assert.That(fixture.StopRequests).IsEqualTo(1);
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Blocked);
        await Assert.That(view.DecisionReason).IsEqualTo("npc_invalid");
    }

    [Test]
    public async Task SelectedNpcWorldChangeStopsOwnedMovementAndInvalidatesPlan()
    {
        var fixture = MakeFixture((200, true, new Vector3(20, 0, 0)));
        var config = EnabledConfig();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        var npc = fixture.World.GetNpc(9000);
        BotTestFixture.SetPrivateField(npc, "_parentWorld", BotTestFixture.MakeWorld(2));

        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(claimed).IsTrue();
        await Assert.That(fixture.StopRequests).IsEqualTo(1);
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Blocked);
        await Assert.That(view.DecisionReason).IsEqualTo("npc_invalid");
    }

    [Test]
    public async Task UnrelatedMovementOrCombatStateFailsClosedWithoutTakingOwnership()
    {
        var moving = MakeFixture((200, true, new Vector3(20, 0, 0)));
        moving.Movement.Destination = new Vector3(5, 5, 0);
        var movingClaimed = moving.Controller.Step(
            moving.Runtime,
            EnabledConfig(),
            moving.Time.GetUtcNow());

        var combat = MakeFixture((200, true, new Vector3(2, 0, 0)));
        combat.Runtime.CombatState.TransitionTo(BotCombatStateType.Grinding);
        var combatClaimed = combat.Controller.Step(
            combat.Runtime,
            EnabledConfig(),
            combat.Time.GetUtcNow());

        await Assert.That(movingClaimed).IsFalse();
        await Assert.That(moving.DestinationRequests).IsEmpty();
        await Assert.That(moving.Movement.Destination).IsEqualTo(new Vector3(5, 5, 0));
        await Assert.That(combatClaimed).IsFalse();
        await Assert.That(combat.AcceptAttempts).IsEmpty();
        await Assert.That(combat.Controller.Inspect().DecisionReason).IsEqualTo("combat_not_idle");
    }

    [Test]
    public async Task CombatInterruptionStopsOnlyTheControllersExistingApproach()
    {
        var fixture = MakeFixture((200, true, new Vector3(20, 0, 0)));
        var config = EnabledConfig();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        fixture.Runtime.CombatState.TransitionTo(BotCombatStateType.Grinding);

        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddSeconds(1));

        await Assert.That(claimed).IsFalse();
        await Assert.That(fixture.StopRequests).IsEqualTo(1);
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(fixture.Controller.Inspect().DecisionReason).IsEqualTo("combat_not_idle");
    }

    [Test]
    public async Task RuntimeCombatAndMovementSafetyGuardsFailClosed()
    {
        var cases = new (Action<Fixture> Mutate, string Reason)[]
        {
            (fixture => fixture.Runtime.Bot.Hp = 0, "runtime_not_world_ready"),
            (fixture => fixture.Runtime.Bot.Quests = null, "runtime_not_world_ready"),
            (fixture => fixture.Runtime.CombatState.SetForcedState(BotCombatStateType.Grinding), "combat_not_idle"),
            (fixture => fixture.Runtime.CombatState.DuelRequestPending = true, "combat_not_idle"),
            (fixture => fixture.Runtime.MovementState.FollowTarget = new CharacterMock(), "movement_mode_busy"),
            (fixture => fixture.Runtime.MovementState.IsFalling = true, "movement_mode_busy"),
            (fixture => fixture.Runtime.MovementState.JumpRequested = true, "movement_mode_busy")
        };

        foreach (var (mutate, reason) in cases)
        {
            var fixture = MakeFixture((200, true, new Vector3(2, 0, 0)));
            mutate(fixture);

            var claimed = fixture.Controller.Step(
                fixture.Runtime,
                EnabledConfig(),
                fixture.Time.GetUtcNow());

            await Assert.That(claimed).IsFalse();
            await Assert.That(fixture.Controller.Inspect().DecisionReason).IsEqualTo(reason);
            await Assert.That(fixture.AcceptAttempts).IsEmpty();
            await Assert.That(fixture.DestinationRequests).IsEmpty();
        }
    }

    [Test]
    public async Task DeterministicTieUsesNpcObjectThenQuestId()
    {
        var fixture = MakeFixture(
            (201, true, new Vector3(-10, 0, 0), 4400),
            (200, true, new Vector3(-10, 0, 0), 4400),
            (202, true, new Vector3(10, 0, 0), 4500));

        fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());
        var view = fixture.Controller.Inspect();

        await Assert.That(view.NpcObjectId).IsEqualTo(9000u);
        await Assert.That(view.QuestId).IsEqualTo(200u);
        await Assert.That(fixture.DestinationRequests.Single()).IsEqualTo(new Vector3(-10, 0, 0));
    }

    [Test]
    public async Task FreshControllerStartsWithNoPriorPlanBackoffOrCounters()
    {
        var first = MakeFixture((200, true, new Vector3(2, 0, 0)));
        first.AcceptOverride = _ => false;
        first.Controller.Step(first.Runtime, EnabledConfig(), first.Time.GetUtcNow());

        var fresh = MakeFixture((201, true, new Vector3(2, 0, 0)));
        var before = fresh.Controller.Inspect();
        fresh.Controller.Step(fresh.Runtime, EnabledConfig(), fresh.Time.GetUtcNow());
        var after = fresh.Controller.Inspect();

        await Assert.That(before.AcceptedCount).IsEqualTo(0);
        await Assert.That(before.RejectedCount).IsEqualTo(0);
        await Assert.That(before.RetryAt).IsNull();
        await Assert.That(after.AcceptedCount).IsEqualTo(1);
        await Assert.That(after.RejectedCount).IsEqualTo(0);
    }

    private static BotConfig EnabledConfig() => new()
    {
        UseEngine = false,
        QuestIntakeEnabled = true,
        SearchRadius = 60,
        QuestIntakeScanRadius = 60,
        QuestIntakeInteractionRadius = 6,
        QuestIntakeRetryBackoffMs = 30000
    };

    private static Fixture MakeFixture(params QuestNpcSpec[] specs)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        var bot = new QuestingCharacterMock
        {
            Id = 63,
            ObjId = 1063,
            Name = "questbot",
            Hp = 100,
            Mp = 100
        };
        bot.Quests = new CharacterQuests(bot);
        bot.Transform.Local.SetPosition(Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        var movement = new BotMovementState();
        var combat = new BotCombatState();
        var blackboard = new BotBlackboard();
        var npcObjectIds = new List<uint>();
        var questsByNpcTemplate = new Dictionary<uint, List<QuestTemplate>>();
        var npcByKey = new Dictionary<(uint TemplateId, Vector3 Position), Npc>();
        var nextObjectId = 9000u;
        foreach (var spec in specs)
        {
            var key = (spec.NpcTemplateId, spec.Position);
            if (!npcByKey.TryGetValue(key, out var npc))
            {
                npc = new Npc
                {
                    ObjId = nextObjectId++,
                    TemplateId = spec.NpcTemplateId,
                    Hp = 100,
                    MaxHp = 100
                };
                npc.Transform.Local.SetPosition(spec.Position);
                BotTestFixture.SetPrivateField(npc, "_parentWorld", world);
                world.AddObject(npc);
                npcByKey.Add(key, npc);
                npcObjectIds.Add(npc.ObjId);
            }

            if (!questsByNpcTemplate.TryGetValue(spec.NpcTemplateId, out var quests))
            {
                quests = [];
                questsByNpcTemplate.Add(spec.NpcTemplateId, quests);
            }
            quests.Add(new QuestTemplate
            {
                Id = spec.QuestId,
                ChapterIdx = spec.MainStory ? 1u : 0u,
                QuestIdx = spec.MainStory ? spec.QuestId : 0u,
                Repeatable = spec.Repeatable
            });
        }

        blackboard.Register(BotValues.NearbyNpcIds, new ManualValue<List<uint>>(npcObjectIds));
        blackboard.Register(BotValues.NearbyHostileNpcIds, new ManualValue<List<uint>>([]));
        var broadcaster = new BotMovementBroadcaster(bot, time);
        var mover = new BotSim.SimMover(bot, movement, broadcaster);
        var brain = new BotCombatTask(
            bot,
            combat,
            broadcaster,
            onCancel: null,
            blackboard: blackboard,
            timeProvider: time);
        var fixture = new Fixture(time, world, bot, movement, combat, blackboard, questsByNpcTemplate);
        var runtime = new BotRuntime(
            bot,
            movement,
            combat,
            broadcaster,
            mover,
            brain,
            blackboard,
            new BotConfig { UseEngine = false },
            questIntakeController: fixture.Controller);
        fixture.Runtime = runtime;
        return fixture;
    }

    private static void AddActiveMarker(Character bot, uint questId) =>
        bot.Quests.ActiveQuests[questId] =
            (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));

    private sealed class Fixture
    {
        private readonly Dictionary<uint, List<QuestTemplate>> _questsByNpcTemplate;

        public Fixture(
            FakeTimeProvider time,
            AAEmu.Game.Models.Game.World.WorldInstance world,
            Character bot,
            BotMovementState movement,
            BotCombatState combat,
            BotBlackboard blackboard,
            Dictionary<uint, List<QuestTemplate>> questsByNpcTemplate)
        {
            Time = time;
            World = world;
            Bot = bot;
            Movement = movement;
            Combat = combat;
            Blackboard = blackboard;
            _questsByNpcTemplate = questsByNpcTemplate;
            Controller = new BotQuestIntakeController(
                npcTemplateId => _questsByNpcTemplate.GetValueOrDefault(npcTemplateId) ?? [],
                Accept,
                (_, destination, _) =>
                {
                    DestinationRequests.Add(destination);
                    Movement.Destination = destination;
                },
                _ =>
                {
                    StopRequests++;
                    Movement.Destination = null;
                },
                (_, position) => HeightOverride(position),
                Events.Add);
        }

        public FakeTimeProvider Time { get; }
        public AAEmu.Game.Models.Game.World.WorldInstance World { get; }
        public Character Bot { get; }
        public BotMovementState Movement { get; }
        public BotCombatState Combat { get; }
        public BotBlackboard Blackboard { get; }
        public BotQuestIntakeController Controller { get; }
        public BotRuntime Runtime { get; set; }
        public List<Vector3> DestinationRequests { get; } = [];
        public List<uint> AcceptAttempts { get; } = [];
        public List<uint> AcceptedQuestIds { get; } = [];
        public List<string> Events { get; } = [];
        public Func<uint, bool> AcceptOverride { get; set; }
        public Func<Vector3, float> HeightOverride { get; set; } = position => position.Z;
        public bool AcceptWithoutState { get; set; }
        public int StopRequests { get; private set; }

        private bool Accept(Character bot, uint questId, uint npcObjectId)
        {
            AcceptAttempts.Add(questId);
            var accepted = AcceptOverride?.Invoke(questId) ?? true;
            bot.CurrentTarget = bot.ParentWorld.GetNpc(npcObjectId);
            if (!accepted)
                return false;
            if (!AcceptWithoutState)
            {
                AddActiveMarker(bot, questId);
                AcceptedQuestIds.Add(questId);
            }
            return true;
        }
    }

    private sealed class QuestingCharacterMock : CharacterMock
    {
        public override int MaxHp { get; set; } = 100;
        public override int MaxMp => 100;
    }

    private readonly record struct QuestNpcSpec(
        uint QuestId,
        bool MainStory,
        Vector3 Position,
        uint NpcTemplateId = 4400,
        bool Repeatable = false)
    {
        public static implicit operator QuestNpcSpec((uint QuestId, bool MainStory, Vector3 Position) value) =>
            new(value.QuestId, value.MainStory, value.Position);

        public static implicit operator QuestNpcSpec(
            (uint QuestId, bool MainStory, Vector3 Position, uint NpcTemplateId) value) =>
            new(value.QuestId, value.MainStory, value.Position, value.NpcTemplateId);

        public static implicit operator QuestNpcSpec(
            (uint QuestId, bool MainStory, Vector3 Position, uint NpcTemplateId, bool Repeatable) value) =>
            new(value.QuestId, value.MainStory, value.Position, value.NpcTemplateId, value.Repeatable);
    }
}
