using System.Text.Json;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Models.Game.Skills.Buffs;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Body;

public class BotSkillGateTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Check_NoTemplate_ReturnsNoTemplate()
    {
        var result = BotSkillGate.Check(new CharacterMock { Hp = 100 }, null, null, 0, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.NoTemplate);
    }

    [Test]
    public async Task Check_DeadBot_ReturnsDead()
    {
        var result = BotSkillGate.Check(new CharacterMock { Hp = 0 }, Skill(), null, 0, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Dead);
    }

    [Test]
    public async Task Check_DeadTarget_ReturnsTargetDead()
    {
        var bot = Bot(100);
        var target = new CharacterMock { Hp = 0 };
        var result = BotSkillGate.Check(bot, Skill(SkillTargetType.Hostile), target, 5, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.TargetDead);
    }

    [Test]
    public async Task Check_CurrentSkill_ReturnsCasting()
    {
        var bot = Bot(100);
        bot.SkillTask = new TestSkillTask();

        var result = BotSkillGate.Check(bot, Skill(), null, 0, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Casting);
    }

    [Test]
    public async Task Check_Cooldown_ReturnsCooldown()
    {
        var bot = Bot(100);
        bot.Cooldowns.Cooldowns[7] = Now.AddSeconds(1);

        var result = BotSkillGate.Check(bot, Skill(7), null, 0, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Cooldown);
    }

    [Test]
    public async Task Check_GlobalCooldown_ReturnsGlobalCooldown()
    {
        var bot = Bot(100);
        bot.SkillLastUsed = Now.AddMilliseconds(-BotConfig.Instance.GlobalSkillDelayMs + 1);

        var result = BotSkillGate.Check(bot, Skill(), null, 0, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.GlobalCooldown);
    }

    [Test]
    public async Task Check_UsesSuppliedConfigForGlobalDelay()
    {
        var bot = Bot(100);
        bot.SkillLastUsed = Now;
        var config = new BotConfig { GlobalSkillDelayMs = 0 };

        var result = BotSkillGate.Check(bot, Skill(), null, 0, Now, config);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Ok);
    }

    [Test]
    public async Task Check_AllowlistedLookingSkillStillUsesGate()
    {
        var bot = Bot(100);
        bot.Cooldowns.Cooldowns[2] = Now.AddSeconds(1);

        var result = BotSkillGate.Check(bot, Skill(2), null, 0, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Cooldown);
    }

    [Test]
    public async Task Check_CleanseExemptionAllowsControlledCastButNormalSkillDoesNot()
    {
        var bot = Bot(100);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(bot, bot,
                new SkillCasterUnit(bot.ObjId), new BuffTemplate { Stun = true }, null, Now)));
        bot.Buffs = buffs.Object;
        var target = UnitTarget();
        var cleanse = Skill(11429);
        var normal = Skill(12029);

        var cleanseResult = BotSkillGate.Check(bot, cleanse, target, 1, Now, castWhileControlled: true);
        var normalResult = BotSkillGate.Check(bot, normal, target, 1, Now);

        await Assert.That(cleanseResult.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(normalResult.Reason).IsEqualTo(GateReason.Controlled);
    }

    [Test]
    public async Task Check_OutOfMinRange_ReturnsOutOfRange()
    {
        var result = BotSkillGate.Check(Bot(100), Skill(SkillTargetType.Hostile, minRange: 5, maxRange: 20), UnitTarget(), 4.9f, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.OutOfRange);
    }

    [Test]
    public async Task Check_OutOfMaxRange_ReturnsOutOfRange()
    {
        var result = BotSkillGate.Check(Bot(100), Skill(SkillTargetType.Hostile, minRange: 5, maxRange: 20), UnitTarget(), 20.1f, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.OutOfRange);
    }

    [Test]
    public async Task Check_SelfTargetIgnoresRange()
    {
        var result = BotSkillGate.Check(Bot(100), Skill(SkillTargetType.Self, minRange: 50, maxRange: 1), null, 100, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Ok);
    }

    [Test]
    public async Task Check_NotEnoughMana_ReturnsNotEnoughMana()
    {
        var bot = Bot(0);
        var result = BotSkillGate.Check(bot, Skill(manaCost: 1), UnitTarget(), 1, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.NotEnoughMana);
    }

    [Test]
    public async Task Check_NotEnoughLabor_ReturnsNotEnoughLabor()
    {
        var bot = Bot(100);
        bot.InitializeLaborCache(0, Now);
        var result = BotSkillGate.Check(bot, Skill(laborCost: 1), UnitTarget(), 1, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.NotEnoughLabor);
    }

    [Test]
    public async Task Check_RealSkillRows_UseDataForSilenceAndGateDecisions()
    {
        var templates = LoadSkillTemplates();
        var bot = Bot(100);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(bot, bot, new SkillCasterUnit(bot.ObjId),
                new BuffTemplate { Silence = true }, null, Now)));
        bot.Buffs = buffs.Object;

        var target = UnitTarget();
        var flamebolt = templates[10752];
        var freezingArrow = templates[10667];
        var toxicShot = templates[10481];
        var meleeAuto = templates[2];
        var offhandAuto = templates[3];
        var rangedAuto = templates[4];

        var flameboltResult = BotSkillGate.Check(bot, flamebolt, target, 1, Now);
        var freezingArrowResult = BotSkillGate.Check(bot, freezingArrow, target, 1, Now);
        var toxicShotResult = BotSkillGate.Check(bot, toxicShot, target, 1, Now);
        var meleeAutoResult = BotSkillGate.Check(bot, meleeAuto, target, 1, Now);
        var offhandAutoResult = BotSkillGate.Check(bot, offhandAuto, target, 1, Now);
        var rangedAutoResult = BotSkillGate.Check(bot, rangedAuto, target, 5, Now);

        await Assert.That(templates).Count().IsEqualTo(6);
        await Assert.That(flamebolt.ManaCost).IsEqualTo(16);
        await Assert.That(flamebolt.CastingTime).IsEqualTo(1000);
        await Assert.That(flamebolt.MaxRange).IsEqualTo(20);
        await Assert.That(freezingArrow.CooldownTime).IsEqualTo(6000);
        await Assert.That(freezingArrow.CastingTime).IsEqualTo(2000);
        await Assert.That(toxicShot.AbilityId).IsEqualTo(AbilityType.Vocation);
        await Assert.That(meleeAuto.MaxRange).IsEqualTo(25);
        await Assert.That(offhandAuto.IgnoreGlobalCooldown).IsTrue();
        await Assert.That(rangedAuto.MinRange).IsEqualTo(4);
        await Assert.That(flamebolt.TargetType).IsEqualTo(SkillTargetType.Hostile);
        await Assert.That(flamebolt.TargetRelation).IsEqualTo(SkillTargetRelation.Any);
        await Assert.That(flameboltResult.Reason).IsEqualTo(GateReason.Controlled);
        await Assert.That(freezingArrowResult.Reason).IsEqualTo(GateReason.Controlled);
        await Assert.That(toxicShotResult.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(meleeAutoResult.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(offhandAutoResult.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(rangedAutoResult.Reason).IsEqualTo(GateReason.Ok);
    }

    [Test]
    public async Task Check_RealSkillRows_EnforceRangeCostCooldownGcdAndRelation()
    {
        var templates = LoadSkillTemplates();
        var target = UnitTarget();

        var rangeResult = BotSkillGate.Check(Bot(100), templates[10752], target, 21, Now);

        var lowMana = Bot(15);
        var costResult = BotSkillGate.Check(lowMana, templates[10752], target, 1, Now);

        var cooldownBot = Bot(100);
        cooldownBot.Cooldowns.Cooldowns[10667] = Now.AddSeconds(1);
        var cooldownResult = BotSkillGate.Check(cooldownBot, templates[10667], target, 1, Now);

        var gcdBot = Bot(100);
        gcdBot.GlobalCooldown = Now.AddSeconds(1);
        var gcdResult = BotSkillGate.Check(gcdBot, templates[10752], target, 1, Now);
        var ignoredGcdResult = BotSkillGate.Check(gcdBot, templates[3], target, 1, Now);

        var friendlyBot = Bot(100);
        friendlyBot.Faction = new() { Id = FactionsEnum.Friendly };
        target.Faction = new() { Id = FactionsEnum.Friendly };
        var relationResult = BotSkillGate.Check(friendlyBot, templates[10752], target, 1, Now);

        await Assert.That(rangeResult.Reason).IsEqualTo(GateReason.OutOfRange);
        await Assert.That(costResult.Reason).IsEqualTo(GateReason.NotEnoughMana);
        await Assert.That(cooldownResult.Reason).IsEqualTo(GateReason.Cooldown);
        await Assert.That(gcdResult.Reason).IsEqualTo(GateReason.GlobalCooldown);
        await Assert.That(ignoredGcdResult.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(relationResult.Reason).IsEqualTo(GateReason.WrongRelation);
    }

    [Test]
    public async Task Check_StunBlocksAllSkills()
    {
        var bot = Bot(100);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(bot, bot, new SkillCasterUnit(bot.ObjId),
                new BuffTemplate { Stun = true }, null, Now)));
        bot.Buffs = buffs.Object;

        var result = BotSkillGate.Check(bot, Skill(2), UnitTarget(), 1, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Controlled);
    }

    [Test]
    public async Task Check_RootBlocksNormalSkill()
    {
        var bot = Bot(100);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(bot, bot, new SkillCasterUnit(bot.ObjId),
                new BuffTemplate { Root = true }, null, Now)));
        bot.Buffs = buffs.Object;

        var result = BotSkillGate.Check(bot, Skill(2), UnitTarget(), 1, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Controlled);
    }

    [Test]
    public async Task Check_RootAllowsCastWhileControlled()
    {
        var bot = Bot(100);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(bot, bot, new SkillCasterUnit(bot.ObjId),
                new BuffTemplate { Root = true }, null, Now)));
        bot.Buffs = buffs.Object;

        var result = BotSkillGate.Check(bot, Skill(2), UnitTarget(), 1, Now, castWhileControlled: true);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Ok);
    }

    [Test]
    public async Task Check_WrongRelation_ReturnsWrongRelation()
    {
        var bot = Bot(100);
        bot.Faction = new() { Id = FactionsEnum.Friendly };
        var target = UnitTarget();
        target.Faction = new() { Id = FactionsEnum.Friendly };

        var result = BotSkillGate.Check(bot, Skill(SkillTargetType.Hostile), target, 1, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.WrongRelation);
    }

    [Test]
    public async Task Check_ValidSkill_ReturnsOk()
    {
        var result = BotSkillGate.Check(Bot(100), Skill(SkillTargetType.Hostile), UnitTarget(), 1, Now);

        await Assert.That(result.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(result.IsAllowed).IsTrue();
    }

    private static CharacterMock Bot(int mana) => new() { Hp = 100, Mp = mana };

    private static CharacterMock UnitTarget() => new() { Hp = 100 };

    private static Dictionary<uint, SkillTemplate> LoadSkillTemplates()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.UnitTests/Bots/Body/Fixtures/skill-templates.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("templates")
            .Deserialize<List<SkillTemplate>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            .ToDictionary(template => template.Id);
    }

    private static SkillTemplate Skill(
        uint id = 1,
        SkillTargetType targetType = SkillTargetType.Self,
        int minRange = 0,
        int maxRange = 100,
        int manaCost = 0,
        int laborCost = 0)
    {
        return new SkillTemplate
        {
            Id = id,
            TargetType = targetType,
            TargetRelation = SkillTargetRelation.Any,
            MinRange = minRange,
            MaxRange = maxRange,
            ManaCost = manaCost,
            ConsumeLaborPower = laborCost
        };
    }

    private static SkillTemplate Skill(
        SkillTargetType targetType,
        int minRange = 0,
        int maxRange = 100,
        int manaCost = 0,
        int laborCost = 0)
    {
        return Skill(id: 1, targetType: targetType, minRange: minRange, maxRange: maxRange,
            manaCost: manaCost, laborCost: laborCost);
    }

    private sealed class TestSkillTask() : AAEmu.Game.Models.Tasks.Skills.SkillTask(new AAEmu.Game.Models.Game.Skills.Skill())
    {
        public override void Execute()
        {
        }
    }
}
