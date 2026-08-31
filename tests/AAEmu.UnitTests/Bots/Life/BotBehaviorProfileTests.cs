using AAEmu.Game.Bots.Life;

namespace AAEmu.UnitTests.Bots.Life;

public sealed class BotBehaviorProfileTests
{
    [Test]
    public async Task Constructor_AcceptsValidLimitsAndNormalizesId()
    {
        var profile = new BotBehaviorProfile(
            "  balanced  ",
            TimeSpan.Zero,
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5));

        await Assert.That(profile.Id).IsEqualTo("balanced");
        await Assert.That(profile.MinimumActivityDuration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(profile.MaximumActivityDuration).IsEqualTo(TimeSpan.FromMinutes(20));
        await Assert.That(profile.MinimumRestDuration).IsEqualTo(TimeSpan.FromMinutes(1));
        await Assert.That(profile.MaximumRestDuration).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task Constructor_RejectsNegativeAndImpossibleLimits()
    {
        Action[] invalidProfiles =
        {
            () => _ = new BotBehaviorProfile(
                "negative-minimum-activity",
                TimeSpan.FromTicks(-1),
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            () => _ = new BotBehaviorProfile(
                "negative-maximum-activity",
                TimeSpan.Zero,
                TimeSpan.FromTicks(-1),
                TimeSpan.Zero,
                TimeSpan.Zero),
            () => _ = new BotBehaviorProfile(
                "negative-minimum-rest",
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.FromTicks(-1),
                TimeSpan.Zero),
            () => _ = new BotBehaviorProfile(
                "negative-maximum-rest",
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.FromTicks(-1)),
            () => _ = new BotBehaviorProfile(
                "activity-minimum-above-maximum",
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(1),
                TimeSpan.Zero,
                TimeSpan.Zero),
            () => _ = new BotBehaviorProfile(
                "rest-minimum-above-maximum",
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(1)),
            () => _ = new BotBehaviorProfile(
                " ",
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero)
        };

        foreach (var createProfile in invalidProfiles)
        {
            var rejected = false;
            try
            {
                createProfile();
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            await Assert.That(rejected).IsTrue();
        }
    }
}
