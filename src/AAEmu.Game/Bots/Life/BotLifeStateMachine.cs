namespace AAEmu.Game.Bots.Life;

public enum BotLifeState
{
    Offline,
    Spawning,
    Idle,
    Active,
    Resting,
    Dead,
    Recovering,
    Despawning
}

public enum BotLifeEventKind
{
    SpawnRequested,
    SpawnSucceeded,
    SpawnFailed,
    ActivityRequested,
    RestRequested,
    TimeElapsed,
    Died,
    RecoveryStarted,
    RecoveryCompleted,
    LogoutRequested,
    DespawnCompleted,
    Restarted
}

public enum BotLifeTransitionOutcome
{
    Accepted,
    Rejected
}

public enum BotLifeTransitionReason
{
    StateChanged,
    AlreadyApplied,
    MinimumDurationNotMet,
    MaximumDurationNotReached,
    InvalidForState,
    TimestampBeforeStateEntry
}

public readonly record struct BotLifeSnapshot(BotLifeState State, DateTimeOffset EnteredAt);

public readonly record struct BotLifeEvent(BotLifeEventKind Kind, DateTimeOffset At);

public readonly record struct BotLifeTransition(
    BotLifeSnapshot Before,
    BotLifeEvent Event,
    BotLifeSnapshot After,
    BotLifeTransitionOutcome Outcome,
    BotLifeTransitionReason Reason)
{
    public bool Accepted => Outcome == BotLifeTransitionOutcome.Accepted;
    public bool Changed => Before != After;
}

public sealed class BotLifeReplayResult
{
    internal BotLifeReplayResult(BotLifeSnapshot finalSnapshot, IReadOnlyList<BotLifeTransition> trace)
    {
        FinalSnapshot = finalSnapshot;
        Trace = trace;
    }

    public BotLifeSnapshot FinalSnapshot { get; }
    public IReadOnlyList<BotLifeTransition> Trace { get; }
}

public static class BotLifeStateMachine
{
    public static BotLifeTransition Transition(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        BotBehaviorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (occurrence.At < current.EnteredAt)
            return Reject(current, occurrence, BotLifeTransitionReason.TimestampBeforeStateEntry);

        return occurrence.Kind switch
        {
            BotLifeEventKind.SpawnRequested => SpawnRequested(current, occurrence),
            BotLifeEventKind.SpawnSucceeded => SpawnSucceeded(current, occurrence),
            BotLifeEventKind.SpawnFailed => SpawnFailed(current, occurrence),
            BotLifeEventKind.ActivityRequested => ActivityRequested(current, occurrence, profile),
            BotLifeEventKind.RestRequested => RestRequested(current, occurrence, profile),
            BotLifeEventKind.TimeElapsed => TimeElapsed(current, occurrence, profile),
            BotLifeEventKind.Died => Died(current, occurrence),
            BotLifeEventKind.RecoveryStarted => RecoveryStarted(current, occurrence),
            BotLifeEventKind.RecoveryCompleted => RecoveryCompleted(current, occurrence),
            BotLifeEventKind.LogoutRequested => LogoutRequested(current, occurrence),
            BotLifeEventKind.DespawnCompleted => DespawnCompleted(current, occurrence),
            BotLifeEventKind.Restarted => Restarted(current, occurrence),
            _ => throw new ArgumentOutOfRangeException(nameof(occurrence), occurrence.Kind, "Unknown life event.")
        };
    }

    public static BotLifeReplayResult Replay(
        BotLifeSnapshot initial,
        IEnumerable<BotLifeEvent> events,
        BotBehaviorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(profile);

        var current = initial;
        var trace = new List<BotLifeTransition>();
        foreach (var occurrence in events)
        {
            var transition = Transition(current, occurrence, profile);
            trace.Add(transition);
            current = transition.After;
        }

        return new BotLifeReplayResult(current, Array.AsReadOnly(trace.ToArray()));
    }

