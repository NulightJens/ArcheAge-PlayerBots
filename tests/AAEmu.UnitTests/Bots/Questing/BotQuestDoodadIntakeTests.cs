using System.Numerics;
using System.Runtime.CompilerServices;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Questing;

public sealed class BotQuestDoodadIntakeTests
{
    [Test]
    public async Task EligibleDoodadUsesNormalAcceptanceSeamAndRecordsItsGiverKind()
    {
        var fixture = MakeFixture(doodadQuestId: 200, doodadMainStory: true, doodadX: 2);

        var claimed = fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

        await Assert.That(claimed).IsTrue();
        await Assert.That(fixture.AcceptAttempts)
            .IsEquivalentTo([(BotQuestGiverKind.Doodad, 200u, fixture.Doodad.ObjId)]);
        await Assert.That(fixture.Bot.Quests.HasQuest(200)).IsTrue();
        await Assert.That(fixture.Events).Contains(message =>
            message.Contains("ev=quest_intake_accepted") && message.Contains("giver=doodad"));
    }

    [Test]
    public async Task MainStoryDoodadOutranksNearerSideQuestNpcWithoutHardcodedFixtureIds()
    {
        var fixture = MakeFixture(
            doodadQuestId: 200,
            doodadMainStory: true,
            doodadX: 20,
            npcQuestId: 100,
            npcMainStory: false,
            npcX: 2);

        fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());
        var view = fixture.Controller.Inspect();

