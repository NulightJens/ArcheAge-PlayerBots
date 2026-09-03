using System.Numerics;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class RotationFillerGateTests
{
    [Test]
    public async Task ShippedDarkrunnerFiller_NeverDrawsPinDownAgainstNpc()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var bot = BotTestFixture.MakeBot(740, Vector3.Zero);
        bot.Hp = bot.MaxHp = 100;
        bot.IsAutoAttack = false;
        var target = new Npc
        {
            Id = 741,
            ObjId = 1741,
            Name = "npc741",
            Template = new NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        target.Transform.Local.SetPosition(new Vector3(2, 0, 0));
        var templateResolver = new Func<uint, SkillTemplate>(id => new SkillTemplate
        {
            Id = id,
            MaxRange = 100,
            TargetType = SkillTargetType.Hostile,
            TargetRelation = SkillTargetRelation.Hostile
        });
        bot.Skills = new CharacterSkills(bot);
        foreach (var skillId in BotSkillIds.Darkrunner.SkillLearnOrder)
            bot.Skills.Skills[skillId] = new Skill(templateResolver(skillId));
        var npcRuntime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        var npcRoll = 0;
        var npcStrategy = new BotRotationCompiler(
            roll: () => npcRoll++,
            templateResolver: templateResolver).Compile(rotation);
        var npcContext = new BotContext(bot, npcRuntime, npcRuntime.Blackboard,
            new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc),
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var npcPinDown = Enumerable.Range(0, 104)
            .Select(_ => npcStrategy.Filler.SelectAction(npcContext)?.Name)
            .Any(name => name == "cast:pinDown");

        var character = BotTestFixture.MakeBot(744, new Vector3(2, 0, 0));
        character.Hp = character.MaxHp = 100;
        var characterRuntime = new BotRuntime(bot, new BotMovementState(),
            new BotCombatState { Target = character }, config: new BotConfig { UseEngine = false });
        var characterRoll = 0;
        var characterStrategy = new BotRotationCompiler(
            roll: () => characterRoll++,
            templateResolver: templateResolver).Compile(rotation);
        var characterContext = new BotContext(bot, characterRuntime, characterRuntime.Blackboard,
            new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc),
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var characterPinDown = Enumerable.Range(0, 119)
            .Select(_ => characterStrategy.Filler.SelectAction(characterContext)?.Name)
            .Any(name => name == "cast:pinDown");

        await Assert.That(npcPinDown).IsFalse();
        await Assert.That(characterPinDown).IsTrue();
    }

    [Test]
    public async Task ShippedPrimevalSingleTargetRotation_DoesNotScheduleMissileRain()
    {
        var rotation = Load("primeval.archer", BotSkillIds.Primeval.SkillLearnOrder);
        var actionNames = rotation.Rules.SelectMany(rule => rule.Then)
            .Select(action => action.As)
            .Where(name => name != null);

        await Assert.That(actionNames).DoesNotContain("damage:missileRain");
    }

    private static BotRotationDefinition Load(string file, IReadOnlyCollection<uint> learnOrder)
    {
        var path = BotTestFixture.FindRepoFile($"AAEmu.Game/Data/BotRotations/{file}.json");
        var manager = new BotRotationManager(id => learnOrder.Contains(id), _ => learnOrder);
        if (!manager.LoadRotations(File.ReadAllText(path), file))
            throw new InvalidOperationException("rotation fixture did not load");
        return manager.GetRotation(file);
    }
}
