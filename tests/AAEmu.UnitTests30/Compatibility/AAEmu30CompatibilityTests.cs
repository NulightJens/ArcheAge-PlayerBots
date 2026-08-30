using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using Xunit;

namespace AAEmu.UnitTests.PlayerBots.Compatibility;

public sealed class AAEmu30CompatibilityTests
{
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
