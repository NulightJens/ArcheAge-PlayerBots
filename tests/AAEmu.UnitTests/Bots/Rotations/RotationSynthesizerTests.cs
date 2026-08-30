using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Models;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;
using Newtonsoft.Json;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class RotationSynthesizerTests
{
    [Test]
    public async Task RealFixtureRows_ClassifyDamageHealBuffAndUtilityEffects()
    {
        var fixture = LoadFixture();

        await Assert.That(RotationSynthesizer.Classify(fixture[10752])).IsEqualTo("damage");
        await Assert.That(RotationSynthesizer.Classify(fixture[10534])).IsEqualTo("heal");
        await Assert.That(RotationSynthesizer.Classify(fixture[10153])).IsEqualTo("buff");
        await Assert.That(RotationSynthesizer.Classify(fixture[10201])).IsEqualTo("damage");
    }

    [Test]
    public async Task PlotSkills_ClassifyBothTheNoEffectAndEffectBackedCases()
    {
        var fixture = LoadFixture();
        var noEffectPlot = fixture.Values.First(row => row.IsPlotWithoutEffects);
        var effectBackedPlot = fixture.Values.First(row => row.PlotId.HasValue && row.HasEffects);

        await Assert.That(noEffectPlot.PlotId).IsNotNull();
        await Assert.That(noEffectPlot.EffectKinds).IsEmpty();
        await Assert.That(RotationSynthesizer.Classify(noEffectPlot)).IsEqualTo("damage");
        await Assert.That(effectBackedPlot.PlotId).IsNotNull();
        await Assert.That(effectBackedPlot.HasEffects).IsTrue();
        await Assert.That(RotationSynthesizer.Classify(effectBackedPlot)).IsNotEqualTo("skipped");
    }

    [Test]
    public async Task DamagePerCost_OrdersTheRealRowsDeterministically()
    {
        var fixture = LoadFixture();
        var ordered = fixture.Values.Where(row => RotationSynthesizer.Classify(row) == "damage")
            .OrderByDescending(RotationSynthesizer.DamagePerCost)
            .ThenBy(row => row.Id)
            .ToArray();

        await Assert.That(ordered).IsNotEmpty();
        await Assert.That(ordered.Zip(ordered.Skip(1), (first, second) =>
                RotationSynthesizer.DamagePerCost(first) >= RotationSynthesizer.DamagePerCost(second)).All(value => value)).IsTrue();
    }

    [Test]
    public async Task Synthesizer_IsByteDeterministicForOneFixture()
    {
        var fixture = LoadFixture();
        var synthesizer = new RotationSynthesizer();
        var rows = fixture;
        var first = synthesizer.Synthesize("reaper.caster", "Reaper", "damage", "ranged",
            BotSkillIds.Reaper.SkillLearnOrder, rows, id => $"skill{id}");
        var second = synthesizer.Synthesize("reaper.caster", "Reaper", "damage", "ranged",
            BotSkillIds.Reaper.SkillLearnOrder, rows, id => $"skill{id}");

        await Assert.That(RotationSynthesizer.Serialize(first.Definition)).IsEqualTo(
            RotationSynthesizer.Serialize(second.Definition));
    }

    [Test]
    public async Task Synthesizer_CoversAllFiveArchetypeLearnOrders()
    {
        var rows = LoadFixture();
        var synthesizer = new RotationSynthesizer();
        var inputs = new[]
        {
            ("abolisher.tank", "Abolisher", BotSkillIds.Abolisher.SkillLearnOrder),
            ("darkrunner.melee", "Darkrunner", BotSkillIds.Darkrunner.SkillLearnOrder),
            ("primeval.archer", "Primeval", BotSkillIds.Primeval.SkillLearnOrder),
            ("reaper.caster", "Reaper", BotSkillIds.Reaper.SkillLearnOrder),
            ("templar.support", "Templar", BotSkillIds.Templar.SkillLearnOrder)
        };

        foreach (var input in inputs)
        {
            var result = synthesizer.Synthesize(input.Item1, input.Item2, "damage", "ranged", input.Item3, rows,
                id => $"skill{id}");
            await Assert.That(result.SkippedSkillIds).IsEmpty();
            await Assert.That(result.Definition.Skills).Count().IsEqualTo(input.Item3.Count);
        }
    }

    [Test]
    public async Task Synthesizer_ExportsTheThreeRemainingRotationsForReview()
    {
        var rows = LoadFixture();
        var synthesizer = new RotationSynthesizer();
        var inputs = new[]
        {
            ("abolisher.tank", "Abolisher", "tank", "melee", BotSkillIds.Abolisher.SkillLearnOrder),
            ("reaper.caster", "Reaper", "damage", "ranged", BotSkillIds.Reaper.SkillLearnOrder),
            ("templar.support", "Templar", "support", "melee", BotSkillIds.Templar.SkillLearnOrder)
        };

        foreach (var input in inputs)
        {
            var result = synthesizer.Synthesize(input.Item1, input.Item2, input.Item3, input.Item4, input.Item5, rows);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), $"r4-{input.Item1}.json"),
                RotationSynthesizer.Serialize(result.Definition));
        }

        await Assert.That(File.Exists(Path.Combine(Path.GetTempPath(), "r4-abolisher.tank.json"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(Path.GetTempPath(), "r4-reaper.caster.json"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(Path.GetTempPath(), "r4-templar.support.json"))).IsTrue();
    }

    [Test]
    public async Task Synthesizer_CommittedThreeExportsAreByteEqual()
    {
        var rows = LoadFixture();
        var synthesizer = new RotationSynthesizer();
        var inputs = new[]
        {
            ("abolisher.tank", "Abolisher", "tank", "melee", BotSkillIds.Abolisher.SkillLearnOrder),
            ("reaper.caster", "Reaper", "damage", "ranged", BotSkillIds.Reaper.SkillLearnOrder),
            ("templar.support", "Templar", "support", "melee", BotSkillIds.Templar.SkillLearnOrder)
        };

        foreach (var input in inputs)
        {
            var expected = RotationSynthesizer.Serialize(synthesizer.Synthesize(input.Item1, input.Item2,
                input.Item3, input.Item4, input.Item5, rows).Definition);
            var path = BotTestFixture.FindRepoFile($"AAEmu.Game/Data/BotRotations/{input.Item1}.json");
            await Assert.That(File.ReadAllText(path)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task SynthesizedRows_UseReadableKeysAndPositivePlotFillerWeights()
    {
        var rows = LoadFixture();
        var result = new RotationSynthesizer().Synthesize("reaper.caster", "Reaper", "damage", "ranged",
            BotSkillIds.Reaper.SkillLearnOrder, rows);

        await Assert.That(result.Definition.Skills.Keys.Any(key => System.Text.RegularExpressions.Regex.IsMatch(key, "^skill\\d+$"))).IsFalse();
        await Assert.That(result.Definition.Default.Where(row => row.Skill != null).All(row => row.Weight > 0)).IsTrue();
        await Assert.That(result.Definition.Default.Select(row => row.Action)).DoesNotContain("autoAttack");
    }

    [Test]
    public async Task SynthesizedRotations_WinOneCombatSimulationTickEach()
    {
        var inputs = new[]
        {
            ("abolisher.tank", "Abolisher", BotSkillIds.Abolisher.SkillLearnOrder),
            ("reaper.caster", "Reaper", BotSkillIds.Reaper.SkillLearnOrder),
            ("templar.support", "Templar", BotSkillIds.Templar.SkillLearnOrder)
        };

        foreach (var input in inputs)
        {
            BotTestFixture.RegisterTaskManager();
            BotTestFixture.SetPrivateField(ModelManager.Instance, "_modelTypes", new Dictionary<uint, ModelType>());
            var sim = new BotSim(seed: 17);
            try
            {
                var bot = sim.AddBot((uint)(100 + Array.IndexOf(inputs, input)), BotCombatStateType.Combat);
                var target = BotTestFixture.MakeBot(9000, new Vector3(1, 0, 0));
                target.Hp = target.MaxHp = 100;
                bot.Bot.IsAutoAttack = true;
                bot.Runtime.CombatState.Target = target;

                var templateResolver = new Func<uint, SkillTemplate>(id => new SkillTemplate
                {
                    Id = id,
                    TargetType = SkillTargetType.Hostile,
                    TargetRelation = SkillTargetRelation.Hostile,
                    MinRange = 0,
                    MaxRange = 20
                });
                var manager = new BotRotationManager(_ => true, archetype => input.Item2 switch
                {
                    "Abolisher" => BotSkillIds.Abolisher.SkillLearnOrder,
                    "Reaper" => BotSkillIds.Reaper.SkillLearnOrder,
                    _ => BotSkillIds.Templar.SkillLearnOrder
                }, templateResolver);
                var path = BotTestFixture.FindRepoFile($"AAEmu.Game/Data/BotRotations/{input.Item1}.json");
                await Assert.That(manager.LoadRotations(File.ReadAllText(path), input.Item1)).IsTrue();

                var strategy = manager.Compile(input.Item1, bot.Runtime.Mover, () => 0, templateResolver,
                    _ => SkillResult.Success);
                await Assert.That(strategy).IsNotNull();
                var engine = bot.Runtime.Engines[(int)BotEngineKind.Combat];
                foreach (var action in strategy.Actions)
                    engine.RegisterAction(action);
                engine.RegisterAction(strategy.Filler);
                engine.AddStrategy(strategy);

                sim.Advance(1000);

                var rotationActionNames = strategy.Actions.Select(action => action.Name)
                    .Append(strategy.Filler.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                await Assert.That(engine.LastActionLog.Any(log => rotationActionNames.Contains(log.Action) &&
                    log.Result == BotActionResult.Success)).IsTrue();
            }
            finally
            {
                sim.Reset();
                BotTestFixture.ResetTaskManager();
            }
        }
    }

    private static Dictionary<uint, RotationSkillRow> LoadFixture()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.UnitTests/Bots/Rotations/Fixtures/skill-rows.json");
        var fixture = JsonConvert.DeserializeObject<RotationSkillFixture>(File.ReadAllText(path));
        return fixture.Rows.ToDictionary(row => row.Id);
    }
}
