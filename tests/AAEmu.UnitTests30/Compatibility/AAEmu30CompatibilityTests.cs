using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using AAEmu.Commons.Network;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Services.WebApi.Controllers;
using NetCoreServer;
using Xunit;

namespace AAEmu.UnitTests.PlayerBots.Compatibility;

public sealed class AAEmu30CompatibilityTests
{
    [Fact]
    public void HumanClassAndGearCommandsRemainAvailableOnThe30Adapter()
    {
        var setClass = new SetBotClass();
        var gear = new BotGearCommand();

        Assert.Contains("setclass", setClass.CommandNames);
        Assert.Contains("botsetclass", setClass.CommandNames);
        Assert.Equal("Battlerage", SetBotClass.TreeName(AbilityType.Fight));
        Assert.Equal("Auramancy", SetBotClass.TreeName(AbilityType.Will));
        Assert.Contains("botgear", gear.CommandNames);
        Assert.Contains("botequip", gear.CommandNames);
        Assert.Contains("create <grade> <prefix> <armor> <weapon>", gear.GetCommandLineHelp());
        Assert.Equal("Mainhand", BotGearCommand.EquipmentSlotName(new EquipItem { Slot = 15 }));
        Assert.True(BotGearCatalog.TryParseGrade("celestial", out var grade));
        Assert.Equal(ItemGrade.Celestial, grade);
        Assert.Equal("gale", BotGearCatalog.NormalizeProfile("wind"));
    }

    [Fact]
    public void QuestDiscoveryCommandsRemainStagedAndBoundedOnThe30Adapter()
    {
        var command = new BotQuestCommand();

        Assert.Contains("botquest", command.CommandNames);
        Assert.True(BotQuestCommand.TryParse(["scan", "2", "35"], out var scan));
        Assert.Equal(BotQuestVerb.Scan, scan.Verb);
        Assert.Equal(35f, scan.Radius);
        Assert.True(BotQuestCommand.TryParse(["inspect", "2", "330"], out var inspect));
        Assert.Equal(BotQuestVerb.Inspect, inspect.Verb);
        Assert.Equal(330u, inspect.QuestId);
        Assert.True(BotQuestCommand.TryParse(["status", "2", "330"], out var status));
        Assert.Equal(BotQuestVerb.Status, status.Verb);
        Assert.True(BotQuestCommand.TryParse(["report", "2", "330"], out var report));
        Assert.Equal(BotQuestVerb.Report, report.Verb);
        Assert.Equal(0, report.SelectedReward);
        Assert.False(BotQuestCommand.TryParse(["scan", "2", "100.1"], out _));
        Assert.True(BotQuestCommand.IsValidSelectedReward([], 0));
        Assert.False(BotQuestCommand.IsValidSelectedReward([], 1));
        Assert.False(BotQuestCommand.IsValidSelectedReward([1, 2], 0));
        Assert.True(BotQuestCommand.IsValidSelectedReward([1, 2], 2));
    }

    [Fact]
    public void QuestInspectionExposesExact30FixtureTargetsWithoutMutatingQuestState()
    {
        var component = new QuestComponentTemplate(new QuestTemplate());

        Assert.Equal(
            "QuestActObjMonsterGroupHunt npc_group=72 highlight_doodad=91 count=4",
            BotQuestCommand.DescribeAct(new QuestActObjMonsterGroupHunt(component)
            {
                QuestMonsterGroupId = 72,
                HighlightDoodadId = 91,
                Count = 4
            }));
        Assert.Equal(
            "QuestActObjInteraction doodad=93 world_interaction=Looting highlight_doodad=94 count=1",
            BotQuestCommand.DescribeAct(new QuestActObjInteraction(component)
            {
                DoodadId = 93,
                WorldInteractionId = WorldInteractionType.Looting,
                HighlightDoodadId = 94,
                Count = 1
            }));
    }

    [Fact]
    public void BotEquipmentVisibilityUsesThe30PublicInspectionPacket()
    {
        const uint objectId = 0x10203;
        var packet = new SCUnitOpenEquipInfoPacket(objectId, BotEquipmentVisibility.IsPublic);
        var stream = new PacketStream();

        packet.Write(stream);
        stream.Pos = 0;

        Assert.Equal(objectId, stream.ReadBc());
        Assert.True(stream.ReadBoolean());
        Assert.False(stream.HasBytes);
    }

