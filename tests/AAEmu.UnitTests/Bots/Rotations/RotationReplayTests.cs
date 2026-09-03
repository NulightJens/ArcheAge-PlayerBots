using System.Globalization;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Tasks.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class RotationReplayTests
{
    [Test]
    [Skip("legacy handlers deleted in R4c step 13")]
    public async Task Legacy_Darkrunner_Pve_MatchesGolden()
    {
    }

    [Test]
    [Skip("legacy handlers deleted in R4c step 13")]
    public async Task Legacy_Darkrunner_Pvp_MatchesGolden()
    {
    }

    [Test]
    [Skip("legacy handlers deleted in R4c step 13")]
    public async Task Legacy_Primeval_Pve_MatchesGolden()
    {
    }

    [Test]
    [Skip("legacy handlers deleted in R4c step 13")]
    public async Task Legacy_Primeval_Pvp_MatchesGolden()
    {
    }


    [Test]
    public async Task Rotation_Darkrunner_Pve_MatchesGolden()
    {
        await AssertTraceAsync("darkrunner", "pve", useCharacterTarget: false);
    }

    [Test]
    public async Task Rotation_Darkrunner_Pvp_MatchesGolden()
    {
        await AssertTraceAsync("darkrunner", "pvp", useCharacterTarget: true);
    }

    [Test]
    public async Task Rotation_Primeval_Pve_MatchesGolden()
    {
        await AssertTraceAsync("primeval", "pve", useCharacterTarget: false);
    }

    [Test]
    public async Task Rotation_Primeval_Pvp_MatchesGolden()
    {
        await AssertTraceAsync("primeval", "pvp", useCharacterTarget: true);
    }

    [Test]
    public async Task RotationReplay_RenamedDisplayNamesAndSkillKeys_MatchesGoldens()
    {
        foreach (var (rotation, mode, useCharacterTarget) in new[]
                 {
                     ("darkrunner", "pve", false), ("darkrunner", "pvp", true),
                     ("primeval", "pve", false), ("primeval", "pvp", true)
                 })
        {
            var rotationId = rotation == "darkrunner" ? "darkrunner.melee" : "primeval.archer";
            var path = BotTestFixture.FindRepoFile($"AAEmu.Game/Data/BotRotations/{rotationId}.json");
            var trace = RecordRotationTrace(rotation, useCharacterTarget, RenameRotationSchema(File.ReadAllText(path)));
            var goldenPath = FindGoldenPath($"AAEmu.UnitTests/Bots/Rotations/Goldens/{rotation}-legacy-{mode}.trace");

            await Assert.That(NormalizeLineEndings(trace))
                .IsEqualTo(NormalizeLineEndings(File.ReadAllText(goldenPath)));
        }
    }

    [Test]
    public async Task RotationReplay_DegenerateSingleFillerFixture_TripsWinnerBound()
    {
        var bot = BotTestFixture.MakeBot(902, Vector3.Zero);
        bot.IsAutoAttack = true;
        bot.Hp = bot.MaxHp = 100;
        var target = BotTestFixture.MakeBot(903, new Vector3(2, 0, 0));
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(),
            new BotCombatState { Target = target, IsComboLocked = true },
            config: new BotConfig { UseEngine = false });
        var definition = new BotRotationDefinition
        {
            Id = "degenerate.rotation",
            Archetype = "Test",
            Skills = new Dictionary<string, uint> { ["strike"] = 42 },
            Default = [new BotRotationRow
            {
                Action = "castMelee",
                Skill = "strike",
                Relevance = 11,
                Weight = 1,
                IgnoreGlobalDelay = true
            }]
        };
        bot.Skills = new CharacterSkills(bot);
        bot.Skills.Skills[42] = new Skill(new SkillTemplate { Id = 42 });
        var strategy = new BotRotationCompiler(
            templateResolver: _ => new SkillTemplate
            {
                Id = 42,
                MaxRange = 20,
                TargetType = SkillTargetType.Hostile,
                TargetRelation = SkillTargetRelation.Hostile
            },
            cast: _ => SkillResult.Success).Compile(definition);
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig { UseEngine = false },
            [strategy], strategy.Actions.Append(strategy.Filler));
        var allowedActionNames = strategy.Actions.Select(action => action.Name)
            .Append(strategy.Filler.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exception = Assert.Throws<InvalidOperationException>(() => RunWinnerBoundHarness(
            engine,
            tick => new BotContext(bot, runtime, runtime.Blackboard,
                TraceStart.AddMilliseconds(tick * 100), new BotConfig { UseEngine = false },
                BotEngineKind.Combat),
            allowedActionNames,
            120));
        await Assert.That(exception.Message).Contains("60 percent");
    }

    [Test]
    public async Task RotationAutoAttack_SimUsesFallbackWhenEveryCastIsUnavailable()
    {
        BotTestFixture.RegisterTaskManager();
        var sim = new BotSim(seed: 17);
        try
        {
            var simulated = sim.AddBot(1, BotCombatStateType.Combat);
            simulated.Bot.IsAutoAttack = true;
            var target = BotTestFixture.MakeBot(2, new Vector3(2, 0, 0));
            target.Hp = target.MaxHp = 100;
            simulated.Runtime.CombatState.Target = target;
            simulated.Runtime.CombatState.LastSkillTime = sim.Time.GetUtcNow().UtcDateTime.AddMilliseconds(-100);

            var templateResolver = new Func<uint, SkillTemplate>(id => new SkillTemplate
            {
                Id = id,
                MaxRange = 1,
                TargetType = SkillTargetType.Hostile,
                TargetRelation = SkillTargetRelation.Hostile
            });
            var definition = new BotRotationDefinition
            {
                Id = "autoattack.fallback",
                Archetype = "Test",
                Skills = new Dictionary<string, uint> { ["unavailable"] = 42 },
                Default =
                [
                    new BotRotationRow { Action = "castMelee", Skill = "unavailable", Relevance = 11 },
                    new BotRotationRow { Action = "autoAttack", Relevance = 11 }
                ],
                Rules =
                [
                    new BotRotationRule
                    {
                        When = new BotRotationWhen { Kind = "pvp" },
                        Then = [new BotRotationRow { Action = "castMelee", Skill = "unavailable", Relevance = 12 }]
                    }
                ]
            };
            var strategy = new BotRotationCompiler(templateResolver: templateResolver, roll: () => 0)
                .Compile(definition);
            var engine = new BotEngine(BotEngineKind.Combat, new BotConfig(), [strategy],
                strategy.Actions.Append(strategy.Filler));
            simulated.Runtime.Engines[(int)BotEngineKind.Combat] = engine;

            sim.Advance(200);

            await Assert.That(simulated.Runtime.Metrics.BrainSteps).IsGreaterThanOrEqualTo(1);
            await Assert.That(simulated.Runtime.Metrics.BrainSteps).IsLessThanOrEqualTo(2);
            await Assert.That(strategy.Filler.LastSelectedActionName).IsEqualTo("autoattack");
            await Assert.That(engine.LastActionLog.Any(log => log.Action == "filler" &&
                log.Result == BotActionResult.Success)).IsTrue();
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "r4e-step1-swing.log"),
                $"autoattack: {strategy.Filler.LastSelectedActionName} selected within " +
                $"{simulated.Runtime.Metrics.BrainSteps} brain tick(s){Environment.NewLine}");
        }
        finally
        {
            sim.Reset();
            BotTestFixture.ResetTaskManager();
        }
    }

    private static async Task AssertTraceAsync(string rotation, string mode, bool useCharacterTarget)
    {
        var trace = RecordRotationTrace(rotation, useCharacterTarget);
        var outputPath = Path.Combine(Path.GetTempPath(), $"r4c-{rotation}-legacy-{mode}-rotation.trace");
        File.WriteAllText(outputPath, trace);
        var goldenRelativePath = $"AAEmu.UnitTests/Bots/Rotations/Goldens/{rotation}-legacy-{mode}.trace";
        var goldenPath = FindGoldenPath(goldenRelativePath);
        await Assert.That(File.Exists(goldenPath)).IsTrue();
        await Assert.That(NormalizeLineEndings(trace))
            .IsEqualTo(NormalizeLineEndings(File.ReadAllText(goldenPath)));
    }

    private static string FindGoldenPath(string relativePath)
    {
        return BotTestFixture.FindRepoFile(relativePath);
    }

    private static string RecordTrace(string rotation, bool useCharacterTarget, bool useRotation)
    {
        throw new InvalidOperationException("Legacy replay handlers were deleted in R4c step 13");
    }


    private static string RecordRotationTrace(string rotation, bool useCharacterTarget)
    {
        var rotationId = rotation == "darkrunner" ? "darkrunner.melee" : "primeval.archer";
        var path = BotTestFixture.FindRepoFile($"AAEmu.Game/Data/BotRotations/{rotationId}.json");
        return RecordRotationTrace(rotation, useCharacterTarget, File.ReadAllText(path));
    }

    private static string RenameRotationSchema(string json)
    {
        var document = JObject.Parse(json);
        var skills = (JObject)document["skills"]!;
        var renames = skills.Properties()
            .Select((property, index) => (property.Name, NewName: $"a{index + 1}"))
            .ToDictionary(pair => pair.Name, pair => pair.NewName, StringComparer.OrdinalIgnoreCase);

        foreach (var value in document.Descendants().OfType<JValue>())
            if (value.Type == JTokenType.String && renames.TryGetValue(value.Value<string>()!, out var renamed))
                value.Value = renamed;
        var displayName = 1;
        foreach (var property in document.Descendants().OfType<JProperty>().Where(property => property.Name == "as"))
            property.Value = $"a{displayName++}";
        foreach (var property in skills.Properties().ToArray())
            property.Replace(new JProperty(renames[property.Name], property.Value));

        return document.ToString(Formatting.None);
    }

    private static string RecordRotationTrace(string rotation, bool useCharacterTarget, string rotationJson)
    {
        // These goldens isolate spell decisions while driving a scripted position through a NoopMover.
        // Physical home-anchor convergence is covered by TranscribedRotationTests instead.
        rotationJson = WithoutHomeAnchorForSpellDecisionReplay(rotationJson);
        var rotationId = rotation == "darkrunner" ? "darkrunner.melee" : "primeval.archer";
        BotTestFixture.RegisterTaskManager();
        BotTestFixture.SetPrivateField(ModelManager.Instance, "_modelTypes", new Dictionary<uint, ModelType>());
        var manager = new BotManager(_ => null, onlineLookup: _ => null);
        BotTestFixture.RegisterSingletons(manager);

        var bot = BotTestFixture.MakeBot(900, Vector3.Zero);
        bot.IsBot = true;
        bot.IsAutoAttack = true;
        bot.Skills = new CharacterSkills(bot);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);

        Unit target;
        if (useCharacterTarget)
        {
            var character = BotTestFixture.MakeBot(901, new Vector3(20, 0, 0));
            character.Hp = 100;
            character.MaxHp = 100;
            BotTestFixture.SetPrivateField(character, "_parentWorld", world);
            target = character;
        }
        else
        {
            var npc = new Npc
            {
                Id = 901,
                ObjId = 1901,
                Name = "npc901",
                Template = new NpcTemplate { Scale = 1f },
                Hp = 100,
                MaxHp = 100
            };
            npc.Transform.Local.SetPosition(new Vector3(20, 0, 0));
            BotTestFixture.SetPrivateField(npc, "_parentWorld", world);
            target = npc;
        }

        var state = new BotCombatState
        {
            BotId = bot.Id,
            Target = target,
            LastCombatTime = TraceStart
        };
        var runtime = new BotRuntime(bot, new BotMovementState(), state,
            config: new BotConfig { UseEngine = false });
        var events = new List<string>();
        var usedSkillIds = new HashSet<uint>();
        var templateResolver = new Func<uint, SkillTemplate>(id => new SkillTemplate
        {
            Id = id,
            TargetType = id == 13281 ? SkillTargetType.Pos : SkillTargetType.Hostile,
            TargetRelation = SkillTargetRelation.Hostile,
            MinRange = 0,
            MaxRange = 100,
            ManaCost = 0
        });
        foreach (var skillId in rotation == "darkrunner"
                     ? BotSkillIds.Darkrunner.SkillLearnOrder
                     : BotSkillIds.Primeval.SkillLearnOrder)
            bot.Skills.Skills[skillId] = new Skill(templateResolver(skillId));
        var rotationManager = new BotRotationManager(
            _ => true,
            archetype => archetype.Equals("Darkrunner", StringComparison.OrdinalIgnoreCase)
                ? BotSkillIds.Darkrunner.SkillLearnOrder
                : BotSkillIds.Primeval.SkillLearnOrder,
            templateResolver);
        if (!rotationManager.LoadRotations(rotationJson, rotationId))
            throw new InvalidOperationException("rotation fixture did not load");

        var strategy = rotationManager.Compile(
            rotationId,
            new NoopMover(),
            roll: () => 0,
            templateResolver,
            cast: request =>
            {
                var skillId = request.Skill.Id;
                events.Add($"cast:{skillId}");
                usedSkillIds.Add(skillId);
                bot.Cooldowns.AddCooldown(skillId, 1000000);
                return SkillResult.Success;
            });
        var config = new BotConfig { UseEngine = false };
        var engine = new BotEngine(BotEngineKind.Combat, config,
            [strategy],
            strategy.Actions.Append(strategy.Filler));
        var definition = rotationManager.GetRotation(rotationId);
        var allowedActionNames = definition.Default
            .Concat(definition.Rules.SelectMany(rule => rule.Then ?? []))
            .Select(DefaultRotationActionName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        allowedActionNames.Add(strategy.Filler.Name);
        allowedActionNames.UnionWith(strategy.Actions.Select(action => action.Name));
        var winningActionNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var castTicks = 0;
        var gateCalls = 0;
        var previousGateOverride = BotSkillGate.CheckOverride;
        BotSkillGate.CheckOverride = (gateBot, template, gateTarget, distance, now, gateConfig, castWhileControlled) =>
        {
            gateCalls++;
            return BotSkillGate.CheckCore(gateBot, template, gateTarget, distance, now, gateConfig, castWhileControlled);
        };
        var builder = new StringBuilder();

        try
        {
            for (var tick = 0; tick < 120; tick++)
            {
                bot.Transform.Local.SetPosition(new Vector3(MathF.Max(3f, 20f - tick * 0.25f), 0, 0));
                if (useCharacterTarget)
                    target.SkillTask = tick is >= 30 and <= 35 ? new TestSkillTask() : null;
                if (useCharacterTarget && tick == 110)
                    target.Transform.Local.SetPosition(new Vector3(100, 0, 0));

                events.Clear();
                var context = new BotContext(bot, runtime, runtime.Blackboard,
                    TraceStart.AddMilliseconds(tick * 100), config, BotEngineKind.Combat,
                    mover: new NoopMover());
                var result = engine.DoNextAction(context, minimal: false);
                var tickLogs = engine.LastActionLog
                    .Where(log => log.Time == context.Now)
                    .ToArray();
                if (events.Count > 0)
                {
                    castTicks++;
                    if (!tickLogs.Any(log => log.Result == BotActionResult.Success &&
                                             allowedActionNames.Contains(log.Action)))
                        throw new InvalidOperationException(
                            $"rotation cast at tick {tick} was not produced by a compiled rule or filler action");
                }
                var winner = tickLogs.LastOrDefault(log => log.Result == BotActionResult.Success &&
                                                            allowedActionNames.Contains(log.Action));
                if (winner.Action != null)
                    winningActionNames[winner.Action] = winningActionNames.GetValueOrDefault(winner.Action) + 1;
                var action = events.Count == 0 ? "none" : string.Join(',', events);
                builder.Append("tick=")
                    .Append(tick.ToString("D3", CultureInfo.InvariantCulture))
                    .Append(" result=")
                    .Append(result ? "true" : "false")
                    .Append(" action=")
                    .Append(action)
                    .Append(" windows=")
                    .Append(useCharacterTarget && tick is >= 30 and <= 35 ? "target-casting" : "")
                    .Append(useCharacterTarget && tick is >= 60 and <= 65 ? "cleanse" : "")
                    .Append(useCharacterTarget && tick >= 110 ? "leash" : "")
                    .Append(useCharacterTarget && (tick < 30 || tick > 35) && (tick < 60 || tick > 65) && tick < 110 ? "none" : "")
                    .Append(" state=")
                    .Append(state.CurrentState)
                    .Append(" combo=")
                    .Append(state.IsComboLocked ? "locked" : "open")
                    .Append(" stalking=")
                    .Append(state.IsStalking ? "true" : "false")
                    .AppendLine();
            }

            AssertWinningActionBound(winningActionNames);
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "r4e-step2-winner-counts.log"),
                $"trace={rotation}-{(useCharacterTarget ? "pvp" : "pve")}: " +
                string.Join(", ", winningActionNames.OrderBy(pair => pair.Key)
                    .Select(pair => $"{pair.Key}={pair.Value}")) + Environment.NewLine);
            if (gateCalls < castTicks)
                throw new InvalidOperationException($"rotation gate calls {gateCalls} were below cast lines {castTicks}");
            if (strategy.TriggerNodes.SelectMany(node => node.Actions)
                .Any(action => !IsInRelevanceBand(action.Relevance)))
                throw new InvalidOperationException("rotation compiler registered an action outside the relevance bands");

            return builder.ToString();
        }
        finally
        {
            BotSkillGate.CheckOverride = previousGateOverride;
            BotTestFixture.ResetTaskManager();
        }
    }

    private static string WithoutHomeAnchorForSpellDecisionReplay(string rotationJson)
    {
        var replayDefinition = JObject.Parse(rotationJson);
        if (replayDefinition["meta"] is JObject meta)
            meta.Remove("homeAnchorSkill");

        return replayDefinition.ToString(Formatting.None);
    }

    private static string NormalizeLineEndings(string value) => value.ReplaceLineEndings("\n");

    private static void RunWinnerBoundHarness(BotEngine engine, Func<int, BotContext> contextFactory,
        IReadOnlySet<string> allowedActionNames, int tickCount)
    {
        var winningActionNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var tick = 0; tick < tickCount; tick++)
        {
            var context = contextFactory(tick);
            engine.DoNextAction(context, minimal: false);
            var winner = engine.LastActionLog.LastOrDefault(log => log.Time == context.Now &&
                log.Result == BotActionResult.Success && allowedActionNames.Contains(log.Action));
            if (winner.Action != null)
                winningActionNames[winner.Action] = winningActionNames.GetValueOrDefault(winner.Action) + 1;
        }

        AssertWinningActionBound(winningActionNames);
    }

    private static void AssertWinningActionBound(IEnumerable<string> winningActions)
    {
        var winningActionNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in winningActions)
            if (!string.IsNullOrWhiteSpace(action))
                winningActionNames[action] = winningActionNames.GetValueOrDefault(action) + 1;

        AssertWinningActionBound(winningActionNames);
    }

    private static void AssertWinningActionBound(IReadOnlyDictionary<string, int> winningActionNames)
    {
        foreach (var pair in winningActionNames)
            if (pair.Value > 72)
                throw new InvalidOperationException(
                    $"rotation action '{pair.Key}' won {pair.Value} of 120 ticks, exceeding 60 percent");
    }

    private static string DefaultRotationActionName(BotRotationRow row)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Action))
            return null;
        if (!string.IsNullOrWhiteSpace(row.As))
            return row.As;
        if (row.Action.Equals("autoAttack", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(row.Skill) ? "autoattack" : $"autoattack:{row.Skill}";
        return string.IsNullOrWhiteSpace(row.Skill) ? row.Action : $"cast:{row.Skill}";
    }

    private static bool IsInRelevanceBand(float relevance) =>
        relevance is >= 11f and < 12f or >= 12f and <= 29f or >= 30f and <= 34f or
        >= 40f and <= 49f or 50f or >= 88f and <= 91f;

    private sealed class NoopMover : IBotMover
    {
        public void SetDestination(Character bot, Vector3 position, bool run, float tolerance) { }
        public void StopIfMoving(Character bot) { }
        public void StopImmediately(Character bot) { }
        public void Face(Character bot, float angle) { }
        public void Teleport(Character bot, Vector3 position) { }
        public void Follow(Character bot, Character target, float distance) { }
        public void StopFollow(Character bot) { }
        public void SendRelaxedStance(Character bot) { }
    }

    private static readonly DateTime TraceStart = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestSkillTask() : SkillTask(new Skill())
    {
        public override void Execute()
        {
        }
    }
}
