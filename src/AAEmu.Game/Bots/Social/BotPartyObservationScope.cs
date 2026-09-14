#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Social;

internal sealed partial class BotPartyQuestCoordinator
{
    private readonly Dictionary<uint, MemberScope> _memberScopes = [];
    private readonly record struct MemberScope(BotRuntime Runtime, object World, uint? Instance);

    private bool HasScopeChanged(BotRuntime[] runtimes)
    {
        foreach (var scope in _memberScopes.Values)
        {
            var runtime = scope.Runtime;
            var admitted = runtime.OwnerHost is { } host
                ? ReferenceEquals(host.GetRuntime(runtime.Bot.Id), runtime)
                : runtimes.Contains(runtime);
            if (runtime.Retired || !admitted ||
                !ReferenceEquals(scope.World, runtime.Bot.ParentWorld) ||
                scope.Instance != runtime.Bot.Transform?.InstanceId)
                return true;
        }
        return false;
    }

    private void CaptureScopes(BotRuntime[] members)
    {
        _memberScopes.Clear();
        foreach (var member in members)
            _memberScopes[member.Bot.Id] = new(member, member.Bot.ParentWorld, member.Bot.Transform?.InstanceId);
    }

    private void InvalidateRecoveryScope()
    {
        ClearSupport();
        foreach (var member in _members)
        {
            _personalSpace.Cancel(member);
            if (!member.Retired && member.MovementState.TravelOwner == BotMovementOwner.PartyQuest)
                _stop(member);
        }
        _holds.Clear();
        _trails.Clear();
        _teleports.Clear();
        _failedAnchors.Clear();
        _routeEpochs.Clear();
        _nativeWork.Clear();
        _regrouping = false;
        _returning = false;
        _anchorMember = 0;
        _nextScan = default;
        NewRouteEpoch();
        Logger.Info("BOT ev=party_recovery_invalidated reason=member_world_instance_or_lifecycle_changed");
    }
}
#endif
