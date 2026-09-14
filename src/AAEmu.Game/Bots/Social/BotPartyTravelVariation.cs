#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.Game.Bots.Social;

/// <summary>Small stable road lanes; original endpoints and local BAI paths remain authoritative.</summary>
internal static class BotPartyTravelVariation
{
    internal static BotTravelRoute Apply(BotTravelRoute route, uint id, Func<float, float, float> height)
    {
        if (!route.Mode.Contains("road", StringComparison.Ordinal) ||
            route.Mode.Contains("bai", StringComparison.Ordinal) || route.Waypoints.Count < 3 ||
            route.Waypoints.Count > 4096 || height == null) return route;
        var lane = ((id % 3) - 1f) * .85f;
        if (lane == 0) return route;
        var points = route.Waypoints.ToArray();
        var changed = false;
        for (var i = 1; i < points.Length - 1; i++)
        {
            var previous = route.Waypoints[i - 1];
            var point = route.Waypoints[i];
            var next = route.Waypoints[i + 1];
            if (Vector3.Distance(point, points[^1]) < 8f) continue;
            var incoming = new Vector2(point.X - previous.X, point.Y - previous.Y);
            var outgoing = new Vector2(next.X - point.X, next.Y - point.Y);
            if (incoming.Length() < 3f || outgoing.Length() < 3f) continue;
            incoming = Vector2.Normalize(incoming); outgoing = Vector2.Normalize(outgoing);
            // Preserve sharp corners and tightly spaced local detail.
            if (Vector2.Dot(incoming, outgoing) < .95f) continue;
            var tangent = Vector2.Normalize(incoming + outgoing);
            var side = new Vector2(-tangent.Y, tangent.X);
            try
            {
                var ground = height(point.X, point.Y);
                if (!float.IsFinite(ground) || MathF.Abs(ground - point.Z) > .3f) continue;
                var valid = true;
                // Check both edges, including the intermediate shoulder sample.
                foreach (var lateral in new[] { -.85f, -.425f, .425f, .85f })
                {
                    var sample = height(point.X + side.X * lateral, point.Y + side.Y * lateral);
                    if (!float.IsFinite(sample) || MathF.Abs(sample - ground) > .3f) { valid = false; break; }
                }
                if (!valid) continue;
                var candidate = new Vector3(point.X + side.X * lane, point.Y + side.Y * lane, 0);
                candidate.Z = height(candidate.X, candidate.Y);
                points[i] = candidate;
                changed = true;
            }
            catch { /* Keep the original waypoint when terrain is unavailable. */ }
        }
        return changed ? route with { Waypoints = points, Detail = route.Detail + $" party_lane={lane:F2}" } : route;
    }
}
#endif
