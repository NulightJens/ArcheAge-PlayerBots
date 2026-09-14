using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.NPChar;

namespace AAEmu.Game.Bots.Questing;

public sealed partial class BotQuestLifecycleController
{
    private readonly Dictionary<uint, DateTimeOffset> _ineffectiveTargets = [];
    private uint _watchedCombatTarget;
    private int _bestTargetHp;
    private float _bestTargetDistance;
    private DateTimeOffset _effectiveCombatAt;
    private int? _watchedQuestProgress;

    private bool TargetCoolingDown(Npc target, DateTimeOffset now) => target != null &&
        _ineffectiveTargets.TryGetValue(target.ObjId, out var until) && now < until;

    private bool CombatMadeNoProgress(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        var target = _objectiveTarget;
        var distance = Vector3.Distance(runtime.Bot.Transform.World.Position, target.Transform.World.Position);
        if (_watchedCombatTarget != target.ObjId || target.Hp < _bestTargetHp ||
            distance < _bestTargetDistance - 1f || _watchedQuestProgress != _objectiveCurrent)
        {
            _watchedCombatTarget = target.ObjId;
            _bestTargetHp = target.Hp;
            _bestTargetDistance = distance;
            _watchedQuestProgress = _objectiveCurrent;
            _effectiveCombatAt = now;
            return false;
        }
        if (now - _effectiveCombatAt < TimeSpan.FromSeconds(30)) return false;
        foreach (var expired in _ineffectiveTargets.Where(p => p.Value <= now).Select(p => p.Key).ToArray())
            _ineffectiveTargets.Remove(expired);
        if (_ineffectiveTargets.Count >= 64)
            _ineffectiveTargets.Remove(_ineffectiveTargets.MinBy(p => p.Value).Key);
        _ineffectiveTargets[target.ObjId] = now + TimeSpan.FromMinutes(5);
        _watchedCombatTarget = 0;
        Suspend(runtime, config, "objective_combat_timeout", now);
        return true;
    }
}
