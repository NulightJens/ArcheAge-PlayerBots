using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

public class BotCombatStateTests
{
    [Test]
    public async Task NewState_DefaultsToIdleInactiveUnforced()
    {
        var state = new BotCombatState();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.PreviousState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.BaseActivity).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(state.IsForced).IsFalse();
    }

    [Test]
    public async Task TransitionTo_Grinding_SetsIsActiveTrue()
    {
        var state = new BotCombatState();

        state.TransitionTo(BotCombatStateType.Grinding);

        await Assert.That(state.IsActive).IsTrue();
    }

    [Test]
    public async Task TransitionTo_Questing_SetsIsActiveTrue()
    {
        var state = new BotCombatState();

        state.TransitionTo(BotCombatStateType.Questing);

        await Assert.That(state.IsActive).IsTrue();
    }

    [Test]
    public async Task TransitionTo_Idle_SetsIsActiveFalse()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Grinding);

        state.TransitionTo(BotCombatStateType.Idle);

        await Assert.That(state.IsActive).IsFalse();
    }

    [Test]
    public async Task TransitionTo_Combat_KeepsIsActiveUnchanged()
    {
        var state = new BotCombatState();

        state.TransitionTo(BotCombatStateType.Combat);

        await Assert.That(state.IsActive).IsFalse();
    }

    [Test]
    public async Task TransitionTo_Resting_KeepsIsActiveUnchanged()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Grinding);

        state.TransitionTo(BotCombatStateType.Resting);

        await Assert.That(state.IsActive).IsTrue();
    }

    [Test]
    public async Task TransitionTo_RecordsPreviousState()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Grinding);

        state.TransitionTo(BotCombatStateType.Combat);

        await Assert.That(state.PreviousState).IsEqualTo(BotCombatStateType.Grinding);
    }

    [Test]
    public async Task TransitionTo_SameState_IsNoOpAndKeepsPreviousState()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);

        state.TransitionTo(BotCombatStateType.Combat);

        await Assert.That(state.PreviousState).IsEqualTo(BotCombatStateType.Grinding);
    }

    [Test]
    public async Task RestorePreviousState_FromCombatWithPreviousGrinding_ReturnsToGrindingActive()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);

        state.RestorePreviousState();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(state.IsActive).IsTrue();
        await Assert.That(state.PreviousState).IsEqualTo(BotCombatStateType.Combat);
    }

    [Test]
    public async Task RestorePreviousState_FromCombatWithPreviousIdle_GoesIdleInactive()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Combat);

        state.RestorePreviousState();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
    }

    [Test]
    public async Task RestorePreviousState_TwiceAfterNestedTransitions_ReturnsToBaseActivity()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);
        state.TransitionTo(BotCombatStateType.Resting);

        state.RestorePreviousState();
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(state.IsActive).IsTrue();

        state.RestorePreviousState();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(state.IsActive).IsTrue();
    }

    [Test]
    public async Task SetForcedState_WhileIdle_TransitionsImmediately()
    {
        var state = new BotCombatState();

        state.SetForcedState(BotCombatStateType.Grinding);

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(state.IsForced).IsTrue();
        await Assert.That(state.IsActive).IsTrue();
    }

    [Test]
    public async Task SetForcedState_WhileInCombat_DoesNotTransition()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Combat);

        state.SetForcedState(BotCombatStateType.Idle);

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Combat);
        await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Idle);
    }

    [Test]
    public async Task SetForcedState_WhileDueling_DoesNotTransition()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Dueling);

        state.SetForcedState(BotCombatStateType.Idle);

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Dueling);
        await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Idle);
    }

    [Test]
    public async Task SetForcedState_Null_ClearsForcedWithoutChangingCurrentState()
    {
        var state = new BotCombatState();
        state.SetForcedState(BotCombatStateType.Grinding);

        state.SetForcedState(null);

        await Assert.That(state.IsForced).IsFalse();
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
    }

    [Test]
    public async Task SetForcedState_Resting_DoesNotActivate()
    {
        var state = new BotCombatState();

        state.SetForcedState(BotCombatStateType.Resting);

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Resting);
        await Assert.That(state.IsActive).IsFalse();
    }

    [Test]
    public async Task RevertToForcedState_AfterCombat_ReturnsToForcedState()
    {
        var state = new BotCombatState();
        state.SetForcedState(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);

        state.RevertToForcedState();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
    }

    [Test]
    public async Task RevertToForcedState_NoForced_NoOp()
    {
        var state = new BotCombatState();
        state.TransitionTo(BotCombatStateType.Combat);

        state.RevertToForcedState();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Combat);
    }

    [Test]
    public async Task RevertToForcedState_AlreadyInForcedState_NoOp()
    {
        var state = new BotCombatState();
        state.SetForcedState(BotCombatStateType.Grinding);
        var previousState = state.PreviousState;

        state.RevertToForcedState();

        await Assert.That(state.PreviousState).IsEqualTo(previousState);
    }

    [Test]
    public async Task ShouldRevertToForced_ForcedGrindingInCombat_False()
    {
        var state = new BotCombatState();
        state.SetForcedState(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);

        await Assert.That(state.ShouldRevertToForced()).IsFalse();
    }

    [Test]
    public async Task ShouldRevertToForced_ForcedGrindingInIdle_True()
    {
        var state = new BotCombatState();
        state.SetForcedState(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Idle);

        await Assert.That(state.ShouldRevertToForced()).IsTrue();
    }

    [Test]
    public async Task ShouldRevertToForced_ForcedIdleInDueling_False()
    {
        var state = new BotCombatState();
        state.SetForcedState(BotCombatStateType.Idle);
        state.TransitionTo(BotCombatStateType.Dueling);

        await Assert.That(state.ShouldRevertToForced()).IsFalse();
    }

    [Test]
    public async Task BeginCombo_DefaultLock_Is2000ms()
    {
        var state = new BotCombatState();

        state.BeginCombo(1, 2);

        await Assert.That(state.ComboLockDurationMs).IsEqualTo(2000d);
    }

    [Test]
    public async Task BeginCombo_CustomLock_DoesNotChangeDefault()
    {
        var state = new BotCombatState();

        state.BeginCombo(1, 2, 10000d);
        state.BeginCombo(3, 4);

        await Assert.That(state.ComboLockDurationMs).IsEqualTo(2000d);
    }

    [Test]
    public async Task BeginCombo_UsesTheEngineTimestampWhenProvided()
    {
        var state = new BotCombatState();
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

        state.BeginCombo(1, 2, now: now);

        await Assert.That(state.LastComboSkillTime).IsEqualTo(now);
        await Assert.That(state.ComboLockStartTime).IsEqualTo(now);
        await Assert.That(state.LastSkillTime).IsEqualTo(now);
    }

    [Test]
    public async Task IsComboActive_SameSkillWithinWindow_ReturnsTrue()
    {
        var state = new BotCombatState();
        state.SetCombo(5);

        await Assert.That(state.IsComboActive(5)).IsTrue();
    }

    [Test]
    public async Task IsComboActive_SameSkillAfterWindow_ReturnsFalse()
    {
        var state = new BotCombatState
        {
            LastComboSkill = 5,
            LastComboSkillTime = DateTime.UtcNow.AddMilliseconds(-2500)
        };

        await Assert.That(state.IsComboActive(5)).IsFalse();
    }

    [Test]
    public async Task IsComboActive_DifferentSkill_ReturnsFalse()
    {
        var state = new BotCombatState();
        state.SetCombo(5);

        await Assert.That(state.IsComboActive(6)).IsFalse();
    }

    [Test]
    public async Task ClearCombo_ResetsSkillAndTimeToMinValue()
    {
        var state = new BotCombatState();
        state.SetCombo(5);

        state.ClearCombo();

        await Assert.That(state.LastComboSkill).IsEqualTo(0u);
        await Assert.That(state.LastComboSkillTime).IsEqualTo(DateTime.MinValue);
    }

    [Test]
    [Arguments(BotCombatStateType.Idle)]
    [Arguments(BotCombatStateType.Grinding)]
    [Arguments(BotCombatStateType.Questing)]
    [Arguments(BotCombatStateType.Roaming)]
    [Arguments(BotCombatStateType.Following)]
    [Arguments(BotCombatStateType.Combat)]
    [Arguments(BotCombatStateType.Dueling)]
    [Arguments(BotCombatStateType.Resting)]
    [Arguments(BotCombatStateType.Searching)]
    public async Task TransitionTo_EveryState_FromIdle_Theory(BotCombatStateType stateType)
    {
        var state = new BotCombatState();

        state.TransitionTo(stateType);

        await Assert.That(state.CurrentState).IsEqualTo(stateType);
        await Assert.That(state.IsActive).IsEqualTo(
            stateType is BotCombatStateType.Grinding or BotCombatStateType.Questing);
        await Assert.That(state.BaseActivity).IsEqualTo(
            stateType is BotCombatStateType.Idle or BotCombatStateType.Grinding or BotCombatStateType.Questing or
            BotCombatStateType.Roaming or BotCombatStateType.Following ? stateType : BotCombatStateType.Idle);
    }
}
