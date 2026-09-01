using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Expeditions;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

[NotInParallel]
public class BotCommandsTests
{
    private BotManager _previousBotManager;
    private BotCombatManager _previousCombatManager;
    private BotArchetypeManager _previousArchetypeManager;
    private Func<IDuelManager> _previousDuelManagerResolver;
    private Func<uint, BuffTemplate> _previousBuffTemplateResolver;
    private Func<Character, uint, Npc> _previousBuffNpcResolver;
    private Func<IEnumerable<Character>, uint, Npc> _previousAttackObjectNpcResolver;
    private BotManager _botManager;
    private FakeBotCombatManager _combatManager;
    private FakeBotArchetypeManager _archetypeManager;

    [Before(Test)]
    public void Setup()
    {
        BotTestFixture.RegisterTaskManager();
        _previousBotManager = BotManager.Instance;
        _previousCombatManager = BotCombatManager.Instance;
        _previousArchetypeManager = BotArchetypeManager.Instance;
        _previousDuelManagerResolver = BotDuelCommand.DuelManagerResolver;
        _previousBuffTemplateResolver = BotBuffCommand.BuffTemplateResolver;
        _previousBuffNpcResolver = BotBuffCommand.NpcResolver;
        _previousAttackObjectNpcResolver = BotAttackObjectCommand.NpcResolver;

        _botManager = new BotManager(_ => null, onlineLookup: _ => null);
        _combatManager = new FakeBotCombatManager();
        _archetypeManager = new FakeBotArchetypeManager();

        BotTestFixture.RegisterSingletons(_botManager, _combatManager, _archetypeManager);
    }

    [After(Test)]
    public void Teardown()
    {
        BotTestFixture.RegisterSingletons(_previousBotManager, _previousCombatManager, _previousArchetypeManager);
        BotDuelCommand.DuelManagerResolver = _previousDuelManagerResolver;
        BotBuffCommand.BuffTemplateResolver = _previousBuffTemplateResolver;
        BotBuffCommand.NpcResolver = _previousBuffNpcResolver;
        BotAttackObjectCommand.NpcResolver = _previousAttackObjectNpcResolver;
    }

    [Test]
    public async Task SetBotClass_ExposesRecoveredHumanFacingAliases()
    {
        var command = new SetBotClass();

        await Assert.That(command.CommandNames).Contains("setclass");
        await Assert.That(command.CommandNames).Contains("botsetclass");
        await Assert.That(command.CommandNames).Contains("setarchetype");
        await Assert.That(SetBotClass.TreeName(AbilityType.Fight)).IsEqualTo("Battlerage");
        await Assert.That(SetBotClass.TreeName(AbilityType.Will)).IsEqualTo("Auramancy");
        await Assert.That(SetBotClass.TreeName(AbilityType.Vocation)).IsEqualTo("Shadowplay");
    }

