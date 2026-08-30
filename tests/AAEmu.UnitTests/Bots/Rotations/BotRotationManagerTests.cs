using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class BotRotationManagerTests
{
    [Test]
    public async Task LoadRotations_ValidDocument_IsAvailableById()
    {
        var manager = NewManager();

        await Assert.That(manager.LoadRotations(Json("cast", "canCast", 11), "test.rotation")).IsTrue();
        await Assert.That(manager.GetRotation("test.rotation")?.Archetype).IsEqualTo("Test");
    }

    [Test]
    public async Task LoadRotations_ShippedClericSupportDocument_IsValid()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/cleric.support.json");
        var json = await File.ReadAllTextAsync(path);
        var definition = Newtonsoft.Json.JsonConvert.DeserializeObject<BotRotationDefinition>(json);
        var manager = new BotRotationManager(_ => true, _ => definition!.Skills.Values.ToArray());

        await Assert.That(manager.LoadRotations(json, "cleric.support")).IsTrue();
        await Assert.That(manager.LastErrors).IsEmpty();
        await Assert.That(manager.GetRotation("cleric.support")?.Meta.Role).IsEqualTo("support");
    }

    [Test]
    public async Task LoadRotations_UnknownActionKind_SkipsDocumentWithNamedError()
    {
        await AssertNamedError("UnknownActionKind", Json("notAnAction", "canCast", 11));
    }

    [Test]
    public async Task LoadRotations_UnknownTriggerKind_SkipsDocumentWithNamedError()
    {
        await AssertNamedError("UnknownTriggerKind", Json("cast", "notATrigger", 11));
    }

    [Test]
    public async Task LoadRotations_UnknownNestedTriggerKind_SkipsDocumentWithNamedError()
    {
        var json = Json("cast", "all", 11).Replace(
            "\"kind\": \"all\", \"skill\": \"strike\"",
            "\"kind\": \"all\", \"children\": [{ \"kind\": \"bogus\" }] ");

        await AssertNamedError("UnknownTriggerKind", json);
    }

    [Test]
    public async Task LoadRotations_UnknownSkillKey_SkipsDocumentWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace("\"skill\": \"strike\", \"rel\": 12", "\"skill\": \"missing\", \"rel\": 12");
        await AssertNamedError("UnknownSkillKey", json);
    }

    [Test]
    public async Task LoadRotations_UnknownHomeAnchorSkillKey_SkipsDocumentWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"range\": \"melee\"",
            "\"range\": \"melee\", \"homeAnchorSkill\": \"missing\"");

        await AssertNamedError("UnknownHomeAnchorSkillKey", json);
    }

    [Test]
    public async Task LoadRotations_ExplicitRowChannels_AreValidatedAndRetained()
    {
        var json = Json("castMelee", "canCast", 11).Replace(
            "\"as\": \"rule:strike\"",
            "\"as\": \"rule:strike\", \"ignoreGlobalDelay\": true, \"chain\": { \"id\": \"tripleSlash\", \"stage\": 1 }");
        var manager = NewManager();

        await Assert.That(manager.LoadRotations(json, "test.rotation")).IsTrue();
        var row = manager.GetRotation("test.rotation")!.Rules.Single().Then.Single();
        await Assert.That(row.IgnoreGlobalDelay).IsTrue();
        await Assert.That(row.Chain?.Id).IsEqualTo("tripleSlash");
        await Assert.That(row.Chain?.Stage).IsEqualTo(1);
    }

    [Test]
    public async Task LoadRotations_UnknownChainId_IsRejectedWithNamedError()
    {
        var json = Json("castMelee", "canCast", 11).Replace(
            "\"as\": \"rule:strike\"",
            "\"as\": \"rule:strike\", \"chain\": { \"id\": \"unknown\", \"stage\": 1 }");

        await AssertNamedError("UnknownChainId", json);
    }

    [Test]
    public async Task LoadRotations_InvalidChainStage_IsRejectedWithNamedError()
    {
        var json = Json("castMelee", "canCast", 11).Replace(
            "\"as\": \"rule:strike\"",
            "\"as\": \"rule:strike\", \"chain\": { \"id\": \"tripleSlash\", \"stage\": 3 }");

        await AssertNamedError("InvalidChainStage", json);
    }

    [Test]
    public async Task LoadRotations_IgnoreGlobalDelayOnNonMeleeCast_IsRejectedWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"as\": \"rule:strike\"",
            "\"as\": \"rule:strike\", \"ignoreGlobalDelay\": true");

        await AssertNamedError("InvalidIgnoreGlobalDelay", json);
    }

    [Test]
    public async Task LoadRotations_UnknownRowKey_IsRejectedWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"as\": \"rule:strike\"",
            "\"as\": \"rule:strike\", \"futureRowKey\": true");

        await AssertNamedError("UnknownRowKey", json);
    }

    [Test]
    public async Task LoadRotations_RangeRuleResolvesSkillFromThenRow()
    {
        var json = $$"""
        {
          "id": "range.rotation",
          "archetype": "Test",
          "meta": { "role": "damage", "range": "melee" },
          "skills": { "charge": 1 },
          "rules": [{ "when": { "kind": "range", "to": "target", "min": 4, "max": 15 },
                      "then": [{ "action": "reachAndCast", "skill": "charge", "rel": 31 }] }]
        }
        """;
        var manager = NewManager();

        await Assert.That(manager.LoadRotations(json, "range.rotation")).IsTrue();
        await Assert.That(manager.LastErrors).IsEmpty();
    }

    [Test]
    public async Task LoadRotations_RangeRuleWithoutSkill_IsRejected()
    {
        var json = $$"""
        {
          "id": "range.rotation",
          "archetype": "Test",
          "meta": { "role": "damage", "range": "melee" },
          "rules": [{ "when": { "kind": "range", "to": "target", "min": 4, "max": 15 },
                      "then": [{ "action": "move", "skill": "melee", "rel": 31 }] }]
        }
        """;
        var manager = NewManager();

        await Assert.That(manager.LoadRotations(json, "range.rotation")).IsFalse();
        await Assert.That(manager.LastErrors.Select(error => error.Code)).Contains("MissingRangeSkill");
    }

    [Test]
    public async Task LoadRotations_RangeRuleUnknownSkillArguments_AreRejected()
    {
        foreach (var argument in new[] { "skill", "spell", "opener" })
        {
            var json = $$"""
            {
              "id": "range.rotation",
              "archetype": "Test",
              "meta": { "role": "damage", "range": "melee" },
              "skills": { "strike": 1 },
              "rules": [{ "when": { "kind": "range", "{{argument}}": "nope", "min": 0, "max": 15 },
                          "then": [{ "action": "cast", "skill": "strike", "rel": 31 }] }]
            }
            """;

            await AssertNamedError("UnknownSkillKey", json);
        }
    }

    [Test]
    public async Task LoadRotations_RangeRuleMoveModesRemainAllowed()
    {
        foreach (var mode in new[] { "behind", "facing", "away" })
        {
            var json = $$"""
            {
              "id": "range.rotation",
              "archetype": "Test",
              "meta": { "role": "damage", "range": "melee" },
              "skills": { "strike": 1 },
              "rules": [{ "when": { "kind": "range", "skill": "{{mode}}", "min": 0, "max": 15 },
                          "then": [{ "action": "move", "skill": "{{mode}}", "rel": 31 }] }]
            }
            """;
            var manager = NewManager();

            await Assert.That(manager.LoadRotations(json, $"range.{mode}")).IsTrue();
            await Assert.That(manager.LastErrors).IsEmpty();
        }
    }

    [Test]
    public async Task LoadRotations_SkillIdNotInTemplates_SkipsDocumentWithNamedError()
    {
        var manager = new BotRotationManager(_ => false, _ => [1]);

        await Assert.That(manager.LoadRotations(Json("cast", "canCast", 11), "test.rotation")).IsFalse();
        await Assert.That(manager.LastErrors.Select(error => error.Code)).Contains("SkillIdNotInTemplates");
    }

    [Test]
    public async Task LoadRotations_RelevanceAtLowerBoundary_SkipsDocumentWithNamedError()
    {
        await AssertNamedError("RelevanceOutOfBand", Json("cast", "canCast", 10));
    }

    [Test]
    public async Task LoadRotations_RelevanceAtUpperBoundary_SkipsDocumentWithNamedError()
    {
        await AssertNamedError("RelevanceOutOfBand", Json("cast", "canCast", 99));
    }

    [Test]
    public async Task LoadRotations_DuplicateActionName_SkipsDocumentWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"then\": [{ \"action\": \"cast\", \"skill\": \"strike\", \"rel\": 12, \"as\": \"rule:strike\" }]",
            "\"then\": [{ \"action\": \"cast\", \"skill\": \"strike\", \"rel\": 11, \"as\": \"same\" }, { \"action\": \"cast\", \"skill\": \"strike\", \"rel\": 12, \"as\": \"same\" }]");
        await AssertNamedError("DuplicateActionName", json);
    }

    [Test]
    public async Task LoadRotations_DuplicateImplicitActionName_IsRejected()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"rules\": [{ \"when\": { \"kind\": \"canCast\", \"skill\": \"strike\" }, \"then\": [{ \"action\": \"cast\", \"skill\": \"strike\", \"rel\": 12, \"as\": \"rule:strike\" }] }]",
            "\"rules\": [{ \"when\": { \"kind\": \"canCast\", \"skill\": \"strike\" }, \"then\": [{ \"action\": \"cast\", \"skill\": \"strike\", \"rel\": 12 }] }]");

        await AssertNamedError("DuplicateActionName", json);
    }

    [Test]
    public async Task LoadRotations_LegacyNodesKey_IsRejectedAsUnknownTopLevelKey()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"rules\":",
            "\"nodes\": {},\n          \"rules\":");

        await AssertNamedError("UnknownTopLevelKey", json);
    }

    [Test]
    public async Task LoadRotations_ReplayKey_IsRejectedAsUnknownReplayKey()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"rules\":",
            "\"replay\": {},\n          \"rules\":");

        await AssertNamedError("UnknownReplayKey", json);
    }

    [Test]
    public async Task LoadRotations_UnknownTopLevelKey_IsRejectedWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"rules\":",
            "\"futureKey\": true,\n          \"rules\":");

        await AssertNamedError("UnknownTopLevelKey", json);
    }

    [Test]
    public async Task LoadRotations_EmptyThen_SkipsDocumentWithNamedError()
    {
        var json = Json("cast", "canCast", 11).Replace(
            "\"then\": [{ \"action\": \"cast\", \"skill\": \"strike\", \"rel\": 12, \"as\": \"rule:strike\" }]",
            "\"then\": []");
        await AssertNamedError("EmptyThen", json);
    }

    [Test]
    public async Task LoadRotations_SkillNotInArchetypeLearnOrder_SkipsDocumentWithNamedError()
    {
        var manager = new BotRotationManager(_ => true, _ => [2]);

        await Assert.That(manager.LoadRotations(Json("cast", "canCast", 11), "test.rotation")).IsFalse();
        await Assert.That(manager.LastErrors.Select(error => error.Code)).Contains("SkillNotInArchetypeLearnOrder");
    }

    [Test]
    public async Task LoadRotations_RelevanceBandAcceptsEveryInteriorBoundary()
    {
        foreach (var relevance in new[] { 11f, 11.99f, 12f, 29f, 30f, 34f, 40f, 49f, 88f, 91f })
        {
            var manager = NewManager();
            await Assert.That(manager.LoadRotations(Json("cast", "canCast", relevance), $"test.{relevance}")).IsTrue();
        }
    }

    private static BotRotationManager NewManager() => new(_ => true, _ => [1]);

    private static async Task AssertNamedError(string code, string json)
    {
        var manager = NewManager();

        await Assert.That(manager.LoadRotations(json, "test.rotation")).IsFalse();
        await Assert.That(manager.LastErrors.Select(error => error.Code)).Contains(code);
        await Assert.That(manager.GetRotation("test.rotation")).IsNull();
    }

    private static string Json(string action, string trigger, float relevance) => $$"""
        {
          "id": "test.rotation",
          "archetype": "Test",
          "meta": { "role": "damage", "range": "melee" },
          "skills": { "strike": 1 },
          "default": [{ "action": "{{action}}", "skill": "strike", "rel": {{relevance}}, "weight": 1 }],
          "rules": [{ "when": { "kind": "{{trigger}}", "skill": "strike" }, "then": [{ "action": "{{action}}", "skill": "strike", "rel": 12, "as": "rule:strike" }] }]
        }
        """;
}