        await Assert.That(view.GiverKind).IsEqualTo(BotQuestGiverKind.Doodad);
        await Assert.That(view.DoodadObjectId).IsEqualTo(fixture.Doodad.ObjId);
        await Assert.That(view.QuestId).IsEqualTo(200u);
        await Assert.That(fixture.Movement.Destination).IsEqualTo(fixture.Doodad.Transform.World.Position);
    }

    [Test]
    public async Task DoodadQuestFunctionIsRevalidatedImmediatelyBeforeAcceptance()
    {
        var fixture = MakeFixture(doodadQuestId: 200, doodadMainStory: true, doodadX: 20);
        var config = EnabledConfig();
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        fixture.ValidateGiverQuest = false;
        fixture.Bot.Transform.Local.SetPosition(new Vector3(16, 0, 0));

        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddSeconds(1));

        await Assert.That(fixture.AcceptAttempts).IsEmpty();
        await Assert.That(fixture.Bot.Quests.HasQuest(200)).IsFalse();
        await Assert.That(fixture.Controller.Inspect().RejectedCount).IsEqualTo(1);
        await Assert.That(fixture.Events).Contains(message =>
            message.Contains("ev=quest_intake_validation_rejected"));
    }

    [Test]
    public async Task NativeRequirementRejectionBacksOffAndNeverFabricatesActiveState()
    {
        var fixture = MakeFixture(doodadQuestId: 200, doodadMainStory: true, doodadX: 2);
        fixture.AcceptResult = false;
        var config = EnabledConfig();

        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow());
        fixture.Controller.Step(fixture.Runtime, config, fixture.Time.GetUtcNow().AddSeconds(1));
        var view = fixture.Controller.Inspect();

        await Assert.That(fixture.AcceptAttempts).Count().IsEqualTo(1);
        await Assert.That(fixture.Bot.Quests.HasQuest(200)).IsFalse();
        await Assert.That(view.State).IsEqualTo(BotQuestIntakeState.Backoff);
        await Assert.That(view.RejectedCount).IsEqualTo(1);
    }

    [Test]
    public async Task DuplicateActiveDoodadQuestIsNotAcceptedAgain()
    {
        var fixture = MakeFixture(doodadQuestId: 200, doodadMainStory: true, doodadX: 2);
        AddActiveMarker(fixture.Bot, 200);

        var claimed = fixture.Controller.Step(fixture.Runtime, EnabledConfig(), fixture.Time.GetUtcNow());

        await Assert.That(claimed).IsFalse();
        await Assert.That(fixture.AcceptAttempts).IsEmpty();
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

    private static Fixture MakeFixture(
        uint doodadQuestId,
        bool doodadMainStory,
        float doodadX,
        uint? npcQuestId = null,
        bool npcMainStory = false,
        float npcX = 0)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
        var bot = new DoodadQuestCharacterMock
        {
            Id = 68,
            ObjId = 1068,
            Name = "doodad-quest-bot",
            Hp = 100,
            Mp = 100
        };
        bot.Quests = new CharacterQuests(bot);
        bot.Transform.Local.SetPosition(Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);

        var doodad = new Doodad { ObjId = 9100, TemplateId = 6100 };
        doodad.Transform.Local.SetPosition(new Vector3(doodadX, 0, 0));
        BotTestFixture.SetPrivateField(doodad, "_parentWorld", world);
        world.AddObject(doodad);
        var doodadQuest = new QuestTemplate
        {
            Id = doodadQuestId,
            ChapterIdx = doodadMainStory ? 1u : 0u,
            QuestIdx = doodadMainStory ? doodadQuestId : 0u
        };

        var nearbyNpcIds = new List<uint>();
        var npcQuests = new Dictionary<uint, IReadOnlyList<QuestTemplate>>();
        if (npcQuestId.HasValue)
        {
            var npc = new Npc
            {
                ObjId = 9200,
                TemplateId = 6200,
                Hp = 100,
                MaxHp = 100
            };
            npc.Transform.Local.SetPosition(new Vector3(npcX, 0, 0));
            BotTestFixture.SetPrivateField(npc, "_parentWorld", world);
            world.AddObject(npc);
            nearbyNpcIds.Add(npc.ObjId);
            npcQuests[npc.TemplateId] =
            [
                new QuestTemplate
                {
                    Id = npcQuestId.Value,
                    ChapterIdx = npcMainStory ? 1u : 0u,
                    QuestIdx = npcMainStory ? npcQuestId.Value : 0u
                }
            ];
        }

        var movement = new BotMovementState();
        var combat = new BotCombatState();
        var blackboard = new BotBlackboard();
        blackboard.Register(BotValues.NearbyNpcIds, new ManualValue<List<uint>>(nearbyNpcIds));
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
        var fixture = new Fixture(time, bot, doodad, movement, npcQuests, doodadQuest);
        var controller = new BotQuestIntakeController(
            templateId => npcQuests.GetValueOrDefault(templateId) ?? [],
            (_, _, _) =>
            [
                new BotQuestStartCandidate(
                    BotQuestGiverKind.Doodad,
                    doodad,
                    doodadQuest,
                    Math.Abs(doodadX))
            ],
            fixture.Accept,
            (_, _, _, _) => fixture.ValidateGiverQuest,
            (_, destination, _) => movement.Destination = destination,
            _ => movement.Destination = null,
            (_, position) => position.Z,
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
            questIntakeController: controller);
        return fixture;
    }

    private static void AddActiveMarker(Character bot, uint questId) =>
        bot.Quests.ActiveQuests[questId] =
            (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));

    private sealed class Fixture(
        FakeTimeProvider time,
        Character bot,
        Doodad doodad,
        BotMovementState movement,
        Dictionary<uint, IReadOnlyList<QuestTemplate>> npcQuests,
        QuestTemplate doodadQuest)
    {
        public FakeTimeProvider Time { get; } = time;
        public Character Bot { get; } = bot;
        public Doodad Doodad { get; } = doodad;
        public BotMovementState Movement { get; } = movement;
        public Dictionary<uint, IReadOnlyList<QuestTemplate>> NpcQuests { get; } = npcQuests;
        public QuestTemplate DoodadQuest { get; } = doodadQuest;
        public BotQuestIntakeController Controller { get; set; }
        public BotRuntime Runtime { get; set; }
        public List<(BotQuestGiverKind Kind, uint QuestId, uint ObjectId)> AcceptAttempts { get; } = [];
        public List<string> Events { get; } = [];
        public bool AcceptResult { get; set; } = true;
        public bool ValidateGiverQuest { get; set; } = true;

        public bool Accept(
            Character character,
            BotQuestGiverKind kind,
            uint questId,
            uint objectId)
        {
            AcceptAttempts.Add((kind, questId, objectId));
            if (!AcceptResult)
                return false;

            AddActiveMarker(character, questId);
            return true;
        }
    }

    private sealed class DoodadQuestCharacterMock : CharacterMock
    {
        public override int MaxHp { get; set; } = 100;
        public override int MaxMp => 100;
    }
}
