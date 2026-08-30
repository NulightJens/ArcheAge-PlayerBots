using System.Globalization;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Scripts.Commands;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

public class SpawnPassiveNpcCommandTests
{
    [Test]
    public async Task TryParse_TemplateOnly_UsesSafeDefaultDistance()
    {
        var parsed = SpawnPassiveNpcCommand.TryParse(["11180"], out var templateId, out var distance);

        await Assert.That(parsed).IsTrue();
        await Assert.That(templateId).IsEqualTo(11180u);
        await Assert.That(distance).IsEqualTo(SpawnPassiveNpcCommand.DefaultDistance);
    }

    [Test]
    public async Task TryParse_DecimalDistanceUnderGermanCulture_UsesInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var parsed = SpawnPassiveNpcCommand.TryParse(["11180", "14.5"], out _, out var distance);

            await Assert.That(parsed).IsTrue();
            await Assert.That(distance).IsEqualTo(14.5f);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Arguments("0")]
    [Arguments("abc")]
    [Arguments("NaN")]
    [Arguments("4.9")]
    [Arguments("100.1")]
    [Test]
    public async Task TryParse_InvalidInput_IsRejected(string value)
    {
        string[] args = value is "0" or "abc" ? [value] : ["11180", value];

        await Assert.That(SpawnPassiveNpcCommand.TryParse(args, out _, out _)).IsFalse();
    }

    [Test]
    public async Task ApplyPassiveAi_DetachesExistingAiAndRegistersDummyBehavior()
    {
        var npc = new Npc();
        var previousAi = new DummyAiCharacter { Owner = npc };
        previousAi.Start();
        previousAi.GoToIdle();
        npc.Ai = previousAi;
        NpcAi registeredAi = null;

        var passiveAi = SpawnPassiveNpcCommand.ApplyPassiveAi(npc, ai => registeredAi = ai);

        await Assert.That(previousAi.Owner).IsNull();
        await Assert.That(npc.Ai).IsSameReferenceAs(passiveAi);
        await Assert.That(registeredAi).IsSameReferenceAs(passiveAi);
        await Assert.That(passiveAi.Owner).IsSameReferenceAs(npc);
        await Assert.That(passiveAi.GetCurrentBehavior()).IsTypeOf<DummyBehavior>();
        await Assert.That(npc.CurrentTarget).IsNull();
    }
}
