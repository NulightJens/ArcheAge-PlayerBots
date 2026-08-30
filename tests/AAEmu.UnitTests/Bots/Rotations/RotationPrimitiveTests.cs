using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class RotationPrimitiveTests
{
    [Test]
    public async Task PrimitiveCatalog_ContainsTheShippedVocabulary()
    {
        await Assert.That(RotationPrimitiveCatalog.ActionKinds).Contains("castAoe");
        await Assert.That(RotationPrimitiveCatalog.ActionKinds).Contains("castHeal");
        await Assert.That(RotationPrimitiveCatalog.ActionKinds).Contains("maintainRange");
        await Assert.That(RotationPrimitiveCatalog.TriggerKinds).Contains("chainStep");
        await Assert.That(RotationPrimitiveCatalog.TriggerKinds).Contains("partyLowest");
        await Assert.That(RotationPrimitiveCatalog.TriggerKinds).Contains("hasTarget");
        await Assert.That(RotationPrimitiveCatalog.ValueKinds).Contains("stalkerActive");
        await Assert.That(RotationPrimitiveCatalog.DeferredKinds).Contains("partyMemberDead");
        await Assert.That(RotationPrimitiveCatalog.DeferredKinds).DoesNotContain("castHeal");
        await Assert.That(RotationPrimitiveCatalog.DeferredKinds).DoesNotContain("partyLowest");
    }

    [Test]
    public async Task BotCastSkillAction_UsesOptionalNameWithoutChangingDefault()
    {
        await Assert.That(new BotCastSkillAction(42).Name).IsEqualTo("cast:42");
        await Assert.That(new BotCastSkillAction(42, name: "cast:opening").Name).IsEqualTo("cast:opening");
    }

    [Test]
    public async Task ReachSpellRangeAction_UsesOptionalName()
    {
        await Assert.That(new ReachSpellRangeAction(15f, name: "reach:opening").Name).IsEqualTo("reach:opening");
    }

    [Test]
    public async Task MaintainSpellRangeAction_UsesOptionalNameAndTemplateRange()
    {
        var action = new MaintainSpellRangeAction(25f, name: "position:antithesis");

        await Assert.That(action.Name).IsEqualTo("position:antithesis");
        await Assert.That(action.MinimumRange).IsEqualTo(23f);
        await Assert.That(action.PreferredRange).IsEqualTo(24f);
        await Assert.That(action.MaximumRange).IsEqualTo(25f);
    }

    [Test]
    public async Task EnemyOutOfSpellRangeTrigger_UsesTheResolvedSkillRange()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(12, 0, 0));
        bot.Hp = bot.MaxHp = 100;
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        var trigger = new EnemyOutOfSpellRangeTrigger(42, _ => new SkillTemplate { Id = 42, MaxRange = 10 });
        var context = new BotContext(bot, runtime, runtime.Blackboard, DateTime.UtcNow,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);

        await Assert.That(trigger.IsActive(context)).IsTrue();
    }

    [Test]
    public async Task WeightedFiller_UsesHostRollAndSkipsClosedGates()
    {
        var first = new ProbeAction("first", true);
        var second = new ProbeAction("second", true);
        var filler = new WeightedFillerAction([new(first, 1), new(second, 3)], () => 175);

        await Assert.That(filler.SelectAction()).IsSameReferenceAs(second);
        first.CanRun = false;
        await Assert.That(filler.SelectAction()).IsSameReferenceAs(second);
    }

    [Test]
    public async Task WeightedFiller_UsesIntegerCumulativeWeightsForOneRollSequence()
    {
        var rolls = new Queue<int>([0, 1, 3]);
        var first = new ProbeAction("first", true);
        var second = new ProbeAction("second", true);
        var filler = new WeightedFillerAction([new(first, 1), new(second, 3)], () => rolls.Dequeue());

        await Assert.That(filler.SelectAction()).IsSameReferenceAs(first);
        await Assert.That(filler.SelectAction()).IsSameReferenceAs(second);
        await Assert.That(filler.SelectAction()).IsSameReferenceAs(second);
    }

    [Test]
    public async Task WeightedFiller_PvpAndPveRowsUseTheirTargetGates()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var npc = new Npc { Id = 2, ObjId = 1002, Template = new NpcTemplate { Scale = 1f } };
        npc.Transform.Local.SetPosition(new Vector3(1, 0, 0));
        var character = BotTestFixture.MakeBot(3, new Vector3(1, 0, 0));
        var pvp = new ProbeAction("pvp", true);
        var pve = new ProbeAction("pve", true);
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = npc },
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, DateTime.UtcNow,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var filler = new WeightedFillerAction([
            new(pvp, 1, c => c.Runtime.CombatState.Target is AAEmu.Game.Models.Game.Char.Character),
            new(pve, 1, c => c.Runtime.CombatState.Target is Npc)
        ], () => 0);

        await Assert.That(filler.SelectAction(context)).IsSameReferenceAs(pve);
        runtime.CombatState.Target = character;
        await Assert.That(filler.SelectAction(context)).IsSameReferenceAs(pvp);
    }

    [Test]
    public async Task RotationStrategy_ExposesOnlyFillerAsDefaultAndUsesRotationSiblingGroup()
    {
        var filler = new WeightedFillerAction([], () => 0);
        var strategy = new RotationStrategy("test.rotation", filler, [], []);

        await Assert.That(strategy.Name).IsEqualTo("rotation");
        await Assert.That(strategy.SiblingGroup).IsEqualTo("rotation");
        await Assert.That(strategy.DefaultActions).Count().IsEqualTo(1);
        await Assert.That(strategy.DefaultActions[0].Name).IsEqualTo("filler");
    }

    private sealed class ProbeAction(string name, bool possible) : IBotAction
    {
        public string Name { get; } = name;
        public bool CanRun { get; set; } = possible;
        public bool IsUseful(BotContext context) => CanRun;
        public bool IsPossible(BotContext context) => CanRun;
        public BotActionResult Execute(BotContext context, BotEvent ev) => BotActionResult.Success;
    }
}
