using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Body;

public sealed class LearnedSkillDecisionTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 20, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Select_UsesOnlyTheLiveLearnedSkillSet()
    {
        var bot = MakeDecisionBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(2, 0, 0));
        target.Hp = 100;
        bot.MaxMp = bot.Mp = 100;
        bot.Skills = new CharacterSkills(bot);
        var nativeStarter = OffensiveSkill(18132, manaCost: 30);
        bot.Skills.Skills[nativeStarter.Id] = new Skill(nativeStarter);

        var selected = LearnedSkillDecision.Select(bot, target, new BotCombatState(), Now,
            new BotConfig { UseEngine = false });

        await Assert.That(selected?.Id).IsEqualTo(18132u);
    }

    [Test]
    public async Task Select_PreservesManaAndThrottlesZeroCooldownFiller()
    {
        var bot = MakeDecisionBot(3, Vector3.Zero);
        var target = BotTestFixture.MakeBot(4, new Vector3(2, 0, 0));
        target.Hp = 100;
        bot.MaxMp = 100;
        bot.Mp = 40;
        bot.Skills = new CharacterSkills(bot);
        var starter = OffensiveSkill(18132, manaCost: 30);
        bot.Skills.Skills[starter.Id] = new Skill(starter);
        var state = new BotCombatState { LastSkillTime = Now.AddSeconds(-1) };

        await Assert.That(LearnedSkillDecision.Select(bot, target, state, Now,
            new BotConfig { UseEngine = false })).IsNull();

        bot.Mp = 100;
        await Assert.That(LearnedSkillDecision.Select(bot, target, state, Now,
            new BotConfig { UseEngine = false })).IsNull();
        state.LastSkillTime = Now.AddSeconds(-2);
        await Assert.That(LearnedSkillDecision.Select(bot, target, state, Now,
            new BotConfig { UseEngine = false })?.Id).IsEqualTo(18132u);
    }

    [Test]
    public async Task Select_IgnoresHiddenInternalSkillsEvenWhenPersisted()
    {
        var bot = MakeDecisionBot(5, Vector3.Zero);
        var target = BotTestFixture.MakeBot(6, new Vector3(2, 0, 0));
        target.Hp = 100;
        bot.Mp = 100;
        bot.Skills = new CharacterSkills(bot);
        var hidden = OffensiveSkill(18131, manaCost: 12);
        hidden.Show = false;
        hidden.NeedLearn = false;
        bot.Skills.Skills[hidden.Id] = new Skill(hidden);

        await Assert.That(LearnedSkillDecision.Select(bot, target, new BotCombatState(), Now,
            new BotConfig { UseEngine = false })).IsNull();
    }

    private static DecisionCharacterMock MakeDecisionBot(uint id, Vector3 position)
    {
        var bot = new DecisionCharacterMock { Id = id, ObjId = 1000 + id, Name = $"bot{id}", Hp = 100 };
        bot.Transform.Local.SetPosition(position);
        return bot;
    }

    private static SkillTemplate OffensiveSkill(uint id, int manaCost) => new()
    {
        Id = id,
        TargetType = SkillTargetType.Hostile,
        TargetRelation = SkillTargetRelation.Hostile,
        MinRange = 0,
        MaxRange = 4,
        ManaCost = manaCost,
        Show = true,
        NeedLearn = true
    };

    private sealed class DecisionCharacterMock : CharacterMock
    {
        public override int MaxMp { get; set; } = 100;
    }
}
