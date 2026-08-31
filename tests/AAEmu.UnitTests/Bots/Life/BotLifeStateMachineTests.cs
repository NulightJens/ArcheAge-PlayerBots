using AAEmu.Game.Bots.Life;

namespace AAEmu.UnitTests.Bots.Life;

public sealed class BotLifeStateMachineTests
{
    private static readonly DateTimeOffset s_now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Transition_EveryStateEventPairReturnsAnExplicitOutcome()
    {
        var transitions = new List<BotLifeTransition>();

        foreach (var state in Enum.GetValues<BotLifeState>())
        {
            foreach (var eventKind in Enum.GetValues<BotLifeEventKind>())
            {
                transitions.Add(BotLifeStateMachine.Transition(
                    new BotLifeSnapshot(state, s_now),
                    new BotLifeEvent(eventKind, s_now.AddHours(1)),
                    Profile()));
            }
        }

        await Assert.That(transitions.Count)
            .IsEqualTo(Enum.GetValues<BotLifeState>().Length * Enum.GetValues<BotLifeEventKind>().Length);
        await Assert.That(transitions.All(transition =>
                transition.Outcome is BotLifeTransitionOutcome.Accepted or BotLifeTransitionOutcome.Rejected))
            .IsTrue();
        await Assert.That(transitions.Where(transition => !transition.Accepted)
                .All(transition => transition.After == transition.Before))
            .IsTrue();
    }

    [Test]
    public async Task Transition_TraversesTheCompleteLifecycle()
    {
        var profile = Profile();
        var current = new BotLifeSnapshot(BotLifeState.Offline, s_now);

        current = Accept(current, BotLifeEventKind.SpawnRequested, s_now).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Spawning);
        current = Accept(current, BotLifeEventKind.SpawnSucceeded, s_now.AddSeconds(1)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Idle);
        current = Accept(current, BotLifeEventKind.ActivityRequested, s_now.AddSeconds(2)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Active);
        current = Accept(current, BotLifeEventKind.RestRequested, s_now.AddMinutes(6)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Resting);
        current = Accept(current, BotLifeEventKind.Died, s_now.AddMinutes(7)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Dead);
        current = Accept(current, BotLifeEventKind.RecoveryStarted, s_now.AddMinutes(8)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Recovering);
        current = Accept(current, BotLifeEventKind.RecoveryCompleted, s_now.AddMinutes(9)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Idle);
        current = Accept(current, BotLifeEventKind.LogoutRequested, s_now.AddMinutes(10)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Despawning);
        current = Accept(current, BotLifeEventKind.DespawnCompleted, s_now.AddMinutes(11)).After;
        await Assert.That(current.State).IsEqualTo(BotLifeState.Offline);

