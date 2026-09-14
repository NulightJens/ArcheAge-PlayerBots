#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.Game.Bots.Social;

/// <summary>Bounded evidence of a member's actual walk since the party was together.</summary>
internal sealed class BotPartyTrail
{
    private readonly List<Vector3> _points = [];
    internal bool Valid { get; private set; }
    internal Vector3 Origin => _points[0];
    internal void Reset(Vector3 point) { _points.Clear(); _points.Add(point); Valid = true; }
    internal void Invalidate() => Valid = false;
    internal void Observe(Vector3 point, bool safe)
    {
        if (!Valid) return;
        if (!safe || !Finite(point)) { Valid = false; return; }
        var distance = Vector3.Distance(_points[^1], point);
        if (distance > 8f || _points.Count >= 256) { Valid = false; return; }
        if (distance >= 1f) _points.Add(point);
    }
    internal Vector3[] ReturnPath(Vector3 current, Func<float, float, float> height)
    {
        if (!Valid || !Finite(current)) return [];
        var nearest = Enumerable.Range(0, _points.Count).MinBy(i => Vector3.Distance(current, _points[i]));
        if (Vector3.Distance(current, _points[nearest]) > 8f) return [];
        var result = new List<Vector3>();
        var previous = current;
        for (var i = nearest; i >= 0; i--)
        {
            var next = _points[i];
            if (Vector3.Distance(previous, next) < 0.5f) continue;
            // Recheck short occupied segments. Never append an unwalked long shortcut.
            if (!BotTravelRoutePlanner.IsSafeTerminalDirect(previous, next, height)) return [];
            result.Add(next); previous = next;
        }
        return result.ToArray();
    }
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
#endif
