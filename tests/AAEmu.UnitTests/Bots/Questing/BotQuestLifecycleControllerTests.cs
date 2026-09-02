using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Questing;

public sealed class BotQuestLifecycleControllerTests
{
    [Test]
    public async Task DisabledControllerDoesNotClaimOrChangeCombat()
    {
        var fixture = MakeFixture();
        fixture.Authority.Snapshots = [MonsterSnapshot(100, 0, 2, 700)];

        var claimed = fixture.Controller.Step(
            fixture.Runtime,
            new BotConfig { QuestCompletionEnabled = false },
            fixture.Time.GetUtcNow());

        await Assert.That(claimed).IsFalse();
        await Assert.That(fixture.Controller.Inspect().State).IsEqualTo(BotQuestLifecycleState.Disabled);
        await Assert.That(fixture.BeginCombatCalls).IsEqualTo(0);
    }

    [Test]
    public async Task SelectsOnlyLivingExactObjectiveTargetAndHandsOffToNormalCombat()
    {
        var fixture = MakeFixture();
        var wrong = fixture.AddNpc(800, 3);
        var dead = fixture.AddNpc(700, 2);
        dead.Hp = 0;
        var exact = fixture.AddNpc(700, 7);
        fixture.Authority.Snapshots = [MonsterSnapshot(100, 0, 2, 700)];
        fixture.Authority.Targets = [wrong, dead, exact];

        var claimed = fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());
        var view = fixture.Controller.Inspect();