        BotLifeTransition Accept(BotLifeSnapshot snapshot, BotLifeEventKind kind, DateTimeOffset at)
        {
            var transition = BotLifeStateMachine.Transition(snapshot, new BotLifeEvent(kind, at), profile);
            if (!transition.Accepted)
                throw new InvalidOperationException($"Expected {kind} from {snapshot.State} to be accepted.");
            return transition;
        }
    }

    [Test]
    public async Task Transition_DuplicateLifecycleEventsAreIdempotent()
    {
        var idempotentEvents = new[]
        {
            BotLifeEventKind.Died,
            BotLifeEventKind.SpawnFailed,
            BotLifeEventKind.LogoutRequested,
            BotLifeEventKind.RecoveryStarted,
            BotLifeEventKind.RecoveryCompleted,
            BotLifeEventKind.Restarted
        };

        foreach (var state in Enum.GetValues<BotLifeState>())
        {
            foreach (var eventKind in idempotentEvents)
            {
                var occurrence = new BotLifeEvent(eventKind, s_now.AddMinutes(1));
                var first = BotLifeStateMachine.Transition(
                    new BotLifeSnapshot(state, s_now),
                    occurrence,
                    Profile());
                var duplicate = BotLifeStateMachine.Transition(first.After, occurrence, Profile());

                await Assert.That(duplicate.After).IsEqualTo(first.After);
                await Assert.That(duplicate.Changed).IsFalse();
                if (first.Accepted && first.Changed)
                {
                    await Assert.That(duplicate.Accepted).IsTrue();
                    await Assert.That(duplicate.Reason).IsEqualTo(BotLifeTransitionReason.AlreadyApplied);
                }
            }
        }
    }

    [Test]
    public async Task Transition_ProfileDurationsUseOnlySuppliedTimestamps()
    {
        var profile = Profile();
        var active = new BotLifeSnapshot(BotLifeState.Active, s_now);
        var earlyRest = BotLifeStateMachine.Transition(
            active,
            new BotLifeEvent(BotLifeEventKind.RestRequested, s_now.AddMinutes(4)),
            profile);
        var onTimeRest = BotLifeStateMachine.Transition(
            active,
            new BotLifeEvent(BotLifeEventKind.RestRequested, s_now.AddMinutes(5)),
            profile);
        var beforeActivityLimit = BotLifeStateMachine.Transition(
            active,
            new BotLifeEvent(BotLifeEventKind.TimeElapsed, s_now.AddMinutes(19)),
            profile);
        var atActivityLimit = BotLifeStateMachine.Transition(
            active,
            new BotLifeEvent(BotLifeEventKind.TimeElapsed, s_now.AddMinutes(20)),
            profile);

        var resting = new BotLifeSnapshot(BotLifeState.Resting, s_now);
        var earlyActivity = BotLifeStateMachine.Transition(
            resting,
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, s_now.AddMinutes(2)),
            profile);
        var onTimeActivity = BotLifeStateMachine.Transition(
            resting,
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, s_now.AddMinutes(3)),
            profile);
        var beforeRestLimit = BotLifeStateMachine.Transition(
            resting,
            new BotLifeEvent(BotLifeEventKind.TimeElapsed, s_now.AddMinutes(9)),
            profile);
        var atRestLimit = BotLifeStateMachine.Transition(
            resting,
            new BotLifeEvent(BotLifeEventKind.TimeElapsed, s_now.AddMinutes(10)),
            profile);

        await Assert.That(earlyRest.Reason).IsEqualTo(BotLifeTransitionReason.MinimumDurationNotMet);
        await Assert.That(onTimeRest.After.State).IsEqualTo(BotLifeState.Resting);
        await Assert.That(beforeActivityLimit.Reason).IsEqualTo(BotLifeTransitionReason.MaximumDurationNotReached);
        await Assert.That(atActivityLimit.After.State).IsEqualTo(BotLifeState.Resting);
        await Assert.That(earlyActivity.Reason).IsEqualTo(BotLifeTransitionReason.MinimumDurationNotMet);
        await Assert.That(onTimeActivity.After.State).IsEqualTo(BotLifeState.Active);
        await Assert.That(beforeRestLimit.Reason).IsEqualTo(BotLifeTransitionReason.MaximumDurationNotReached);
        await Assert.That(atRestLimit.After.State).IsEqualTo(BotLifeState.Idle);
    }

    [Test]
    public async Task Transition_EarlierTimestampIsRejectedWithoutMutation()
    {
        var current = new BotLifeSnapshot(BotLifeState.Idle, s_now);
        var transition = BotLifeStateMachine.Transition(
            current,
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, s_now.AddTicks(-1)),
            Profile());

        await Assert.That(transition.Accepted).IsFalse();
        await Assert.That(transition.After).IsEqualTo(current);
        await Assert.That(transition.Reason).IsEqualTo(BotLifeTransitionReason.TimestampBeforeStateEntry);
    }

    [Test]
    public async Task Replay_SameSequenceProducesSameFinalStateAndTrace()
    {
        var initial = new BotLifeSnapshot(BotLifeState.Offline, s_now);
        var sequence = new[]
        {
            new BotLifeEvent(BotLifeEventKind.SpawnRequested, s_now),
            new BotLifeEvent(BotLifeEventKind.SpawnSucceeded, s_now.AddSeconds(1)),
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, s_now.AddSeconds(2)),
            new BotLifeEvent(BotLifeEventKind.RestRequested, s_now.AddMinutes(7)),
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, s_now.AddMinutes(8)),
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, s_now.AddMinutes(10)),
            new BotLifeEvent(BotLifeEventKind.Died, s_now.AddMinutes(11)),
            new BotLifeEvent(BotLifeEventKind.RecoveryStarted, s_now.AddMinutes(12)),
            new BotLifeEvent(BotLifeEventKind.RecoveryCompleted, s_now.AddMinutes(13)),
            new BotLifeEvent(BotLifeEventKind.LogoutRequested, s_now.AddMinutes(14)),
            new BotLifeEvent(BotLifeEventKind.DespawnCompleted, s_now.AddMinutes(15)),
            new BotLifeEvent(BotLifeEventKind.Restarted, s_now.AddMinutes(16))
        };

        var first = BotLifeStateMachine.Replay(initial, sequence, Profile());
        var second = BotLifeStateMachine.Replay(initial, sequence, Profile());

        await Assert.That(first.FinalSnapshot).IsEqualTo(second.FinalSnapshot);
        await Assert.That(first.FinalSnapshot.State).IsEqualTo(BotLifeState.Offline);
        await Assert.That(first.Trace.SequenceEqual(second.Trace)).IsTrue();
        await Assert.That(first.Trace.Count).IsEqualTo(sequence.Length);
        await Assert.That(first.Trace.Count(transition => !transition.Accepted)).IsEqualTo(1);
    }

    private static BotBehaviorProfile Profile()
    {
        return new BotBehaviorProfile(
            "balanced",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(10));
    }
}
