using System.Runtime.CompilerServices;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Questing;

public sealed class BotQuestAuthorityTests
{
    [Test]
    public async Task InterpretObjectiveReadsExactNativeMonsterHuntTemplateAndCounter()
    {
        var quest = BuildQuest(
            new QuestActObjMonsterHunt(new QuestComponentTemplate(new QuestTemplate()))
            {
                NpcId = 701,
                Count = 4,
                ThisComponentObjectiveIndex = 2
            });
        quest.Objectives[2] = 3;

        var result = BotQuestAuthority.InterpretObjective(quest);

        await Assert.That(result.Shape).IsEqualTo(BotQuestObjectiveShape.MonsterHunt);
        await Assert.That(result.Objective.HasValue).IsTrue();
        await Assert.That(result.Objective.Value.TargetNpcTemplateId).IsEqualTo(701u);
        await Assert.That(result.Objective.Value.ObjectiveIndex).IsEqualTo((byte)2);
        await Assert.That(result.Objective.Value.Current).IsEqualTo(3);
        await Assert.That(result.Objective.Value.Required).IsEqualTo(4);
    }

    [Test]
    public async Task InterpretObjectiveRejectsUnsupportedObjectiveWithoutMutation()
    {
        var unsupported = new QuestActTemplate(new QuestComponentTemplate(new QuestTemplate()))
        {
            CountsAsAnObjective = true,
            ThisComponentObjectiveIndex = 1,
            Count = 2
        };
        var quest = BuildQuest(unsupported);
        quest.Objectives[1] = 1;

        var result = BotQuestAuthority.InterpretObjective(quest);

        await Assert.That(result.Shape).IsEqualTo(BotQuestObjectiveShape.Unsupported);
        await Assert.That(result.Objective.HasValue).IsFalse();
        await Assert.That(quest.Objectives[1]).IsEqualTo(1);
    }

    [Test]
    public async Task InterpretObjectiveRejectsMultipleActiveObjectivesAsAmbiguous()
    {
        var componentTemplate = new QuestComponentTemplate(new QuestTemplate());
        var quest = BuildQuest(
            new QuestActObjMonsterHunt(componentTemplate)
            {
                NpcId = 701,
                Count = 1,
                ThisComponentObjectiveIndex = 0
            },
            new QuestActObjMonsterHunt(componentTemplate)
            {
                NpcId = 702,
                Count = 1,
                ThisComponentObjectiveIndex = 1
            });

        var result = BotQuestAuthority.InterpretObjective(quest);

        await Assert.That(result.Shape).IsEqualTo(BotQuestObjectiveShape.Ambiguous);
        await Assert.That(result.Reason).IsEqualTo("multiple_active_objectives");
    }

    [Test]
    public async Task InterpretObjectiveRejectsInvalidTemplateAndCounterBounds()
    {
        var quest = BuildQuest(
            new QuestActObjMonsterHunt(new QuestComponentTemplate(new QuestTemplate()))
            {
                NpcId = 0,
                Count = 0,
                ThisComponentObjectiveIndex = 4
            });

        var result = BotQuestAuthority.InterpretObjective(quest);

        await Assert.That(result.Shape).IsEqualTo(BotQuestObjectiveShape.Invalid);
        await Assert.That(result.Objective.HasValue).IsFalse();
    }

    private static Quest BuildQuest(params QuestActTemplate[] templates)
    {
        var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
        quest.Objectives = new int[5];
        var step = new QuestStep(QuestComponentKind.Progress, quest);
        var component = (QuestComponent)RuntimeHelpers.GetUninitializedObject(typeof(QuestComponent));
        component.Template = new QuestComponentTemplate(new QuestTemplate())
        {
            Id = 1,
            KindId = QuestComponentKind.Progress
        };
        component.IsCurrentlyActive = true;
        BotTestFixture.SetPrivateField(component, "<Parent>k__BackingField", step);
        component.Acts = templates.Select(template => new QuestAct(component, template)).ToList();
        step.Components[1] = component;
        BotTestFixture.SetPrivateField(
            quest,
            "<QuestSteps>k__BackingField",
            new Dictionary<QuestComponentKind, QuestStep>
            {
                [QuestComponentKind.Progress] = step
            });
        return quest;
    }
}