    [Fact]
    public void BotGearCreateRefreshUsesTheNormalDespawnSpawnLifecycle()
    {
        var calls = new List<string>();
        var refreshed = new TestCharacter { Id = 2 };

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

        Assert.True(despawned);
        Assert.Same(refreshed, result);
        Assert.Equal(["despawn:2", "spawn:2"], calls);
    }

    [Fact]
    public void RestoreSavedHpMpClampsPersistedValuesToTheCurrentMaximums()
    {
        var character = new TestCharacter
        {
            MaxHp = 100,
            Hp = 130,
            MaxMp = 80,
            Mp = -5
        };

        character.RestoreSavedHpMp();

        Assert.Equal(100, character.Hp);
        Assert.Equal(0, character.Mp);
    }

    [Fact]
    public void RestoreSavedHpMpUsesValuesCapturedBeforeEarlyHostClamping()
    {
        var character = new TestCharacter
        {
            MaxHp = 370,
            Hp = 720,
            MaxMp = 320,
            Mp = 670
        };

        character.CaptureSavedHpMpForBotLoad();
        character.Hp = character.MaxHp;
        character.Mp = character.MaxMp;
        character.MaxHp = 720;
        character.MaxMp = 670;

        character.RestoreSavedHpMp();

        Assert.Equal(720, character.Hp);
        Assert.Equal(670, character.Mp);
    }

    [Fact]
    public void ClearAllAggroAlsoLeavesTheConnectionlessCharacterOutOfBattle()
    {
        var character = new Character(new UnitCustomModelParams())
        {
            IsInBattle = true
        };

        character.ClearAllAggro();

        Assert.Empty(character.AggroTable);
        Assert.False(character.IsInBattle);
    }

    [Fact]
    public void LethalUpdateAttributesTheKillToTheAttackerAndVictim()
    {
        var attacker = new NonDyingUnit();
        var victim = new NonDyingUnit { Hp = 0 };
        object? sender = null;
        OnKillArgs? captured = null;
        attacker.Events.OnKill += (eventSender, args) =>
        {
            sender = eventSender;
            captured = args;
        };

        victim.PostUpdateCurrentHp(attacker, 1, 0);

        Assert.Same(victim, sender);
        Assert.NotNull(captured);
        Assert.Same(attacker, captured.Killer);
        Assert.Same(victim, captured.Target);
        Assert.Same(victim, captured.Victim);
    }

    [Fact]
    public void ServerTickMetricsExposeAConservativeMaximum()
    {
        var metrics = new ServerTickMetrics();

        metrics.RecordTick(0.0173, TickManager.TickSleepMilliseconds);
        var snapshot = metrics.Snapshot();

        Assert.Equal(1, snapshot.Work.Count);
        Assert.True(snapshot.Work.MaxMs >= 0.018);
        Assert.True(snapshot.Work.MaxMs >= 0.0173);
    }

    [Fact]
    public void SyntheticSystemActorKeepsItsAdministrativeAccessWithoutAnAccount()
    {
        var actor = SystemActor.Create();

        var accessLevel = new CharacterManager().GetEffectiveAccessLevel(actor);

        Assert.Equal(100, accessLevel);
        Assert.Equal(0UL, actor.AccountId);
    }

    [Fact]
    public void CommandControllerAcceptsSyntheticSystemActorWithoutWorldRegistration()
    {
        CommandManager.Instance.Clear();
        var request = new HttpRequest("POST", "/api/commands/botmetrics", "HTTP/1.1");
        request.SetBody(JsonSerializer.Serialize(new
        {
            character = SystemActor.ActorName,
            arguments = "snapshot"
        }));
        var matches = Regex.Matches(request.Url, "/api/commands/([^/]+)");

        var response = new CommandController().ExecuteCommand(request, matches);

        Assert.Equal(200, response.Status);
        Assert.DoesNotContain("not found", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NonDyingUnit : Unit
    {
        public override void DoDie(BaseUnit killer, KillReason killReason)
        {
        }
    }

    private sealed class TestCharacter : Character
    {
        public TestCharacter()
            : base(new UnitCustomModelParams())
        {
        }

        public override int MaxHp { get; set; }
        public override int MaxMp { get; set; }
    }
}
