namespace AAEmu.Game.Bots.Life;

public sealed class BotBehaviorProfile
{
    public BotBehaviorProfile(
        string id,
        TimeSpan minimumActivityDuration,
        TimeSpan maximumActivityDuration,
        TimeSpan minimumRestDuration,
        TimeSpan maximumRestDuration)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A behavior profile id is required.", nameof(id));

        RejectNegative(minimumActivityDuration, nameof(minimumActivityDuration));
        RejectNegative(maximumActivityDuration, nameof(maximumActivityDuration));
        RejectNegative(minimumRestDuration, nameof(minimumRestDuration));
        RejectNegative(maximumRestDuration, nameof(maximumRestDuration));

        if (minimumActivityDuration > maximumActivityDuration)
        {
            throw new ArgumentException(
                "The minimum activity duration cannot exceed the maximum activity duration.",
                nameof(minimumActivityDuration));
        }

        if (minimumRestDuration > maximumRestDuration)
        {
            throw new ArgumentException(
                "The minimum rest duration cannot exceed the maximum rest duration.",
                nameof(minimumRestDuration));
        }

        Id = id.Trim();
        MinimumActivityDuration = minimumActivityDuration;
        MaximumActivityDuration = maximumActivityDuration;
        MinimumRestDuration = minimumRestDuration;
        MaximumRestDuration = maximumRestDuration;
    }

    public string Id { get; }
    public TimeSpan MinimumActivityDuration { get; }
    public TimeSpan MaximumActivityDuration { get; }
    public TimeSpan MinimumRestDuration { get; }
    public TimeSpan MaximumRestDuration { get; }

    private static void RejectNegative(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, value, "Profile durations cannot be negative.");
    }
}