    [Test]
    public async Task BotMetrics_SnapshotIncludesImmutableActivityDirectorEnvelope()
    {
        _ = new BotActivityDirectorTask(
            new BotConfig
            {
                ActivityDirectorEnabled = true,
                ActivityDirectorZoneId = 137,
                ActivityDirectorCharacterIds = [7, 8],
                ActivityDirectorMinimumPopulation = 1,
                ActivityDirectorTargetPopulation = 1,
                ActivityDirectorMaximumPopulation = 2
            },
            _botManager,
            TimeProvider.System);

        var output = Execute(new BotMetricsCommand(), "snapshot");

        await Assert.That(output.Messages).Contains(message => message.Contains("T021_METRICS "));
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("T081_DIRECTOR ") &&
            message.Contains("\"enabled\":true") &&
            message.Contains("\"valid\":true") &&
            message.Contains("\"zoneId\":137") &&
            message.Contains("\"minimumPopulation\":1") &&
            message.Contains("\"targetPopulation\":1") &&
            message.Contains("\"maximumPopulation\":2") &&
            message.Contains("\"eligibleIdentities\":2") &&
            message.Contains("\"attemptCount\":0") &&
            message.Contains("\"startedAt\":null") &&
            message.Contains("\"lastTickAt\":null"));
    }

    [Test]
    public async Task BotGear_ExposesTargetFirstHumanWorkflowWithoutBrokenPipeMarkup()
    {
        var command = new BotGearCommand();

        await Assert.That(command.CommandNames).Contains("botgear");
        await Assert.That(command.CommandNames).Contains("botequip");
        await Assert.That(command.GetCommandLineHelp()).Contains("show, equip, inspect");
        await Assert.That(command.GetCommandLineHelp()).Contains("create <grade> <prefix> <armor> <weapon>");
        await Assert.That(command.GetCommandLineHelp()).DoesNotContain("|");
        await Assert.That(BotGearCommand.EquipmentSlotName(new EquipItem { Slot = 15 })).IsEqualTo("Mainhand");
    }

    [Test]
    public async Task BotQuest_SeparatesDiscoveryFromExplicitLifecycleActions()
    {
        var command = new BotQuestCommand();

        await Assert.That(command.CommandNames).Contains("botquest");
        await Assert.That(command.GetCommandLineHelp()).Contains("scan <botId> [radius]");
        await Assert.That(BotQuestCommand.TryParse(["scan", "2", "40"], out var scan)).IsTrue();
        await Assert.That(scan.Verb).IsEqualTo(BotQuestVerb.Scan);
        await Assert.That(scan.BotId).IsEqualTo(2u);
        await Assert.That(scan.Radius).IsEqualTo(40f);
        await Assert.That(BotQuestCommand.TryParse(["nearby", "2", "3495", "75"], out var nearby)).IsTrue();
        await Assert.That(nearby.Verb).IsEqualTo(BotQuestVerb.Nearby);
        await Assert.That(nearby.NpcTemplateId).IsEqualTo(3495u);
        await Assert.That(nearby.Radius).IsEqualTo(75f);
        await Assert.That(BotQuestCommand.TryParse(["locate", "2", "3475"], out var locate)).IsTrue();
        await Assert.That(locate.Verb).IsEqualTo(BotQuestVerb.Locate);
        await Assert.That(locate.NpcTemplateId).IsEqualTo(3475u);
        await Assert.That(BotQuestCommand.TryParse(["locate", "2", "0"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["accept", "2", "330"], out var accept)).IsTrue();
        await Assert.That(accept.Verb).IsEqualTo(BotQuestVerb.Accept);
        await Assert.That(accept.QuestId).IsEqualTo(330u);
        await Assert.That(BotQuestCommand.TryParse(["status", "2", "330"], out var status)).IsTrue();
        await Assert.That(status.Verb).IsEqualTo(BotQuestVerb.Status);
        await Assert.That(BotQuestCommand.TryParse(["talk", "2", "5304"], out var talk)).IsTrue();
        await Assert.That(talk.Verb).IsEqualTo(BotQuestVerb.Talk);
        await Assert.That(talk.QuestId).IsEqualTo(5304u);
        await Assert.That(BotQuestCommand.TryParse(["talk", "2", "5304", "3567"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["hunt", "2", "251"], out var hunt)).IsTrue();
        await Assert.That(hunt.Verb).IsEqualTo(BotQuestVerb.Hunt);
        await Assert.That(hunt.QuestId).IsEqualTo(251u);
        await Assert.That(BotQuestCommand.TryParse(["hunt", "2", "251", "3475"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["travel", "2", "137"], out var travel)).IsTrue();
        await Assert.That(travel.Verb).IsEqualTo(BotQuestVerb.Travel);
        await Assert.That(travel.QuestId).IsEqualTo(137u);
        await Assert.That(BotQuestCommand.TryParse(["travel", "2", "137", "3653"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["use", "2", "293", "45678"], out var use)).IsTrue();
        await Assert.That(use.Verb).IsEqualTo(BotQuestVerb.Use);
        await Assert.That(use.QuestId).IsEqualTo(293u);
        await Assert.That(use.TargetObjId).IsEqualTo(45678u);
        await Assert.That(BotQuestCommand.TryParse(["use", "2", "293"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["use", "2", "293", "0"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["acquire", "2", "293", "45678"], out var acquire)).IsTrue();
        await Assert.That(acquire.Verb).IsEqualTo(BotQuestVerb.Acquire);
        await Assert.That(acquire.QuestId).IsEqualTo(293u);
        await Assert.That(acquire.TargetObjId).IsEqualTo(45678u);
        await Assert.That(BotQuestCommand.TryParse(["acquire", "2", "293"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["loot", "2", "251", "45678"], out var loot)).IsTrue();
        await Assert.That(loot.Verb).IsEqualTo(BotQuestVerb.Loot);
        await Assert.That(loot.QuestId).IsEqualTo(251u);
        await Assert.That(loot.TargetObjId).IsEqualTo(45678u);
        await Assert.That(BotQuestCommand.TryParse(["loot", "2", "251"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["report", "2", "330", "1"], out var report)).IsTrue();
        await Assert.That(report.Verb).IsEqualTo(BotQuestVerb.Report);
        await Assert.That(report.QuestId).IsEqualTo(330u);
        await Assert.That(report.SelectedReward).IsEqualTo(1);
        await Assert.That(BotQuestCommand.TryParse(["report", "2", "330", "-1"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["scan", "2", "101"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryParse(["nearby", "2", "3495", "101"], out _)).IsFalse();
        await Assert.That(BotQuestCommand.IsValidSelectedReward([], 0)).IsTrue();
        await Assert.That(BotQuestCommand.IsValidSelectedReward([], 1)).IsFalse();
        await Assert.That(BotQuestCommand.IsValidSelectedReward([1, 2], 0)).IsFalse();
        await Assert.That(BotQuestCommand.IsValidSelectedReward([1, 2], 2)).IsTrue();
        await Assert.That(BotQuestCommand.IsValidSelectedReward([1, 2], 3)).IsFalse();
        await Assert.That(BotQuestCommand.AnyObjectiveAdvanced([0, 0], [0, 1])).IsTrue();
        await Assert.That(BotQuestCommand.AnyObjectiveAdvanced([1], [1])).IsFalse();
        await Assert.That(BotQuestCommand.AnyObjectiveAdvanced([1], [2, 1])).IsFalse();

        var component = new QuestComponentTemplate(new QuestTemplate());
        var supply = new QuestActSupplyItem(component) { ItemId = 8242 };
        var sourceItem = new Item
        {
            TemplateId = 8242,
            Template = new ItemTemplate { Id = 8242, LootQuestId = 293, UseSkillId = 11684 }
        };
        await Assert.That(BotQuestCommand.IsSupportedQuestUseSource(supply, sourceItem, 293)).IsTrue();
        await Assert.That(BotQuestCommand.IsSupportedQuestUseSource(supply, sourceItem, 251)).IsFalse();
        sourceItem.Template.UseSkillId = 0;
        await Assert.That(BotQuestCommand.IsSupportedQuestUseSource(supply, sourceItem, 293)).IsFalse();

        var gather = new QuestActObjItemGather(component) { ItemId = 4058 };
        var lootItem = new Item
        {
            TemplateId = 4058,
            Template = new ItemTemplate { Id = 4058, LootQuestId = 251 }
        };
        await Assert.That(BotQuestCommand.IsSupportedQuestLootSource(gather, lootItem, 251)).IsTrue();
        await Assert.That(BotQuestCommand.IsSupportedQuestLootSource(gather, lootItem, 293)).IsFalse();
        lootItem.TemplateId = 8243;
        await Assert.That(BotQuestCommand.IsSupportedQuestLootSource(gather, lootItem, 251)).IsFalse();
    }

    [Test]
    public async Task BotQuest_HuntContractDerivesExactNpcAndOnlyRemainingNativeCount()
    {
        var component = new QuestComponentTemplate(new QuestTemplate());
        QuestActTemplate[] activeActs =
        [
            new QuestActObjMonsterHunt(component) { NpcId = 3475, Count = 3 }
        ];

        var supported = BotQuestCommand.TryGetExactHuntContract(
            activeActs, [1], out var npcTemplateId, out var remainingKills, out var error);

        await Assert.That(supported).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(npcTemplateId).IsEqualTo(3475u);
        await Assert.That(remainingKills).IsEqualTo(2);
    }

    [Test]
    public async Task BotQuest_TravelContractDerivesOneSameWorldHeightmapArrival()
    {
        var component = new QuestComponentTemplate(new QuestTemplate()) { Id = 3653 };
        QuestActTemplate[] activeActs =
        [
            new QuestActObjSphere(component) { SphereId = 191 }
        ];
        SphereQuest[] spheres =
        [
            new()
            {
                ComponentId = 3653,
                WorldId = "main_world",
                Radius = 6,
                Xyz = new Vector3(10, 0, 105)
            },
            new()
            {
                ComponentId = 3653,
                WorldId = "login_world",
                Radius = 6,
                Xyz = new Vector3(20, 0, 100)
            }
        ];

        var supported = BotQuestCommand.TryGetStaticSphereTravelContract(
            activeActs,
            [0],
            [3653],
            spheres,
            "main_world",
            new Vector3(0, 0, 100),
            (_, _) => 100,
            out var plan,
            out var error);

        await Assert.That(supported).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(plan.ComponentId).IsEqualTo(3653u);
        await Assert.That(plan.SphereId).IsEqualTo(191u);
        await Assert.That(plan.Destination).IsEqualTo(new Vector3(10, 0, 100));
        await Assert.That(plan.Distance).IsEqualTo(10f);
        await Assert.That(plan.SurfaceOffset).IsEqualTo(5f);
    }

    [Test]
    public async Task BotQuest_TravelContractFailsClosedOnUnsupportedOrUnsafeShapes()
    {
        var component = new QuestComponentTemplate(new QuestTemplate()) { Id = 3653 };
        var sphereAct = new QuestActObjSphere(component) { SphereId = 191 };
        var sphere = new SphereQuest
        {
            ComponentId = 3653,
            WorldId = "main_world",
            Radius = 10,
            Xyz = new Vector3(10, 0, 100)
        };

        await Assert.That(BotQuestCommand.TryGetStaticSphereTravelContract(
            [sphereAct, new QuestActObjTalk(component)], [0, 0], [3653, 3653], [sphere],
            "main_world", Vector3.Zero, (_, _) => 100, out _, out _)).IsFalse();

        sphereAct.NpcId = 146;
        await Assert.That(BotQuestCommand.TryGetStaticSphereTravelContract(
            [sphereAct], [0], [3653], [sphere],
            "main_world", Vector3.Zero, (_, _) => 100, out _, out _)).IsFalse();
        sphereAct.NpcId = 0;

        await Assert.That(BotQuestCommand.TryGetStaticSphereTravelContract(
            [sphereAct], [1], [3653], [sphere],
            "main_world", Vector3.Zero, (_, _) => 100, out _, out _)).IsFalse();

        await Assert.That(BotQuestCommand.TryGetStaticSphereTravelContract(
            [sphereAct], [0], [3653], [sphere, sphere],
            "main_world", Vector3.Zero, (_, _) => 100, out _, out _)).IsFalse();

        sphere.Xyz = new Vector3(10, 0, 130);
        await Assert.That(BotQuestCommand.TryGetStaticSphereTravelContract(
            [sphereAct], [0], [3653], [sphere],
            "main_world", Vector3.Zero, (_, _) => 100, out _, out _)).IsFalse();

        sphere.Xyz = new Vector3(101, 0, 100);
        await Assert.That(BotQuestCommand.TryGetStaticSphereTravelContract(
            [sphereAct], [0], [3653], [sphere],
            "main_world", Vector3.Zero, (_, _) => 100, out _, out _)).IsFalse();
    }

    [Test]
    public async Task BotQuest_HuntContractRejectsCompletedGroupedOrMixedObjectives()
    {
        var component = new QuestComponentTemplate(new QuestTemplate());
        var exactHunt = new QuestActObjMonsterHunt(component) { NpcId = 3475, Count = 3 };
        var groupedHunt = new QuestActObjMonsterGroupHunt(component) { QuestMonsterGroupId = 17, Count = 3 };
        var talk = new QuestActObjTalk(component) { NpcId = 3512, Count = 1 };

        await Assert.That(BotQuestCommand.TryGetExactHuntContract(
            [exactHunt], [3], out _, out _, out var completedError)).IsFalse();
        await Assert.That(completedError).Contains("already complete");
        await Assert.That(BotQuestCommand.TryGetExactHuntContract(
            [groupedHunt], [0], out _, out _, out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryGetExactHuntContract(
            [exactHunt, talk], [0, 0], out _, out _, out _)).IsFalse();
        await Assert.That(BotQuestCommand.TryGetExactHuntContract(
            [exactHunt], [], out _, out _, out _)).IsFalse();
    }

    [Test]
    public async Task BotQuest_CorpseLootRequiresExclusiveBotTagOwnership()
    {
        var bot = AddBot(2);
        var other = AddBot(3);

        await Assert.That(BotQuestCommand.IsSupportedSoloLootOwner(bot, bot, 0)).IsTrue();
        await Assert.That(BotQuestCommand.IsSupportedSoloLootOwner(other, bot, 0)).IsFalse();
        await Assert.That(BotQuestCommand.IsSupportedSoloLootOwner(bot, bot, 42)).IsFalse();
        await Assert.That(BotQuestCommand.IsSupportedSoloLootOwner(bot, null, 0)).IsFalse();
    }

    [Test]
    public async Task BotQuest_ItemUseSelectsExactTargetAndRestoresPreviousTargetOnRejection()
    {
        var bot = AddBot(2);
        var previousTarget = AddBot(3);
        var questTarget = new Npc { ObjId = 4242, TemplateId = 3460 };
        bot.CurrentTarget = previousTarget;
        var exactTargetWasSelected = false;

        var result = BotQuestCommand.UseWithSelectedTarget(bot, questTarget, () =>
        {
            exactTargetWasSelected = ReferenceEquals(bot.CurrentTarget, questTarget);
            return SkillResult.UnitReqsOrFail;
        });

        await Assert.That(exactTargetWasSelected).IsTrue();
        await Assert.That(result).IsEqualTo(SkillResult.UnitReqsOrFail);
        await Assert.That(bot.CurrentTarget).IsSameReferenceAs(previousTarget);
    }

    [Test]
    public async Task BotQuest_ItemUseRetainsExactTargetForSuccessfulNativeChannel()
    {
        var bot = AddBot(2);
        var questTarget = new Npc { ObjId = 4242, TemplateId = 3460 };
        var exactTargetWasSelected = false;

        var result = BotQuestCommand.UseWithSelectedTarget(bot, questTarget, () =>
        {
            exactTargetWasSelected = ReferenceEquals(bot.CurrentTarget, questTarget);
            return SkillResult.Success;
        });

        await Assert.That(exactTargetWasSelected).IsTrue();
        await Assert.That(result).IsEqualTo(SkillResult.Success);
        await Assert.That(bot.CurrentTarget).IsSameReferenceAs(questTarget);
    }

    [Test]
    public async Task BotQuest_AcquisitionContractDerivesExactNativeNpcAndHealthFloor()
    {
        var skill = new SkillTemplate { Id = 11684, OrUnitReqs = false };
        UnitReqs[] requirements =
        [
            new() { KindType = UnitReqsKindType.TargetHealthLessThan, Value1 = 1, Value2 = 50 },
            new() { KindType = UnitReqsKindType.TargetNpc, Value1 = 3460 },
            new() { KindType = UnitReqsKindType.ProgressQuestContext, Value1 = 293 }
        ];

        var supported = BotQuestCommand.TryGetNativeAcquisitionContract(
            skill, 293, requirements, out var npcTemplateId, out var healthFloor, out var error);

        await Assert.That(supported).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(npcTemplateId).IsEqualTo(3460u);
        await Assert.That(healthFloor).IsEqualTo((byte)50);
    }

    [Test]
    public async Task BotQuest_AcquisitionContractRejectsAmbiguousOrMismatchedNativeRequirements()
    {
        var skill = new SkillTemplate { Id = 11684, OrUnitReqs = true };
        UnitReqs[] requirements =
        [
            new() { KindType = UnitReqsKindType.TargetHealthLessThan, Value1 = 1, Value2 = 50 },
            new() { KindType = UnitReqsKindType.TargetNpc, Value1 = 3460 },
            new() { KindType = UnitReqsKindType.ProgressQuestContext, Value1 = 293 }
        ];

        await Assert.That(BotQuestCommand.TryGetNativeAcquisitionContract(
            skill, 293, requirements, out _, out _, out _)).IsFalse();

        skill.OrUnitReqs = false;
        await Assert.That(BotQuestCommand.TryGetNativeAcquisitionContract(
            skill, 294, requirements, out _, out _, out _)).IsFalse();
    }

    [Test]
    public async Task BotAttackObject_StatusUsesBotWorldLookupForSystemCommands()
    {
        var requestedObjId = 0u;
        BotAttackObjectCommand.NpcResolver = (_, objId) =>
        {
            requestedObjId = objId;
            return null;
        };
        var requester = new CharacterMock();
        var output = new CharacterMessageOutput(requester);

        new BotAttackObjectCommand().Execute(requester, ["status", "4242"], output);

        await Assert.That(requestedObjId).IsEqualTo(4242u);
        await Assert.That(output.Messages.Single()).Contains("NPC object 4242 was not found");
    }

    [Test]
    public async Task BotAttackObject_NonLethalFloorAcceptsOnlyOneToNinetyNinePercent()
    {
        await Assert.That(BotAttackObjectCommand.TryParseStopAtHpPercent("50", out var percent)).IsTrue();
        await Assert.That(percent).IsEqualTo((byte)50);
        await Assert.That(BotAttackObjectCommand.TryParseStopAtHpPercent("0", out _)).IsFalse();
        await Assert.That(BotAttackObjectCommand.TryParseStopAtHpPercent("100", out _)).IsFalse();
        await Assert.That(BotAttackObjectCommand.TryParseStopAtHpPercent("half", out _)).IsFalse();
    }

    [Test]
    public async Task BotAttackObject_ArmsCombatManagersAuthoritativeState()
    {
        var bot = AddBot(2);
        var authoritative = new BotCombatState { BotId = bot.Id };
        _combatManager.States[bot.Id] = authoritative;

        var resolved = BotAttackObjectCommand.EnsureAuthoritativeCombatState(bot);

        await Assert.That(resolved).IsSameReferenceAs(authoritative);
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
    }

    [Test]
    public async Task BotQuest_InspectActDescriptionsExposeExactNativeFixtureTargets()
    {
        var component = new QuestComponentTemplate(new QuestTemplate());

        await Assert.That(BotQuestCommand.DescribeAct(new QuestActObjMonsterHunt(component)
        {
            NpcId = 10220,
            HighlightDoodadId = 443,
            Count = 3
        })).IsEqualTo("QuestActObjMonsterHunt npc=10220 highlight_doodad=443 count=3");
        await Assert.That(BotQuestCommand.DescribeAct(new QuestActObjTalk(component)
        {
            NpcId = 145,
            ItemId = 900,
            TeamShare = true
        })).IsEqualTo("QuestActObjTalk npc=145 item=900 team_share=true");
        await Assert.That(BotQuestCommand.DescribeAct(new QuestActObjItemGather(component)
        {
            ItemId = 901,
            Cleanup = true,
            Count = 2
        })).IsEqualTo("QuestActObjItemGather item=901 cleanup=true count=2");
        await Assert.That(BotQuestCommand.DescribeAct(new QuestActObjDistance(component)
        {
            NpcId = 146,
            Distance = 12,
            WithIn = true
        })).IsEqualTo("QuestActObjDistance npc=146 distance=12 within=true");
        await Assert.That(BotQuestCommand.DescribeAct(new QuestActObjSphere(component)
        {
            SphereId = 47,
            NpcId = 146
        })).IsEqualTo("QuestActObjSphere sphere=47 npc=146");
        await Assert.That(BotQuestCommand.DescribeAct(new QuestActSupplyRemoveItem(component)
        {
            ItemId = 901,
            Count = 2
        })).IsEqualTo("QuestActSupplyRemoveItem item=901 count=2");
    }

    [Test]
    public async Task BotGear_Show_UsesCurrentlyTargetedLiveBotWhenIdIsOmitted()
    {
        var bot = AddBot(2);
        var requester = new CharacterMock { CurrentTarget = bot };
        var output = new CharacterMessageOutput(requester);

        new BotGearCommand().Execute(requester, ["show"], output);

        await Assert.That(output.Messages.Any(message => message.Contains("Bot 'bot2' (Id: 2)"))).IsTrue();
    }

    [Test]
    public async Task BotGear_CreateRefresh_RestartsTheSameBotAfterACompleteDespawn()
    {
        var calls = new List<string>();
        var refreshed = new CharacterMock { Id = 2 };

        var result = BotGearCommand.RestartAfterCreate(
            2,
            id =>
            {
                calls.Add($"despawn:{id}");
                return true;
            },
            id =>
            {
                calls.Add($"spawn:{id}");
                return refreshed;
            },
            out var despawned);

        await Assert.That(despawned).IsTrue();
        await Assert.That(result).IsSameReferenceAs(refreshed);
        await Assert.That(calls).IsEquivalentTo(["despawn:2", "spawn:2"]);
    }

    [Test]
    public async Task BotGear_CreateRefresh_DoesNotSpawnWhenDespawnFails()
    {
        var spawnCalls = 0;

        var result = BotGearCommand.RestartAfterCreate(
            2,
            _ => false,
            _ =>
            {
                spawnCalls++;
                return new CharacterMock();
            },
            out var despawned);

        await Assert.That(despawned).IsFalse();
        await Assert.That(result).IsNull();
        await Assert.That(spawnCalls).IsEqualTo(0);
    }

    [Test]
    public async Task BotGearCatalog_ParsesGradeAliasesAndScoresMatchingProfiles()
    {
        await Assert.That(BotGearCatalog.TryParseGrade("celestial", out var grade)).IsTrue();
        await Assert.That(grade).IsEqualTo(ItemGrade.Celestial);
        await Assert.That(BotGearCatalog.NormalizeProfile("Wind")).IsEqualTo("gale");
        await Assert.That(BotGearCatalog.NormalizeToken("Great-Sword")).IsEqualTo("greatsword");

        var flame = new BotGearCatalog.AttributeVector(6, 0, 0, 0, 0);
        var desert = new BotGearCatalog.AttributeVector(6, 0, 4, 0, 0);
        var meadow = new BotGearCatalog.AttributeVector(0, 0, 0, 6, 4);
        await Assert.That(BotGearCatalog.Similarity(flame, desert)).IsGreaterThan(0);
        await Assert.That(BotGearCatalog.Similarity(flame, meadow)).IsEqualTo(0);
    }

    [Test]
    public async Task BotState_NonNumeric_SendsHelp()
    {
        var output = Execute(new BotStateCommand(), "x");

        await Assert.That(output.Messages).HasSingleItem();
        await Assert.That(output.Messages.Single()).Contains("Help for |cFFFFFFFF/botstate|r");
    }

    [Test]
    public async Task MoveBot_UnknownFifthArg_DefaultsToRun_Characterization()
    {
        var bot = AddBot(2);

        var output = Execute(new MoveBot(), "2", "1", "2", "3", "crawl");

        var state = _botManager.GetBotState(bot.Id);
        await Assert.That(state.Destination).IsEqualTo(new Vector3(1, 2, 3));
        await Assert.That(state.IsRunning).IsTrue();
        await Assert.That(output.Messages.Single()).Contains("running");
    }

    [Test]
    public async Task MoveBot_DecimalCoordinatesUnderGermanCulture_ParseInvariantly()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var bot = AddBot(2);

            Execute(new MoveBot(), "2", "12.5", "2", "3");

            await Assert.That(_botManager.GetBotState(bot.Id).Destination).IsEqualTo(new Vector3(12.5f, 2, 3));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task MoveBot_TeleportUsesExplicitStagingPathAndClearsDestination()
    {
        var bot = AddBot(2);
        _botManager.SetBotDestination(bot, 9, 9, 9);
        var previousTeleporter = MoveBot.Teleporter;
        Character teleportedBot = null;
        var teleportedPosition = Vector3.Zero;
        try
        {
            MoveBot.Teleporter = (candidate, x, y, z) =>
            {
                teleportedBot = candidate;
                teleportedPosition = new Vector3(x, y, z);
                _botManager.GetBotState(candidate.Id).Destination = null;
            };

            var output = Execute(new MoveBot(), "2", "12.5", "2", "3", "teleport");

            await Assert.That(teleportedBot).IsSameReferenceAs(bot);
            await Assert.That(teleportedPosition).IsEqualTo(new Vector3(12.5f, 2, 3));
            await Assert.That(_botManager.GetBotState(bot.Id).Destination).IsNull();
            await Assert.That(output.Messages.Single()).Contains("teleported for GM staging");
        }
        finally
        {
            MoveBot.Teleporter = previousTeleporter;
        }
    }

    [Test]
    public async Task MoveBot_NaNCoordinate_Rejected()
    {
        AddBot(2);

        var output = Execute(new MoveBot(), "2", "NaN", "2", "3");

        await Assert.That(output.Messages.Single()).Contains("Help for |cFFFFFFFF/movebot|r");
        await Assert.That(_botManager.GetBotState(2).Destination).IsNull();
    }

    [Test]
    public async Task BotArchetype_UnknownKeyword_Rejected()
    {
        var bot = AddBot(2);
        _archetypeManager.States[bot.Id] = new BotArchetypeState
        {
            IsInitialized = true,
            ArchetypeName = "Darkrunner"
        };

        var output = Execute(new BotArchetypeCommand(), "2", "rerol");

        await Assert.That(output.Messages.Single()).Contains("Unknown archetype action");
    }

    [Test]
    public async Task BotState_Free_MessageReportsActualCurrentState()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Grinding,
            ForcedState = BotCombatStateType.Grinding,
            IsActive = true
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "free");

        await Assert.That(state.IsForced).IsFalse();
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
        await Assert.That(output.Messages.Single()).Contains("current state: Grinding");
        await Assert.That(output.Messages.Single()).DoesNotContain("returned to idle");
    }

    [Test]
    public async Task BotState_Free_IdleActive_TransitionsToGrinding()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            IsActive = true,
            ForcedState = BotCombatStateType.Grinding
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "free");

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(output.Messages.Single()).Contains("current state: Grinding");
    }

    [Test]
    public async Task BotState_GrindingKillGoalArmsOneKill()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            KillCount = 7,
            KillGoal = 9
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "grind", "1");

        await Assert.That(state.KillGoal).IsEqualTo(1);
        await Assert.That(state.KillCount).IsEqualTo(0);
        await Assert.That(state.IsActive).IsTrue();
        await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
        await Assert.That(output.Messages.Single()).Contains("kill goal 1");
    }

    [Test]
    public async Task BotState_Following_ReattachesListenerEvenWhenInactive()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            IsActive = false
        };
        _combatManager.States[bot.Id] = state;

        Execute(new BotStateCommand(), "2", "following");

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Following);
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
    }

    [Test]
    public async Task BotState_GrindingRejectsNonPositiveKillGoal()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            KillCount = 7,
            KillGoal = 9
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "grind", "0");

        await Assert.That(state.KillGoal).IsEqualTo(9);
        await Assert.That(state.KillCount).IsEqualTo(7);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(output.Messages.Single()).Contains("positive kill goal");
    }

    [Test]
    public async Task BotState_StatusReportsArmedNonLethalFloor()
    {
        var bot = new FixedHealthCharacterMock
        {
            Id = 2,
            ObjId = 1002,
            Name = "bot2",
            FixedMaxHp = 100,
            Hp = 100
        };
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, Character>>(_botManager, "ActiveBots")[bot.Id] = bot;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, BotMovementState>>(_botManager, "_botStates")[bot.Id] = new BotMovementState();
        _combatManager.States[bot.Id] = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            StopAtTargetHpPercent = 80
        };

        var output = Execute(new BotStateCommand(), "2");

        await Assert.That(output.Messages.Single()).Contains("StopAtHP: 80%");
    }

    [Test]
    public async Task BotState_IdleWhileInCombat_DisengagesImmediately()
    {
        var bot = AddBot(2);
        var target = AddBot(3);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            ForcedState = BotCombatStateType.Grinding,
            IsActive = true,
            Target = target,
            StopAtTargetHpPercent = 50
        };
        bot.CurrentTarget = target;
        _combatManager.States[bot.Id] = state;
        _botManager.SetFollowTarget(bot, target);
        _botManager.SetBotDestination(bot, 10, 20, 30);

        var output = Execute(new BotStateCommand(), "2", "idle");

        var movement = _botManager.GetBotState(bot.Id);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(state.Target).IsNull();
        await Assert.That(state.StopAtTargetHpPercent).IsNull();
        await Assert.That(bot.CurrentTarget).IsNull();
        await Assert.That(movement.FollowTarget).IsNull();
        await Assert.That(movement.Destination).IsNull();
        await Assert.That(output.Messages.Single()).Contains("forced into Idle state");
    }

    [Test]
    public async Task BotDebug_TransformTelemetry_IsInvariantRoundTripAndReadOnly()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            var bot = AddDebugBot(2);
            var target = AddBot(3);
            var world = new WorldInstance(
                new WorldTemplate { Id = 0, Name = "main_world" },
                0,
                true,
                43);
            const uint zoneId = 601;
            var position = new Vector3(-123.45679f, 0.00012345678f, -98765.43f);
            const float yaw = 1.2345678f;
            BotTestFixture.SetPrivateField(bot.Transform, "_instanceId", world.Id);
            BotTestFixture.SetPrivateField(bot.Transform, "<WorldId>k__BackingField", 0u);
            BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", zoneId);
            bot.Transform.Local.SetPosition(position);
            bot.Transform.Local.SetRotation(-0.25f, 0.75f, yaw);
            BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
            bot.CurrentTarget = target;
            bot.IsInBattle = true;

            var combat = new BotCombatState
            {
                CurrentState = BotCombatStateType.Combat,
                PreviousState = BotCombatStateType.Idle,
                ForcedState = BotCombatStateType.Grinding,
                IsActive = true,
                Target = target
            };
            _combatManager.States[bot.Id] = combat;
            var movement = _botManager.GetBotState(bot.Id);
            movement.Destination = new Vector3(-7.25f, 8.5f, -9.75f);
            movement.IsRunning = false;
            movement.IsMoving = true;
            movement.IsFalling = true;
            movement.FallVelocity = -4.25f;
            movement.FollowTarget = target;

            var originalPosition = bot.Transform.World.Position;
            var originalRotation = bot.Transform.World.Rotation;
            var originalDestination = movement.Destination;
            var expectedTransform = string.Create(CultureInfo.InvariantCulture,
                $"[botdebug] Transform: world=0, instance=43, zone={zoneId}, " +
                $"x={position.X:R}, y={position.Y:R}, z={position.Z:R}, yaw_rad={yaw:R}");

            var output = Execute(new BotDebugCommand(), bot.Id.ToString(CultureInfo.InvariantCulture));
            var messages = output.Messages.ToList();
            var positionIndex = messages.IndexOf($"[botdebug] Position: {originalPosition}");
            var transformIndex = messages.IndexOf(expectedTransform);
            var transformLine = messages.Single(message =>
                message.StartsWith("[botdebug] Transform: ", StringComparison.Ordinal));

            await Assert.That(positionIndex).IsGreaterThanOrEqualTo(0);
            await Assert.That(transformIndex).IsEqualTo(positionIndex + 1);
            await Assert.That(messages).Contains("[botdebug] === Bot 'bot2' (Id: 2, ObjId: 1002) ===");
            await Assert.That(messages).Contains("[botdebug] HP: 100/100, MP: 100/100");
            await Assert.That(messages).Contains("[botdebug] IsDead: False, IsInBattle: True");
            await Assert.That(messages).Contains("[botdebug] --- Movement State ---");
            await Assert.That(messages).Contains("[botdebug] IsRunning: False");
            await Assert.That(messages).Contains("[botdebug] IsMoving: True");
            await Assert.That(messages).Contains("[botdebug] IsFalling: True");
            await Assert.That(messages).Contains("[botdebug] FollowTarget: bot3");
            await Assert.That(messages).Contains("[botdebug] --- Combat State ---");
            await Assert.That(messages).Contains("[botdebug] State: Combat, Previous: Idle, Forced: Grinding");
            await Assert.That(messages).Contains("[botdebug] Target: bot3, CurrentTarget: bot3");
            await Assert.That(messages).Contains(message => message.StartsWith("[botdebug] Host metrics: ", StringComparison.Ordinal));

            var parsedYaw = float.Parse(
                transformLine[(transformLine.LastIndexOf('=') + 1)..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            await Assert.That(BitConverter.SingleToInt32Bits(parsedYaw))
                .IsEqualTo(BitConverter.SingleToInt32Bits(yaw));
            await Assert.That(transformLine).DoesNotContain("yaw_rad=1,2345678");

            await Assert.That(bot.Transform.WorldId).IsEqualTo(0u);
            await Assert.That(bot.Transform.InstanceId).IsEqualTo(43u);
            await Assert.That(bot.Transform.ZoneId).IsEqualTo(zoneId);
            await Assert.That(bot.Transform.World.Position).IsEqualTo(originalPosition);
            await Assert.That(bot.Transform.World.Rotation).IsEqualTo(originalRotation);
            await Assert.That(bot.CurrentTarget).IsSameReferenceAs(target);
            await Assert.That(bot.IsInBattle).IsTrue();
            await Assert.That(combat.CurrentState).IsEqualTo(BotCombatStateType.Combat);
            await Assert.That(combat.PreviousState).IsEqualTo(BotCombatStateType.Idle);
            await Assert.That(combat.ForcedState).IsEqualTo(BotCombatStateType.Grinding);
            await Assert.That(combat.IsActive).IsTrue();
            await Assert.That(combat.Target).IsSameReferenceAs(target);
            await Assert.That(movement.Destination).IsEqualTo(originalDestination);
            await Assert.That(movement.IsRunning).IsFalse();
            await Assert.That(movement.IsMoving).IsTrue();
            await Assert.That(movement.IsFalling).IsTrue();
            await Assert.That(movement.FallVelocity).IsEqualTo(-4.25f);
            await Assert.That(movement.FollowTarget).IsSameReferenceAs(target);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Test]
    public async Task BotDebug_SearchingState_ExposesLossSearchAndDuelDiagnostics()
    {
        var bot = AddDebugBot(2);
        var opponent = AddBot(3);
        bot.CurrentTarget = opponent;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Searching,
            PreviousState = BotCombatStateType.Dueling,
            ForcedState = BotCombatStateType.Grinding,
            IsActive = true,
            IsSearching = true,
            SearchStartTime = DateTime.UtcNow.AddSeconds(-5),
            SearchRadius = 4.5f,
            SearchAngle = 1.25f,
            LastKnownTargetPosition = new Vector3(10, 20, 30),
            InDuel = true,
            DuelOpponent = opponent
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotDebugCommand(), "2");

        await Assert.That(output.Messages).Contains(message =>
            message.Contains("State: Searching, Previous: Dueling, Forced: Grinding"));
        await Assert.That(output.Messages).Contains("[botdebug] Stealthed: False");
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("Target: null, CurrentTarget: bot3"));
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("Duel: active=True, opponent=bot3"));
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("Search: active=True") &&
            message.Contains("radius=4.50") &&
            message.Contains("angle=1.25") &&
            message.Contains("last_known=<10, 20, 30>"));
    }

    [Test]
    public async Task BotDebug_AutonomousDecision_ExposesLifeTransitionReasonAndTimestamps()
    {
        var bot = AddDebugBot(4);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        var movement = _botManager.GetBotState(bot.Id);
        var combat = new BotCombatState();
        _combatManager.States[bot.Id] = combat;
        var blackboard = new BotBlackboard();
        blackboard.Register(BotValues.NearbyHostileNpcIds, new ManualValue<List<uint>>([9901u]));
        world.AddObject(new Npc { ObjId = 9901, Hp = 100, MaxHp = 100 });
        var broadcaster = new BotMovementBroadcaster(bot, BotHost.Instance.TimeProvider);
        var mover = new BotMovementTask(bot, movement, broadcaster);
        var brain = new BotCombatTask(
            bot,
            combat,
            broadcaster,
            onCancel: null,
            blackboard: blackboard,
            timeProvider: BotHost.Instance.TimeProvider);
        var runtime = new BotRuntime(
            bot,
            movement,
            combat,
            broadcaster,
            mover,
            brain,
            blackboard,
            new BotConfig { UseEngine = false });
        BotHost.Instance.Register(runtime);
        try
        {
            runtime.LifeController.Step(runtime, true, BotHost.Instance.TimeProvider.GetUtcNow());

            var output = Execute(new BotDebugCommand(), "4");

            await Assert.That(output.Messages).Contains(message =>
                message.Contains("Life: state=Active") && message.Contains("entered_at=2026-"));
            await Assert.That(output.Messages).Contains(message =>
                message.Contains("Life transition: event=ActivityRequested") &&
                message.Contains("outcome=Accepted") &&
                message.Contains("reason=StateChanged") &&
                message.Contains("at=2026-"));
            await Assert.That(output.Messages).Contains(message =>
                message.Contains("Life decision: activity=grind, reason=nearby_mortal") &&
                message.Contains("at=2026-"));
            await Assert.That(output.Messages).Contains(
                "[botdebug] Life recovery: state=not_required, started_at=none, completed_at=none, " +
                "observed_at=none, resources=pending, hp=unavailable/unavailable, mp=unavailable/unavailable");
            await Assert.That(output.Messages).Contains(message =>
                message.Contains("Life baseline: captured_at=2026-") &&
                message.Contains("hp=100/100") &&
                message.Contains("mp=100/100") &&
                message.Contains("inventory=unavailable") &&
                message.Contains("summary=unavailable") &&
                message.Contains("fingerprint=unavailable"));
            await Assert.That(output.Messages).Contains("[botdebug] Life completion: pending");
            await Assert.That(output.Messages).Contains("[botdebug] Life delta: pending");

            bot.Hp = 80;
            bot.Mp = 70;
            combat.KillCount = 1;
            combat.TransitionTo(BotCombatStateType.Idle);
            runtime.LifeController.Step(
                runtime,
                true,
                BotHost.Instance.TimeProvider.GetUtcNow().AddSeconds(1));

            var pendingOutput = Execute(new BotDebugCommand(), "4");

            await Assert.That(pendingOutput.Messages).Contains(message =>
                message.Contains("Life recovery: state=pending") &&
                message.Contains("started_at=2026-") &&
                message.Contains("completed_at=none") &&
                message.Contains("observed_at=2026-") &&
                message.Contains("resources=available") &&
                message.Contains("hp=80/100") &&
                message.Contains("mp=70/100"));
            await Assert.That(pendingOutput.Messages).Contains("[botdebug] Life completion: pending");
            await Assert.That(pendingOutput.Messages).Contains("[botdebug] Life delta: pending");

            bot.Hp = 100;
            bot.Mp = 100;
            runtime.LifeController.Step(
                runtime,
                true,
                BotHost.Instance.TimeProvider.GetUtcNow().AddSeconds(2));

            var completedOutput = Execute(new BotDebugCommand(), "4");

            await Assert.That(completedOutput.Messages).Contains(message =>
                message.Contains("Life recovery: state=completed") &&
                message.Contains("started_at=2026-") &&
                message.Contains("completed_at=2026-") &&
                message.Contains("resources=available") &&
                message.Contains("hp=100/100") &&
                message.Contains("mp=100/100"));
            await Assert.That(completedOutput.Messages).Contains(message =>
                message.Contains("Life completion: captured_at=2026-") &&
                message.Contains("hp=100/100") &&
                message.Contains("mp=100/100") &&
                message.Contains("inventory=unavailable"));
            await Assert.That(completedOutput.Messages).Contains(message =>
                message.Contains("Life delta: level=+0, experience=+0") &&
                message.Contains("hp=+0, max_hp=+0") &&
                message.Contains("mp=+0, max_mp=+0") &&
                message.Contains("bag_slots=unavailable, bag_units=unavailable") &&
                message.Contains("inventory_changed=unavailable"));
        }
        finally
        {
            BotHost.Instance.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task BotBuff_AppliesAndRemovesStealthWithoutASelectedTarget()
    {
        var bot = AddBot(2);
        var active = false;
        Buff appliedBuff = null;
        var buffs = Mock.Of<IBuffs>();
        buffs.CheckBuff(599).Returns(_ => active);
        buffs.AddBuff(Any<Buff>(), Any<uint>(), Any<int>())
            .Callback((Buff buff, uint index, int forcedDuration) =>
            {
                appliedBuff = buff;
                active = true;
            });
        buffs.RemoveBuff(599).Callback(_ => active = false);
        bot.Buffs = buffs.Object;
        BotBuffCommand.BuffTemplateResolver = id => id == 599
            ? new BuffTemplate { Id = 599, Duration = 45000, Stealth = true }
            : null;

        var applied = Execute(new BotBuffCommand(), "2", "599", "1");
        var wasApplied = bot.Buffs.CheckBuff(599);
        var removed = Execute(new BotBuffCommand(), "2", "-599");

        await Assert.That(wasApplied).IsTrue();
        await Assert.That(bot.Buffs.CheckBuff(599)).IsFalse();
        await Assert.That(appliedBuff).IsNotNull();
        await Assert.That(appliedBuff.Template.Stealth).IsTrue();
        await Assert.That(appliedBuff.Owner).IsEqualTo(bot);
        await Assert.That(appliedBuff.Caster).IsEqualTo(bot);
        await Assert.That(applied.Messages.Single()).Contains("stealth=True");
        await Assert.That(removed.Messages.Single()).Contains("Removed buff 599");
    }

    [Test]
    public async Task BotBuff_UnknownBuff_ReportsErrorWithoutMutation()
    {
        var bot = AddBot(2);
        BotBuffCommand.BuffTemplateResolver = _ => null;

        var output = Execute(new BotBuffCommand(), "2", "999999");

        await Assert.That(bot.Buffs.CheckBuff(999999)).IsFalse();
        await Assert.That(output.Messages.Single()).Contains("Unknown buff id 999999");
    }

    [Test]
    public async Task BotBuffNpc_AppliesAndRemovesStealthOnExactSuppliedObject()
    {
        var bot = AddBot(2);
        var active = false;
        Buff appliedBuff = null;
        var buffs = Mock.Of<IBuffs>();
        buffs.CheckBuff(599).Returns(_ => active);
        buffs.AddBuff(Any<Buff>(), Any<uint>(), Any<int>())
            .Callback((Buff buff, uint index, int forcedDuration) =>
            {
                appliedBuff = buff;
                active = true;
            });
        buffs.RemoveBuff(599).Callback(_ => active = false);
        var npc = new Npc
        {
            ObjId = 9901,
            TemplateId = 7901,
            Template = new NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100,
            Buffs = buffs.Object
        };
        BotBuffCommand.NpcResolver = (candidate, objId) =>
            ReferenceEquals(candidate, bot) && objId == npc.ObjId ? npc : null;
        BotBuffCommand.BuffTemplateResolver = id => id == 599
            ? new BuffTemplate { Id = 599, Duration = 45000, Stealth = true }
            : null;

        var command = new BotBuffCommand();
        var applied = Execute(command, "2", "9901", "599", "1");
        var wasApplied = npc.Buffs.CheckBuff(599);
        var removed = Execute(command, "2", "9901", "-599", "1");

        await Assert.That(command.CommandNames).Contains("botbuffnpc");
        await Assert.That(wasApplied).IsTrue();
        await Assert.That(npc.Buffs.CheckBuff(599)).IsFalse();
        await Assert.That(appliedBuff).IsNotNull();
        await Assert.That(appliedBuff.Owner).IsEqualTo(npc);
        await Assert.That(appliedBuff.Caster).IsEqualTo(bot);
        await Assert.That(applied.Messages.Single()).Contains("NPC object 9901");
        await Assert.That(applied.Messages.Single()).Contains("stealth=True");
        await Assert.That(removed.Messages.Single()).Contains("Removed buff 599");
    }

    [Test]
    public async Task BotDuel_DeadBot_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        bot1.Hp = 0;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(output.Messages.Single()).Contains("dead bot");
    }

    [Test]
    public async Task BotDuel_DifferentInstances_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        BotTestFixture.SetPrivateField(bot2.Transform, "_instanceId", bot1.Transform.InstanceId + 1);

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(output.Messages.Single()).Contains("same instance");
    }

    [Test]
    public async Task BotDuel_CharacterAlreadyInDuel_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        bot2.IsInDuel = true;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(output.Messages.Single()).Contains("out of a duel");
    }

    [Test]
    public async Task BotDuel_BothFree_UsesResolver()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        var requested = false;
        var duelManager = Mock.Of<IDuelManager>();
        duelManager.DuelRequest(Any<Character>(), Any<uint>())
            .Callback((Character challenger, uint challengedId) => requested = challenger == bot1 && challengedId == bot2.Id);
        BotDuelCommand.DuelManagerResolver = () => duelManager.Object;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(requested).IsTrue();
        await Assert.That(output.Messages.Single()).Contains("challenged 'bot3'");
    }

    [Test]
    public async Task BotDuel_BotInExpedition_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        bot2.Expedition = new Expedition();
        var requested = false;
        var duelManager = Mock.Of<IDuelManager>();
        duelManager.DuelRequest(Any<Character>(), Any<uint>())
            .Callback((Character challenger, uint challengedId) => requested = challenger == bot1 && challengedId == bot2.Id);
        BotDuelCommand.DuelManagerResolver = () => duelManager.Object;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(requested).IsFalse();
        await Assert.That(output.Messages.Single()).Contains("expedition");
    }

    [Test]
    public async Task ReloadBotArchetype_ParseFailure_ReportsError()
    {
        _archetypeManager.ReloadResult = false;

        var output = Execute(new BotArchetypeReloadCommand());

        await Assert.That(_archetypeManager.ReloadCalls).IsEqualTo(1);
        await Assert.That(output.Messages.Single()).Contains("reload failed");
    }

    private CharacterMock AddBot(uint id)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, Character>>(_botManager, "ActiveBots")[id] = bot;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, BotMovementState>>(_botManager, "_botStates")[id] = new BotMovementState();
        return bot;
    }

    private DebugCharacterMock AddDebugBot(uint id)
    {
        var bot = new DebugCharacterMock { Id = id, ObjId = 1000 + id, Name = $"bot{id}", Hp = 100, Mp = 100 };
        bot.Transform.Local.SetPosition(Vector3.Zero);
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, Character>>(_botManager, "ActiveBots")[id] = bot;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, BotMovementState>>(_botManager, "_botStates")[id] = new BotMovementState();
        return bot;
    }

    private sealed class DebugCharacterMock : CharacterMock
    {
        public override int MaxHp => 100;
        public override int MaxMp => 100;
    }

    private static CharacterMessageOutput Execute(ICommand command, params string[] args)
    {
        var output = new CharacterMessageOutput(new CharacterMock());
        command.Execute(new CharacterMock(), args, output);
        return output;
    }
}