        await Assert.That(claimed).IsTrue();
        await Assert.That(fixture.BeginCombatCalls).IsEqualTo(1);
        await Assert.That(fixture.Combat.Target).IsSameReferenceAs(exact);
        await Assert.That(fixture.Combat.TargetTypeFilter).IsEqualTo(700u);
        await Assert.That(fixture.Combat.CurrentState).IsEqualTo(BotCombatStateType.Combat);
        await Assert.That(view.ObjectiveTargetObjectId).IsEqualTo(exact.ObjId);
        await Assert.That(view.ObjectiveCurrent).IsEqualTo(0);
    }

    [Test]
    public async Task DeadTargetWithoutAuthoritativeCreditWaitsThenReselectsWithoutChangingProgress()
    {
        var fixture = MakeFixture();
        var first = fixture.AddNpc(700, 3);
        var second = fixture.AddNpc(700, 5);
        fixture.Authority.Snapshots = [MonsterSnapshot(100, 0, 2, 700)];
        fixture.Authority.Targets = [first, second];
        var config = EnabledConfig();

        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        first.Hp = 0;
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddMilliseconds(10));
        var waiting = fixture.Controller.Inspect();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddMilliseconds(120));
        var reselected = fixture.Controller.Inspect();

        await Assert.That(waiting.State).IsEqualTo(BotQuestLifecycleState.WaitingForProgress);
        await Assert.That(reselected.ObjectiveCurrent).IsEqualTo(0);
        await Assert.That(reselected.ObjectiveTargetObjectId).IsEqualTo(second.ObjId);
        await Assert.That(fixture.BeginCombatCalls).IsEqualTo(2);
        await Assert.That(fixture.Events).Contains(message => message.Contains("ev=quest_lifecycle_no_credit"));
    }

    [Test]
    public async Task ProgressChangesOnlyWhenAuthorityReportsAChangedObjectiveCounter()
    {
        var fixture = MakeFixture();
        fixture.Authority.Snapshots = [MonsterSnapshot(100, 0, 2, 700)];
        fixture.Authority.Targets = [fixture.AddNpc(700, 3)];
        var config = EnabledConfig();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());

        fixture.Authority.Snapshots = [MonsterSnapshot(100, 1, 2, 700)];
        fixture.Authority.Targets = [];
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(view.ObjectiveCurrent).IsEqualTo(1);
        await Assert.That(view.ProgressObservedAt).IsEqualTo(fixture.Time.GetUtcNow().AddSeconds(1));
        await Assert.That(fixture.EndCombatCalls).IsGreaterThanOrEqualTo(1);
        await Assert.That(fixture.Events).Contains(message => message.Contains("ev=quest_lifecycle_progress_observed"));
    }

    [Test]
    public async Task ObjectiveCountAtRequirementWaitsForReadyInsteadOfReportingEarly()
    {
        var fixture = MakeFixture();
        fixture.Authority.Snapshots = [MonsterSnapshot(100, 2, 2, 700)];

        fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

        await Assert.That(fixture.Controller.Inspect().State).IsEqualTo(BotQuestLifecycleState.WaitingForReady);
        await Assert.That(fixture.Authority.Reports).IsEmpty();
    }

    [Test]
    public async Task ReadyQuestSupportsNpcDoodadAndJournalEndpointsWithDeterministicReward()
    {
        foreach (var kind in new[]
                 {
                     BotQuestReportKind.Npc,
                     BotQuestReportKind.Doodad,
                     BotQuestReportKind.Journal
                 })
        {
            var fixture = MakeFixture();
            var templateId = kind == BotQuestReportKind.Journal ? 0u : 900u + (uint)kind;
            fixture.Authority.Snapshots = [ReadySnapshot(100, kind, templateId, [4, 2, 7])];
            uint expectedObjectId = 0;
            if (kind != BotQuestReportKind.Journal)
            {
                AAEmu.Game.Models.Game.Units.BaseUnit endpointObject =
                    kind == BotQuestReportKind.Npc
                        ? fixture.AddNpc(templateId, 2)
                        : fixture.AddDoodad(templateId, 2);
                expectedObjectId = endpointObject.ObjId;
                fixture.Authority.ReportObjects =
                    [new BotQuestWorldObject(kind, endpointObject, 2)];
            }

            fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

            var report = fixture.Authority.Reports.Single();
            await Assert.That(report.Kind).IsEqualTo(kind);
            await Assert.That(report.ObjectId).IsEqualTo(expectedObjectId);
            await Assert.That(report.RewardIndex).IsEqualTo(2);
            await Assert.That(fixture.Controller.Inspect().State)
                .IsEqualTo(BotQuestLifecycleState.WaitingForCompletion);
        }
    }

    [Test]
    public async Task WorldReportEndpointUsesOwnedMovementAndRevalidatesBeforeDispatch()
    {
        var fixture = MakeFixture();
        var npc = fixture.AddNpc(901, 20);
        fixture.Authority.Snapshots = [ReadySnapshot(100, BotQuestReportKind.Npc, 901, [])];
        fixture.Authority.ReportObjects =
            [new BotQuestWorldObject(BotQuestReportKind.Npc, npc, 20)];
        var config = EnabledConfig();

        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        await Assert.That(fixture.Movement.Destination).IsEqualTo(npc.Transform.World.Position);
        await Assert.That(fixture.Authority.Reports).IsEmpty();

        fixture.Authority.ReportObjects =
            [new BotQuestWorldObject(BotQuestReportKind.Npc, npc, 2)];
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddSeconds(1));

        await Assert.That(fixture.StopMovementCalls).IsEqualTo(1);
        await Assert.That(fixture.Movement.Destination).IsNull();
        await Assert.That(fixture.Authority.Reports).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RejectedReportSuspendsAndDoesNotRetryBeforeBackoff()
    {
        var fixture = MakeFixture();
        fixture.Authority.ReportResult = false;
        fixture.Authority.Snapshots = [ReadySnapshot(100, BotQuestReportKind.Journal, 0, [])];
        var config = EnabledConfig();

        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(fixture.Authority.Reports).Count().IsEqualTo(1);
        await Assert.That(view.State).IsEqualTo(BotQuestLifecycleState.Suspended);
        await Assert.That(view.DecisionReason).IsEqualTo("authoritative_report_rejected");
        await Assert.That(view.RetryAt).IsEqualTo(fixture.Time.GetUtcNow().AddSeconds(30));
    }

    [Test]
    public async Task CompletionRequiresQuestRemovalThenYieldsForImmediateRescanAndCanChain()
    {
        var fixture = MakeFixture();
        fixture.Authority.Snapshots = [ReadySnapshot(100, BotQuestReportKind.Journal, 0, [])];
        var config = EnabledConfig();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());

        fixture.Authority.Snapshots = [];
        var yielded = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddMilliseconds(10));
        var completed = fixture.Controller.Inspect();

        fixture.Authority.Snapshots = [MonsterSnapshot(101, 0, 1, 701)];
        fixture.Authority.Targets = [fixture.AddNpc(701, 4)];
        var chained = fixture.Controller.Step(
            fixture.Runtime,
            config,
            fixture.Time.GetUtcNow().AddMilliseconds(20));

        await Assert.That(yielded).IsFalse();
        await Assert.That(completed.CompletedCount).IsEqualTo(1);
        await Assert.That(completed.DecisionReason).IsEqualTo("quest_completed_rescan");
        await Assert.That(chained).IsTrue();
        await Assert.That(fixture.Controller.Inspect().QuestId).IsEqualTo(101u);
        await Assert.That(fixture.Events).Contains(message => message.Contains("ev=quest_lifecycle_rescan"));
    }

    [Test]
    public async Task UnsupportedAndAmbiguousObjectivesFailClosedWithoutCombatOrReport()
    {
        foreach (var shape in new[] { BotQuestObjectiveShape.Unsupported, BotQuestObjectiveShape.Ambiguous })
        {
            var fixture = MakeFixture();
            fixture.Authority.Snapshots =
            [
                new BotQuestSnapshot(
                    100,
                    true,
                    false,
                    shape,
                    null,
                    [],
                    [],
                    shape == BotQuestObjectiveShape.Ambiguous
                        ? "multiple_active_objectives"
                        : "unsupported_fixture")
            ];

            var claimed = fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

            await Assert.That(claimed).IsTrue();
            await Assert.That(fixture.Controller.Inspect().State).IsEqualTo(BotQuestLifecycleState.Suspended);
            await Assert.That(fixture.BeginCombatCalls).IsEqualTo(0);
            await Assert.That(fixture.Authority.Reports).IsEmpty();
        }
    }

    [Test]
    public async Task DeathAndExternalMovementYieldWithoutTakingNewOwnership()
    {
        var dead = MakeFixture();
        dead.Authority.Snapshots = [MonsterSnapshot(100, 0, 1, 700)];
        dead.Runtime.Bot.Hp = 0;
        var deadClaimed = dead.Controller.Step(dead.Runtime, EnabledConfig(), dead.Time.GetUtcNow());

        var moving = MakeFixture();
        moving.Authority.Snapshots = [MonsterSnapshot(100, 0, 1, 700)];
        moving.Authority.Targets = [moving.AddNpc(700, 3)];
        moving.Movement.Destination = new Vector3(50, 50, 0);
        var movingClaimed = moving.Controller.Step(moving.Runtime, EnabledConfig(), moving.Time.GetUtcNow());

        await Assert.That(deadClaimed).IsFalse();
        await Assert.That(movingClaimed).IsFalse();
        await Assert.That(dead.BeginCombatCalls).IsEqualTo(0);
        await Assert.That(moving.BeginCombatCalls).IsEqualTo(0);
        await Assert.That(moving.Movement.Destination).IsEqualTo(new Vector3(50, 50, 0));
    }

    [Test]
    public async Task TargetAndReportDiscoveryTimeoutsSuspendBoundedly()
    {
        var objective = MakeFixture();
        objective.Authority.Snapshots = [MonsterSnapshot(100, 0, 1, 700)];
        var config = EnabledConfig();
        objective.Controller.Step(objective.Runtime, config, objective.Time.GetUtcNow());
        objective.Controller.Step(
            objective.Runtime,
            config,
            objective.Time.GetUtcNow().AddMilliseconds(1001));

        var report = MakeFixture();
        report.Authority.Snapshots = [ReadySnapshot(200, BotQuestReportKind.Npc, 900, [])];
        report.Controller.Step(report.Runtime, config, report.Time.GetUtcNow());
        report.Controller.Step(
            report.Runtime,
            config,
            report.Time.GetUtcNow().AddMilliseconds(1001));

        await Assert.That(objective.Controller.Inspect().DecisionReason)
            .IsEqualTo("target_selection_timeout");
        await Assert.That(report.Controller.Inspect().DecisionReason)
            .IsEqualTo("report_endpoint_timeout");
    }

    private static BotQuestSnapshot MonsterSnapshot(
        uint questId,
        int current,
        int required,
        uint targetTemplateId) =>
        new(
            questId,
            true,
            false,
            BotQuestObjectiveShape.MonsterHunt,
            new BotQuestMonsterHuntObjective(targetTemplateId, 0, current, required),
            [],
            [],
            "monster_hunt");

    private static BotQuestSnapshot ReadySnapshot(
        uint questId,
        BotQuestReportKind kind,
        uint templateId,
        int[] rewards) =>
        new(
            questId,
            true,
            true,
            BotQuestObjectiveShape.Unsupported,
            null,
            [new BotQuestReportEndpoint(kind, templateId)],
            rewards,
            "ready");

    private static BotConfig EnabledConfig() => new()
    {
        UseEngine = false,
        QuestCompletionEnabled = true,
        SearchRadius = 60,
        QuestObjectiveScanRadius = 60,
        QuestReportScanRadius = 60,
        QuestReportInteractionRadius = 6,
        QuestTargetSelectionTimeoutMs = 1000,
        QuestProgressObservationMs = 100,
        QuestCompletionObservationMs = 100,
        QuestCompletionRetryBackoffMs = 30000
    };

    private static Fixture MakeFixture()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        var bot = new LifecycleCharacterMock
        {
            Id = 67,
            ObjId = 1067,
            Name = "quest-lifecycle-bot",
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
        blackboard.Register(BotValues.NearbyNpcIds, new ManualValue<List<uint>>([]));
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
        var authority = new FakeAuthority();
        var fixture = new Fixture(time, world, bot, movement, combat, authority);
        var controller = new BotQuestLifecycleController(
            authority,
            fixture.BeginCombat,
            fixture.EndCombat,
            fixture.SetDestination,
            fixture.StopMovement,
            fixture.Events.Add);
        fixture.Controller = controller;
        fixture.Runtime = new BotRuntime(
            bot,
            movement,
            combat,
            broadcaster,
            mover,
            brain,
            blackboard,
            new BotConfig { UseEngine = false },
            questLifecycleController: controller);
        return fixture;
    }

    private sealed class Fixture
    {
        private uint _nextObjectId = 9000;

        public Fixture(
            FakeTimeProvider time,
            AAEmu.Game.Models.Game.World.WorldInstance world,
            Character bot,
            BotMovementState movement,
            BotCombatState combat,
            FakeAuthority authority)
        {
            Time = time;
            World = world;
            Bot = bot;
            Movement = movement;
            Combat = combat;
            Authority = authority;
        }

        public FakeTimeProvider Time { get; }
        public AAEmu.Game.Models.Game.World.WorldInstance World { get; }
        public Character Bot { get; }
        public BotMovementState Movement { get; }
        public BotCombatState Combat { get; }
        public FakeAuthority Authority { get; }
        public BotQuestLifecycleController Controller { get; set; }
        public BotRuntime Runtime { get; set; }
        public List<string> Events { get; } = [];
        public int BeginCombatCalls { get; private set; }
        public int EndCombatCalls { get; private set; }
        public int StopMovementCalls { get; private set; }

        public Npc AddNpc(uint templateId, float x)
        {
            var npc = new Npc
            {
                ObjId = _nextObjectId++,
                TemplateId = templateId,
                Hp = 100,
                MaxHp = 100
            };
            npc.Transform.Local.SetPosition(new Vector3(x, 0, 0));
            BotTestFixture.SetPrivateField(npc, "_parentWorld", World);
            World.AddObject(npc);
            return npc;
        }

        public Doodad AddDoodad(uint templateId, float x)
        {
            var doodad = new Doodad
            {
                ObjId = _nextObjectId++,
                TemplateId = templateId
            };
            doodad.Transform.Local.SetPosition(new Vector3(x, 0, 0));
            BotTestFixture.SetPrivateField(doodad, "_parentWorld", World);
            World.AddObject(doodad);
            return doodad;
        }

        public void BeginCombat(BotRuntime runtime, Npc target, uint targetTemplateId)
        {
            BeginCombatCalls++;
            runtime.CombatState.TargetTypeFilter = targetTemplateId;
            runtime.CombatState.Target = target;
            runtime.Bot.CurrentTarget = target;
            runtime.CombatState.IsActive = true;
            runtime.CombatState.TransitionTo(BotCombatStateType.Combat);
        }

        public void EndCombat(BotRuntime runtime, uint targetTemplateId, uint? targetObjectId)
        {
            EndCombatCalls++;
            if (runtime.CombatState.TargetTypeFilter != targetTemplateId)
                return;
            runtime.CombatState.TargetTypeFilter = null;
            runtime.CombatState.Target = null;
            runtime.Bot.CurrentTarget = null;
            runtime.CombatState.IsActive = false;
            runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
        }

        public void SetDestination(Character _, Vector3 destination, bool __) =>
            Movement.Destination = destination;

        public void StopMovement(Character _)
        {
            StopMovementCalls++;
            Movement.Destination = null;
        }
    }

    private sealed class FakeAuthority : IBotQuestAuthority
    {
        public IReadOnlyList<BotQuestSnapshot> Snapshots { get; set; } = [];
        public IReadOnlyList<Npc> Targets { get; set; } = [];
        public IReadOnlyList<BotQuestWorldObject> ReportObjects { get; set; } = [];
        public List<(uint QuestId, BotQuestReportKind Kind, uint ObjectId, int RewardIndex)> Reports { get; } = [];
        public bool ReportResult { get; set; } = true;

        public IReadOnlyList<BotQuestStartCandidate> FindDoodadQuestStarts(
            BotRuntime runtime,
            float radius,
            DateTimeOffset now) => [];

        public bool AcceptQuest(
            Character bot,
            BotQuestGiverKind kind,
            uint questId,
            uint giverObjectId) => false;

        public IReadOnlyList<BotQuestSnapshot> ReadActiveQuests(Character bot) => Snapshots;

        public IReadOnlyList<Npc> FindMonsterTargets(
            BotRuntime runtime,
            uint npcTemplateId,
            float radius,
            DateTimeOffset now) => Targets;

        public IReadOnlyList<BotQuestWorldObject> FindReportObjects(
            BotRuntime runtime,
            BotQuestReportEndpoint endpoint,
            float radius,
            DateTimeOffset now) => ReportObjects;

        public bool ReportQuest(
            Character bot,
            uint questId,
            BotQuestReportKind kind,
            uint worldObjectId,
            int rewardIndex)
        {
            Reports.Add((questId, kind, worldObjectId, rewardIndex));
            return ReportResult;
        }
    }

    private sealed class LifecycleCharacterMock : CharacterMock
    {
        public override int MaxHp { get; set; } = 100;
        public override int MaxMp => 100;
    }
}