    private static BotLifeTransition SpawnRequested(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => Move(current, occurrence, BotLifeState.Spawning),
            BotLifeState.Spawning => AcceptUnchanged(current, occurrence),
            BotLifeState.Idle => RejectInvalid(current, occurrence),
            BotLifeState.Active => RejectInvalid(current, occurrence),
            BotLifeState.Resting => RejectInvalid(current, occurrence),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition SpawnSucceeded(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => Move(current, occurrence, BotLifeState.Idle),
            BotLifeState.Idle => AcceptUnchanged(current, occurrence),
            BotLifeState.Active => RejectInvalid(current, occurrence),
            BotLifeState.Resting => RejectInvalid(current, occurrence),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition SpawnFailed(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => AcceptUnchanged(current, occurrence),
            BotLifeState.Spawning => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Idle => RejectInvalid(current, occurrence),
            BotLifeState.Active => RejectInvalid(current, occurrence),
            BotLifeState.Resting => RejectInvalid(current, occurrence),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition ActivityRequested(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        BotBehaviorProfile profile)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => Move(current, occurrence, BotLifeState.Active),
            BotLifeState.Active => AcceptUnchanged(current, occurrence),
            BotLifeState.Resting => AtLeast(
                current,
                occurrence,
                profile.MinimumRestDuration,
                BotLifeState.Active),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition RestRequested(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        BotBehaviorProfile profile)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => RejectInvalid(current, occurrence),
            BotLifeState.Active => AtLeast(
                current,
                occurrence,
                profile.MinimumActivityDuration,
                BotLifeState.Resting),
            BotLifeState.Resting => AcceptUnchanged(current, occurrence),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition TimeElapsed(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        BotBehaviorProfile profile)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => RejectInvalid(current, occurrence),
            BotLifeState.Active => AtMaximum(
                current,
                occurrence,
                profile.MaximumActivityDuration,
                BotLifeState.Resting),
            BotLifeState.Resting => AtMaximum(
                current,
                occurrence,
                profile.MaximumRestDuration,
                BotLifeState.Idle),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition Died(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => Move(current, occurrence, BotLifeState.Dead),
            BotLifeState.Active => Move(current, occurrence, BotLifeState.Dead),
            BotLifeState.Resting => Move(current, occurrence, BotLifeState.Dead),
            BotLifeState.Dead => AcceptUnchanged(current, occurrence),
            BotLifeState.Recovering => Move(current, occurrence, BotLifeState.Dead),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition RecoveryStarted(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => RejectInvalid(current, occurrence),
            BotLifeState.Active => RejectInvalid(current, occurrence),
            BotLifeState.Resting => RejectInvalid(current, occurrence),
            BotLifeState.Dead => Move(current, occurrence, BotLifeState.Recovering),
            BotLifeState.Recovering => AcceptUnchanged(current, occurrence),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition RecoveryCompleted(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => RejectInvalid(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => AcceptUnchanged(current, occurrence),
            BotLifeState.Active => RejectInvalid(current, occurrence),
            BotLifeState.Resting => RejectInvalid(current, occurrence),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => Move(current, occurrence, BotLifeState.Idle),
            BotLifeState.Despawning => RejectInvalid(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition LogoutRequested(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => AcceptUnchanged(current, occurrence),
            BotLifeState.Spawning => Move(current, occurrence, BotLifeState.Despawning),
            BotLifeState.Idle => Move(current, occurrence, BotLifeState.Despawning),
            BotLifeState.Active => Move(current, occurrence, BotLifeState.Despawning),
            BotLifeState.Resting => Move(current, occurrence, BotLifeState.Despawning),
            BotLifeState.Dead => Move(current, occurrence, BotLifeState.Despawning),
            BotLifeState.Recovering => Move(current, occurrence, BotLifeState.Despawning),
            BotLifeState.Despawning => AcceptUnchanged(current, occurrence),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition DespawnCompleted(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => AcceptUnchanged(current, occurrence),
            BotLifeState.Spawning => RejectInvalid(current, occurrence),
            BotLifeState.Idle => RejectInvalid(current, occurrence),
            BotLifeState.Active => RejectInvalid(current, occurrence),
            BotLifeState.Resting => RejectInvalid(current, occurrence),
            BotLifeState.Dead => RejectInvalid(current, occurrence),
            BotLifeState.Recovering => RejectInvalid(current, occurrence),
            BotLifeState.Despawning => Move(current, occurrence, BotLifeState.Offline),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition Restarted(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return current.State switch
        {
            BotLifeState.Offline => AcceptUnchanged(current, occurrence),
            BotLifeState.Spawning => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Idle => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Active => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Resting => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Dead => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Recovering => Move(current, occurrence, BotLifeState.Offline),
            BotLifeState.Despawning => Move(current, occurrence, BotLifeState.Offline),
            _ => UnknownState(current)
        };
    }

    private static BotLifeTransition AtLeast(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        TimeSpan minimum,
        BotLifeState next)
    {
        return occurrence.At - current.EnteredAt >= minimum
            ? Move(current, occurrence, next)
            : Reject(current, occurrence, BotLifeTransitionReason.MinimumDurationNotMet);
    }

    private static BotLifeTransition AtMaximum(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        TimeSpan maximum,
        BotLifeState next)
    {
        return occurrence.At - current.EnteredAt >= maximum
            ? Move(current, occurrence, next)
            : Reject(current, occurrence, BotLifeTransitionReason.MaximumDurationNotReached);
    }

    private static BotLifeTransition Move(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        BotLifeState next)
    {
        return new BotLifeTransition(
            current,
            occurrence,
            new BotLifeSnapshot(next, occurrence.At),
            BotLifeTransitionOutcome.Accepted,
            BotLifeTransitionReason.StateChanged);
    }

    private static BotLifeTransition AcceptUnchanged(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return new BotLifeTransition(
            current,
            occurrence,
            current,
            BotLifeTransitionOutcome.Accepted,
            BotLifeTransitionReason.AlreadyApplied);
    }

    private static BotLifeTransition RejectInvalid(BotLifeSnapshot current, BotLifeEvent occurrence)
    {
        return Reject(current, occurrence, BotLifeTransitionReason.InvalidForState);
    }

    private static BotLifeTransition Reject(
        BotLifeSnapshot current,
        BotLifeEvent occurrence,
        BotLifeTransitionReason reason)
    {
        return new BotLifeTransition(
            current,
            occurrence,
            current,
            BotLifeTransitionOutcome.Rejected,
            reason);
    }

    private static BotLifeTransition UnknownState(BotLifeSnapshot current)
    {
        throw new ArgumentOutOfRangeException(nameof(current), current.State, "Unknown life state.");
    }
}
