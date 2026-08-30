using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
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
