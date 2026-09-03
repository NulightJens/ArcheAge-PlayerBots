using System.Numerics;
using Newtonsoft.Json.Linq;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Content.Rotations.Triggers;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class TranscribedRotationTests
{
    [Test]
    public async Task DarkrunnerRotation_UsesTheRecordedFillerWeightsAndLadderRows()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);

        await Assert.That(rotation.Default).Count().IsEqualTo(6);
        await Assert.That(rotation.Default.Take(6).Select(row => row.Relevance).Distinct()).Count().IsEqualTo(1);
        await Assert.That(rotation.Default.Take(6).Select(row => row.Weight)).IsEquivalentTo([20f, 25f, 25f, 18f, 15f, 15f]);
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).Contains("comboActive");
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).Contains("chainStep");
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).Contains("hasCleansableDebuff");
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).DoesNotContain("targetCasting");
    }

    [Test]
    public async Task PrimevalRotation_UsesTheRecordedBuffDamageAndRangeLadder()
    {
        var rotation = Load("primeval.archer", BotSkillIds.Primeval.SkillLearnOrder);

        await Assert.That(rotation.Default).Count().IsEqualTo(1);
        await Assert.That(rotation.Default.Single().Skill).IsEqualTo("endlessArrows");
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).Contains("buffMissing");
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).Contains("range");
        await Assert.That(rotation.Rules.Select(rule => rule.When.Kind)).DoesNotContain("targetCasting");
        await Assert.That(rotation.Skills.Values).IsEquivalentTo(
            BotSkillIds.Primeval.SkillLearnOrder.Except([10694u, 10082u, 10648u]));
        await Assert.That(rotation.Rules.SelectMany(rule => rule.Then)
            .Any(row => string.Equals(row.As, "close:teleportation", StringComparison.OrdinalIgnoreCase))).IsFalse();
    }

    [Test]
    public async Task LiveArchetypePositioning_UsesTheExplicitHomeAnchorSkillTemplateRange()
    {
        var cases = new[]
        {
            (File: "darkrunner.melee", LearnOrder: BotSkillIds.Darkrunner.SkillLearnOrder,
                Skill: "tripleSlash1", SkillId: 18131u, Maximum: 4f),
            (File: "primeval.archer", LearnOrder: BotSkillIds.Primeval.SkillLearnOrder,
                Skill: "endlessArrows", SkillId: 14835u, Maximum: 20f),
            (File: "daggerspell.caster", LearnOrder: BotSkillIds.Daggerspell.SkillLearnOrder,
                Skill: "flamebolt", SkillId: 10752u, Maximum: 20f),
            (File: "cleric.support", LearnOrder: BotSkillIds.Cleric.SkillLearnOrder,
                Skill: "antithesis", SkillId: 10534u, Maximum: 25f)
        };
        var compiler = new BotRotationCompiler(templateResolver: id => new SkillTemplate
        {
            Id = id,
            MaxRange = (int)cases.Single(item => item.SkillId == id).Maximum
        });

        foreach (var item in cases)
        {
            var rotation = Load(item.File, item.LearnOrder);
            var strategy = compiler.Compile(rotation);
            var position = strategy.Actions.OfType<MaintainSpellRangeAction>().Single();

            await Assert.That(rotation.Meta.HomeAnchorSkill).IsEqualTo(item.Skill);
            await Assert.That(position.Name).IsEqualTo($"home-range:{item.Skill}");
            await Assert.That(position.MaximumRange).IsEqualTo(item.Maximum);
            await Assert.That(position.PreferredRange).IsEqualTo(item.Maximum - 1f);
            await Assert.That(strategy.TriggerNodes.Single(node =>
                node.Actions.Any(action => action.Name == position.Name)).Trigger.Name)
                .IsEqualTo($"outside-home-range:{item.Skill}");
            await Assert.That(rotation.Rules.SelectMany(rule => rule.Then)
                .Any(row => string.Equals(row.Action, "maintainRange", StringComparison.OrdinalIgnoreCase))).IsFalse();
        }
    }

    [Test]
    public async Task HomeAnchorTrigger_DoesNotCreateABasketInsideBandAndMovesInBothDirectionsOutsideIt()
    {
        var mover = new RecordingMover();
        var definition = new BotRotationDefinition
        {
            Id = "home.anchor.test",
            Archetype = "Test",
            Meta = new BotRotationMeta { Role = "damage", Range = "ranged", HomeAnchorSkill = "primary" },
            Skills = new Dictionary<string, uint> { ["primary"] = 1 }
        };
        var config = new BotConfig { UseEngine = false };
        var strategy = new BotRotationCompiler(
            templateResolver: id => new SkillTemplate { Id = id, MaxRange = 20 },
            mover: mover).Compile(definition);
        var position = strategy.Actions.OfType<MaintainSpellRangeAction>().Single();
        var homeTrigger = strategy.TriggerNodes.Single(node =>
            node.Actions.Any(action => action.Name == position.Name));
        var bot = BotTestFixture.MakeBot(790, Vector3.Zero);
        var target = BotTestFixture.MakeBot(791, new Vector3(19, 0, 0));
        bot.Hp = bot.MaxHp = target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target }, config: config);
        var metrics = new BotHostMetrics();
        runtime.HostMetrics = metrics;
        var context = new BotContext(bot, runtime, runtime.Blackboard,
            new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc), config, BotEngineKind.Combat, mover: mover);
        var engine = new BotEngine(BotEngineKind.Combat, config, [strategy],
            strategy.Actions.Append(strategy.Filler));

        await Assert.That(homeTrigger.Trigger.IsActive(context)).IsFalse();
        await Assert.That(engine.DoNextAction(context, false)).IsTrue();
        await Assert.That(metrics.Snapshot().ActionBasketsCreated).IsEqualTo(2L);
        await Assert.That(engine.SnapshotLog().Any(entry => entry.Action == position.Name)).IsFalse();

        target.Transform.Local.SetPosition(new Vector3(30, 0, 0));
        await Assert.That(homeTrigger.Trigger.IsActive(context)).IsTrue();
        await Assert.That(position.Execute(context, default)).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(mover.DestinationCount).IsEqualTo(1);

        target.Transform.Local.SetPosition(new Vector3(2, 0, 0));
        await Assert.That(homeTrigger.Trigger.IsActive(context)).IsTrue();
        await Assert.That(position.Execute(context, default)).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(mover.DestinationCount).IsEqualTo(2);
    }

    [Test]
    public async Task PrimevalHomeEnvelope_OwnsCombatAtEveryBandWithoutLegacyFallback()
    {
        var rotation = Load("primeval.archer", BotSkillIds.Primeval.SkillLearnOrder);
        var config = new BotConfig { UseEngine = false };

        foreach (var distance in new[] { 19f, 25f, 15f })
        {
            var mover = new RecordingMover();
            var legacy = new ProbeAction("legacy tick");
            var strategy = new BotRotationCompiler(
                roll: () => 0,
                templateResolver: id => new SkillTemplate
                {
                    Id = id,
                    MaxRange = 20,
                    TargetType = SkillTargetType.Hostile,
                    TargetRelation = SkillTargetRelation.Hostile
                },
                mover: mover,
                cast: _ => SkillResult.CooldownTime).Compile(rotation);
            var engine = new BotEngine(BotEngineKind.Combat, config,
                [strategy, new LegacyStrategy()],
                strategy.Actions.Append(strategy.Filler).Append(legacy));
            var bot = BotTestFixture.MakeBot((uint)(800 + distance), Vector3.Zero);
            var target = BotTestFixture.MakeBot((uint)(900 + distance), new Vector3(distance, 0, 0));
            bot.Hp = bot.MaxHp = target.Hp = target.MaxHp = 100;
            bot.CurrentTarget = target;
            bot.IsInBattle = true;
            var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
            {
                CurrentState = BotCombatStateType.Combat,
                Target = target
            }, config: config);
            var context = new BotContext(bot, runtime, runtime.Blackboard,
                new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc), config,
                BotEngineKind.Combat, mover: mover);

            await Assert.That(engine.DoNextAction(context, minimal: false)).IsTrue();
            await Assert.That(legacy.ExecuteCount).IsEqualTo(0);
            await Assert.That(engine.SnapshotLog().Any(entry =>
                entry.Action == "legacy tick" && entry.Result == BotActionResult.Success)).IsFalse();
            if (distance != 19f)
            {
                await Assert.That(mover.Destinations).Count().IsEqualTo(1);
                await Assert.That(Vector3.Distance(mover.Destinations[0], target.Transform.World.Position))
                    .IsEqualTo(19f).Within(0.001f);
                await Assert.That(Vector3.Distance(mover.Destinations[0], target.Transform.World.Position))
                    .IsGreaterThan(5f);
            }
        }
    }

    [Test]
    public async Task PrimevalHomeEnvelope_SetsCloseWaypointThenAllowsReadyRangedCast()
    {
        var previousGate = BotSkillGate.CheckOverride;
        BotSkillGate.CheckOverride = (_, _, _, _, _, _, _) => new GateResult(GateReason.Ok);
        try
        {
            var mover = new RecordingMover();
            var legacy = new ProbeAction("legacy tick");
            var casts = new List<uint>();
            var definition = new BotRotationDefinition
            {
                Id = "primeval.archer",
                Archetype = "Primeval",
                Meta = new BotRotationMeta
                {
                    Role = "damage",
                    Range = "ranged",
                    HomeAnchorSkill = "endlessArrows"
                },
                Skills = new Dictionary<string, uint> { ["endlessArrows"] = 14835 },
                Default =
                [
                    new BotRotationRow
                    {
                        Action = "cast", Skill = "endlessArrows", Relevance = 11
                    }
                ]
            };
            var config = new BotConfig { UseEngine = false, GlobalSkillDelayMs = 0 };
            var strategy = new BotRotationCompiler(
                roll: () => 0,
                templateResolver: id => new SkillTemplate
                {
                    Id = id,
                    MaxRange = 20,
                    TargetType = SkillTargetType.Hostile,
                    TargetRelation = SkillTargetRelation.Hostile
                },
                mover: mover,
                cast: request =>
                {
                    casts.Add(request.Skill.Id);
                    return SkillResult.Success;
                }).Compile(definition);
            var engine = new BotEngine(BotEngineKind.Combat, config,
                [strategy, new LegacyStrategy()],
                strategy.Actions.Append(strategy.Filler).Append(legacy));
            var bot = BotTestFixture.MakeBot(850, Vector3.Zero);
            var target = BotTestFixture.MakeBot(851, new Vector3(15, 0, 0));
            bot.Hp = bot.MaxHp = target.Hp = target.MaxHp = 100;
            bot.CurrentTarget = target;
            bot.IsInBattle = true;
            bot.Skills = new CharacterSkills(bot);
            bot.Skills.Skills[14835] = new Skill(new SkillTemplate { Id = 14835 });
            var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
            {
                CurrentState = BotCombatStateType.Combat,
                Target = target
            }, config: config);
            var context = new BotContext(bot, runtime, runtime.Blackboard,
                new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc), config,
                BotEngineKind.Combat, mover: mover);

            await Assert.That(engine.DoNextAction(context, minimal: false)).IsTrue();
            await Assert.That(mover.Destinations).Count().IsEqualTo(1);
            await Assert.That(casts).IsEquivalentTo([14835u]);
            await Assert.That(legacy.ExecuteCount).IsEqualTo(0);
            await Assert.That(engine.SnapshotLog().Any(entry => entry.Action == "home-range:endlessArrows" &&
                entry.Result == BotActionResult.Impossible)).IsTrue();
            await Assert.That(engine.SnapshotLog().Any(entry => entry.Action == "filler" &&
                entry.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            BotSkillGate.CheckOverride = previousGate;
        }
    }

    [Test]
    public async Task DarkrunnerPrimaryRangeRule_UsesTheActualTripleSlashMaximum()
    {
        var darkrunner = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var range = darkrunner.Rules
            .SelectMany(rule => rule.When.Children ?? [])
            .Single(child => child.Kind == "range" &&
                             child.Arguments.TryGetValue("skill", out var skill) &&
                             skill.Value<string>() == "tripleSlash1");

        await Assert.That(range.Arguments["max"].Value<float>()).IsEqualTo(4f);
    }

    [Test]
    public async Task ShippedPlayerbotRotations_DoNotContainAutoAttackRows()
    {
        var anchor = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/daggerspell.caster.json");
        var directory = Path.GetDirectoryName(anchor);

        foreach (var path in Directory.EnumerateFiles(directory!, "*.json", SearchOption.TopDirectoryOnly))
        {
            var document = JObject.Parse(File.ReadAllText(path));
            var rows = (document["default"]?.Children() ?? Enumerable.Empty<JToken>())
                .Concat(document["rules"]?.Children()
                    .SelectMany(rule => rule["then"]?.Children() ?? Enumerable.Empty<JToken>()) ?? []);

            await Assert.That(rows.Any(row => string.Equals(row["action"]?.Value<string>(), "autoAttack",
                StringComparison.OrdinalIgnoreCase))).IsFalse();
        }
    }

    [Test]
    public async Task ComboAndChainRules_ReadTheLegacyStateWithoutChangingTheQueue()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var definition = rotation.Rules.First(rule => rule.When.Kind == "comboActive");
        var factory = new RotationTriggerFactory(_ => 11918, _ => new SkillTemplate { Id = 11918, MaxRange = 4 });
        var trigger = factory.Create(definition.When);
        var bot = BotTestFixture.MakeBot(700, Vector3.Zero);
        bot.Hp = bot.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
        {
            IsComboLocked = true,
            LastComboSkill = 11918
        },
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, DateTime.UtcNow,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);

        await Assert.That(trigger.IsActive(context)).IsTrue();
        runtime.CombatState.IsComboLocked = false;
        await Assert.That(trigger.IsActive(context)).IsFalse();
    }

    [Test]
    public async Task TripleSlashChainRule_RequiresTheRecordedSecondOrThirdStage()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var definition = rotation.Rules.First(rule => rule.When.Kind == "chainStep");
        var factory = new RotationTriggerFactory(_ => 18131, _ => new SkillTemplate { Id = 18131, MaxRange = 4 });
        var trigger = factory.Create(definition.When);
        var bot = BotTestFixture.MakeBot(701, Vector3.Zero);
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState(),
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, DateTime.UtcNow,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);

        runtime.CombatState.TripleSlashStage = 1;
        runtime.CombatState.LastTripleSlashTime = context.Now.AddMilliseconds(-800);
        await Assert.That(trigger.IsActive(context)).IsTrue();
        runtime.CombatState.LastTripleSlashTime = context.Now.AddMilliseconds(-799);
        await Assert.That(trigger.IsActive(context)).IsFalse();
        runtime.CombatState.TripleSlashStage = 0;
        await Assert.That(trigger.IsActive(context)).IsFalse();
    }

    [Test]
    public async Task TranscribedRotations_KeepAllLearnOrderIdsAndTheSixteenAndEighteenSkillAllowlists()
    {
        var darkrunner = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var primeval = Load("primeval.archer", BotSkillIds.Primeval.SkillLearnOrder);

        await Assert.That(darkrunner.Skills).Count().IsEqualTo(21);
        await Assert.That(primeval.Skills).Count().IsEqualTo(17);
        await Assert.That(darkrunner.Skills.Values).IsEquivalentTo(
            BotSkillIds.Darkrunner.SkillLearnOrder.Except([10152u, 10082u]));
        await Assert.That(primeval.Skills.Values).IsEquivalentTo(
            BotSkillIds.Primeval.SkillLearnOrder.Except([10694u, 10082u, 10648u]));
    }

    [Test]
    public async Task TranscribedRotations_ExposeTheCommittedGoldenTraceShape()
    {
        foreach (var name in new[]
                 {
                     "darkrunner-legacy-pve.trace", "darkrunner-legacy-pvp.trace",
                     "primeval-legacy-pve.trace", "primeval-legacy-pvp.trace"
                 })
        {
            var path = BotTestFixture.FindRepoFile($"AAEmu.UnitTests/Bots/Rotations/Goldens/{name}");
            await Assert.That(File.ReadAllLines(path)).Count().IsEqualTo(120);
        }
    }

    [Test]
    public async Task DarkrunnerRotation_DropsStalkerLadderAndKeepsShadowStepRow()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var compiler = new BotRotationCompiler(
            roll: () => 0,
            templateResolver: id => new SkillTemplate { Id = id, MaxRange = 20 });
        var strategy = compiler.Compile(rotation);

        await Assert.That(strategy.Actions.Select(action => action.Name)).DoesNotContain("stalker:fsm");
        await Assert.That(rotation.Rules.SelectMany(rule => rule.Then)
            .Any(row => string.Equals(row.Skill, "shadowStep", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(strategy.TriggerNodes.SelectMany(node => node.Actions)
            .Any(action => action.Name == "stalker:fsm")).IsFalse();
    }

    [Test]
    public async Task DarkrunnerSkillsAreRowsOrCommittedDroppedEntries()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/darkrunner.melee.json");
        var document = JObject.Parse(File.ReadAllText(path));
        var rowSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in document["default"]?.Children() ?? Enumerable.Empty<JToken>())
            AddSkill(row, rowSkills);
        foreach (var rule in document["rules"]?.Children() ?? Enumerable.Empty<JToken>())
            foreach (var row in rule["then"]?.Children() ?? Enumerable.Empty<JToken>())
                AddSkill(row, rowSkills);

        var committedDroppedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dropBack"
        };
        foreach (var property in document["skills"]?.Children().Cast<JProperty>() ?? Enumerable.Empty<JProperty>())
            if (!rowSkills.Contains(property.Name))
                await Assert.That(committedDroppedEntries).Contains(property.Name);

        static void AddSkill(JToken row, HashSet<string> skills)
        {
            var skill = row["skill"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(skill))
                skills.Add(skill);
        }
    }

    [Test]
    public async Task DarkrunnerStalkerLadderSourceIsDropped()
    {
        var relativePath = Path.Combine("AAEmu.Game", "Bots", "Content", "Rotations", "StalkerRotationActions.cs");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var exists = false;
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, relativePath)))
            {
                exists = true;
                break;
            }

            directory = directory.Parent;
        }

        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task DarkrunnerRotation_DataDrivenShadowStepUsesCommittedRelevances()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        var compiler = new BotRotationCompiler(
            roll: () => 0,
            templateResolver: id => new SkillTemplate { Id = id, MaxRange = 20 });
        var strategy = compiler.Compile(rotation);

        await Assert.That(rotation.Rules.SelectMany(rule => rule.Then)
            .Single(row => row.As == "combo:shadowStep").Relevance).IsEqualTo(24f);
        await Assert.That(rotation.Rules.SelectMany(rule => rule.Then)
            .Single(row => row.As == "combo:precisionStrike").Relevance).IsEqualTo(47f);
    }

    [Test]
    public async Task RangeRule_ReachAndCastQueuesReachPrerequisiteOnCombatEngineTick()
    {
        var bot = BotTestFixture.MakeBot(710, Vector3.Zero);
        var target = BotTestFixture.MakeBot(711, new Vector3(20, 0, 0));
        bot.Hp = bot.MaxHp = 100;
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        var mover = new RecordingMover();
        var definition = new BotRotationDefinition
        {
            Id = "range.test",
            Archetype = "Test",
            Skills = new Dictionary<string, uint> { ["charge"] = 42 },
            Rules =
            [
                new BotRotationRule
                {
                    When = new BotRotationWhen
                    {
                        Kind = "range",
                        Arguments = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                        {
                            ["min"] = Newtonsoft.Json.Linq.JToken.FromObject(5f),
                            ["max"] = Newtonsoft.Json.Linq.JToken.FromObject(25f)
                        }
                    },
                    Then = [new BotRotationRow { Action = "reachAndCast", Skill = "charge", Relevance = 31 }]
                }
            ]
        };
        var strategy = new BotRotationCompiler(
            templateResolver: _ => new SkillTemplate
            {
                Id = 42,
                MinRange = 0,
                MaxRange = 5,
                TargetType = SkillTargetType.Hostile,
                TargetRelation = SkillTargetRelation.Hostile
            },
            mover: mover).Compile(definition);
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig { UseEngine = false },
            [strategy], strategy.Actions.Append(strategy.Filler));
        var context = new BotContext(bot, runtime, runtime.Blackboard, DateTime.UtcNow,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat, mover: mover);

        await Assert.That(strategy.TriggerNodes.Single().Trigger.IsActive(context)).IsTrue();
        var result = engine.DoNextAction(context, minimal: false);
        await Assert.That(engine.PushCount).IsGreaterThan(0);
        await Assert.That(engine.LastActionLog).IsNotEmpty();
        await Assert.That(result).IsTrue();
        await Assert.That(mover.DestinationSet).IsTrue();
        await Assert.That(strategy.Actions.Select(action => action.Name)).Contains("reach:charge");
    }

    [Test]
    public async Task DarkrunnerRotation_ComboRulesWinOverFiller()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        foreach (var (opener, followUp) in new[]
                 {
                     (11918u, 13282u),
                     (10648u, 10496u)
                 })
        {
            var (bot, runtime, target, now) = CombatFixture(opener, followUp);
            var events = new List<uint>();
            var strategy = new BotRotationCompiler(
                templateResolver: id => new SkillTemplate
                {
                    Id = id,
                    MaxRange = 100,
                    TargetType = SkillTargetType.Hostile,
                    TargetRelation = SkillTargetRelation.Hostile
                },
                cast: request =>
                {
                    events.Add(request.Skill.Id);
                    return SkillResult.Success;
                }).Compile(rotation);
            var engine = new BotEngine(BotEngineKind.Combat, new BotConfig { UseEngine = false },
                [strategy], strategy.Actions.Append(strategy.Filler));
            var context = new BotContext(bot, runtime, runtime.Blackboard, now,
                new BotConfig { UseEngine = false }, BotEngineKind.Combat);
            await Assert.That(engine.DoNextAction(context, minimal: false)).IsTrue();
            await Assert.That(events).Contains(followUp);
        }
    }

    [Test]
    public async Task DarkrunnerRotation_ChainStepRowsWinAtBothRecordedStages()
    {
        var rotation = Load("darkrunner.melee", BotSkillIds.Darkrunner.SkillLearnOrder);
        foreach (var (stage, expected) in new[]
                 {
                     (1, 18132u),
                     (2, 18134u)
                 })
        {
            var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
            var bot = BotTestFixture.MakeBot((uint)(720 + stage), new Vector3(10, 0, 0));
            var target = BotTestFixture.MakeBot((uint)(730 + stage), new Vector3(20, 0, 0));
            bot.Hp = bot.MaxHp = 100;
            target.Hp = target.MaxHp = 100;
            var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
            {
                Target = target,
                TripleSlashStage = stage,
                LastTripleSlashTime = now.AddMilliseconds(-800)
            }, config: new BotConfig { UseEngine = false });
            var events = new List<uint>();
            var strategy = new BotRotationCompiler(
                templateResolver: id => new SkillTemplate
                {
                    Id = id,
                    MaxRange = 100,
                    TargetType = SkillTargetType.Hostile,
                    TargetRelation = SkillTargetRelation.Hostile
                },
                cast: request =>
                {
                    events.Add(request.Skill.Id);
                    return SkillResult.Success;
                }).Compile(rotation);
            var engine = new BotEngine(BotEngineKind.Combat, new BotConfig { UseEngine = false },
                [strategy], strategy.Actions.Append(strategy.Filler));
            var context = new BotContext(bot, runtime, runtime.Blackboard, now,
                new BotConfig { UseEngine = false }, BotEngineKind.Combat);
            bot.Skills = new CharacterSkills(bot);
            bot.Skills.Skills[expected] = new Skill(new SkillTemplate { Id = expected });

            await Assert.That(engine.DoNextAction(context, minimal: false)).IsTrue();
            await Assert.That(events).Contains(expected);
        }
    }

    [Test]
    public async Task PrimevalDefaultIsOnlyTheEndlessArrowsBaseline()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/primeval.archer.json");
        var document = JObject.Parse(File.ReadAllText(path));
        var defaults = document["default"]?.Children().ToArray() ?? [];

        await Assert.That(defaults).Count().IsEqualTo(1);
        await Assert.That(defaults[0]["skill"]?.Value<string>()).IsEqualTo("endlessArrows");
        await Assert.That(defaults[0]["weight"]).IsNull();
    }

    [Test]
    public async Task PrimevalSpecialArrowsShareOneFourSecondThrottle()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/primeval.archer.json");
        var document = JObject.Parse(File.ReadAllText(path));
        var specialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "damage:toxicShot", "damage:piercingShot", "damage:chargedBolt", "damage:concussiveArrow"
        };
        var specialRules = document["rules"]?.Children()
            .Where(rule => rule["then"]?.Children()
                .Any(row => specialNames.Contains(row["as"]?.Value<string>() ?? string.Empty)) == true)
            .ToArray() ?? [];

        await Assert.That(specialRules).Count().IsEqualTo(4);
        foreach (var rule in specialRules)
        {
            var throttle = rule["when"]?["children"]?.Children()
                .Single(child => string.Equals(child["kind"]?.Value<string>(), "groupCooldown",
                    StringComparison.OrdinalIgnoreCase));
            await Assert.That(throttle?["group"]?.Value<string>()).IsEqualTo("primevalSpecialArrow");
            await Assert.That(throttle?["ms"]?.Value<int>()).IsEqualTo(4000);
        }
    }

    [Test]
    public async Task DaggerspellSpecialDamageSharesOneFourSecondThrottle()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/daggerspell.caster.json");
        var document = JObject.Parse(File.ReadAllText(path));
        var specialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "damage:meteorStrike", "damage:arcLightning", "damage:freezingArrow", "damage:chainLightning"
        };
        var specialRules = document["rules"]?.Children()
            .Where(rule => rule["then"]?.Children()
                .Any(row => specialNames.Contains(row["as"]?.Value<string>() ?? string.Empty)) == true)
            .ToArray() ?? [];

        await Assert.That(specialRules).Count().IsEqualTo(4);
        foreach (var rule in specialRules)
        {
            var throttle = rule["when"]?["children"]?.Children()
                .Single(child => string.Equals(child["kind"]?.Value<string>(), "groupCooldown",
                    StringComparison.OrdinalIgnoreCase));
            await Assert.That(throttle?["group"]?.Value<string>()).IsEqualTo("daggerspellSpecialDamage");
            await Assert.That(throttle?["ms"]?.Value<int>()).IsEqualTo(4000);
        }
    }

    [Test]
    public async Task PrimevalHomeEnvelope_ReplacesTheLegacyFleeRows()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/primeval.archer.json");
        var document = JObject.Parse(File.ReadAllText(path));
        var movementAliases = document["rules"]?.Children()
            .SelectMany(rule => rule["then"]?.Children() ?? Enumerable.Empty<JToken>())
            .Where(row => string.Equals(row["action"]?.Value<string>(), "move", StringComparison.OrdinalIgnoreCase))
            .Select(row => row["as"]?.Value<string>())
            .Where(alias => alias != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        await Assert.That(movementAliases).IsEmpty();
    }

    private static (Character Bot, BotRuntime Runtime, Character Target, DateTime Now) CombatFixture(
        uint opener, uint followUp)
    {
        var now = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var bot = BotTestFixture.MakeBot(714, new Vector3(10, 0, 0));
        var target = BotTestFixture.MakeBot(715, new Vector3(20, 0, 0));
        bot.Hp = bot.MaxHp = 100;
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
        {
            Target = target,
            IsComboLocked = true,
            LastComboSkill = opener,
            PendingComboFollowUp = followUp
        }, config: new BotConfig { UseEngine = false });
        bot.Skills = new CharacterSkills(bot);
        bot.Skills.Skills[opener] = new Skill(new SkillTemplate { Id = opener });
        bot.Skills.Skills[followUp] = new Skill(new SkillTemplate { Id = followUp });
        return (bot, runtime, target, now);
    }

    private sealed class RecordingMover : IBotMover
    {
        public bool DestinationSet { get; private set; }
        public int DestinationCount { get; private set; }
        public List<Vector3> Destinations { get; } = [];
        public void SetDestination(Character bot, Vector3 position, bool run, float tolerance)
        {
            DestinationSet = true;
            DestinationCount++;
            Destinations.Add(position);
        }
        public void StopIfMoving(Character bot) { }
        public void StopImmediately(Character bot) { }
        public void Face(Character bot, float angle) { }
        public void Teleport(Character bot, Vector3 position) { }
        public void Follow(Character bot, Character target, float distance) { }
        public void StopFollow(Character bot) { }
        public void SendRelaxedStance(Character bot) { }
    }

    private sealed class ProbeAction(string name) : IBotAction
    {
        public string Name { get; } = name;
        public int ExecuteCount { get; private set; }
        public bool IsUseful(BotContext context) => true;
        public bool IsPossible(BotContext context) => true;
        public BotActionResult Execute(BotContext context, BotEvent ev)
        {
            ExecuteCount++;
            return BotActionResult.Success;
        }
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
