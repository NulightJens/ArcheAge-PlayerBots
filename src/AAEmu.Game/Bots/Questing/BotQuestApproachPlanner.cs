using System.Numerics;

namespace AAEmu.Game.Bots.Questing;

/// <summary>
/// Selects a grounded stand-off point inside a live object's interaction radius.
/// </summary>
internal static class BotQuestApproachPlanner
{
    private const float InteractionSafetyMargin = 0.5f;
    private const float MinimumStandOff = 0.5f;
    private const float StandOffStep = 0.5f;

    internal static Vector3 ForWorldObject(
        Vector3 botPosition,
        Vector3 targetPosition,
        float interactionRadius,
        Func<float, float, float> groundHeight,
        uint variationKey = 0)
    {
        return TrySelect(botPosition, targetPosition, interactionRadius, groundHeight,
            null, out var destination, variationKey) ? destination : targetPosition;
    }

#if !PLAYERBOTS_AAEMU_3_0
    // Target elevation belongs to the native object. A grounded approach is a
    // separate point, and unavailable terrain must not fall back to that object.
    // Ground travel does not implement swimming; submerged targets are deferred
    // unless an interaction remains reachable from a dry grounded approach.
    internal static bool TryForWorldObject(
        Vector3 botPosition, Vector3 targetPosition, float interactionRadius,
        Func<float, float, float> groundHeight, Func<Vector3, bool> isWater,
        out Vector3 destination, uint variationKey = 0)
    {
        destination = default;
        return isWater != null && TrySelect(botPosition, targetPosition, interactionRadius,
            groundHeight, isWater, out destination, variationKey);
    }
#endif

    private static bool TrySelect(
        Vector3 botPosition, Vector3 targetPosition, float interactionRadius,
        Func<float, float, float> groundHeight, Func<Vector3, bool> isWater,
        out Vector3 destination, uint variationKey)
    {
        destination = default;
        if (!IsFinite(botPosition) || !IsFinite(targetPosition) ||
            !float.IsFinite(interactionRadius) || interactionRadius <= InteractionSafetyMargin ||
            groundHeight == null)
        {
            return false;
        }

        float targetGround;
        try
        {
            targetGround = groundHeight(targetPosition.X, targetPosition.Y);
        }
        catch
        {
            return false;
        }

        if (!float.IsFinite(targetGround))
            return false;

        var away = new Vector2(
            botPosition.X - targetPosition.X,
            botPosition.Y - targetPosition.Y);
        if (away.LengthSquared() < 0.0001f)
            away = Vector2.UnitX;
        else
            away = Vector2.Normalize(away);

        if (variationKey != 0)
        {
            var angle = ((variationKey % 3) - 1f) * 0.4f;
            away = Vector2.Transform(away, Matrix3x2.CreateRotation(angle));
        }

        var maximumInteractionDistance = interactionRadius - InteractionSafetyMargin;
        // A small depth difference complements the existing angular spread.
        // Preserve tight interaction ranges and all solo approaches.
        var preferredStandOff = Math.Min(maximumInteractionDistance, 6f);
        if (variationKey != 0 && interactionRadius >= 3f)
            preferredStandOff -= (variationKey % 3) * .3f;
        // Try the near side first, then bounded alternatives around the object.
        // A raised object may be reachable from only one side of a slope.
        foreach (var angle in new[] { 0f, .7853982f, -.7853982f, 1.5707964f, -1.5707964f, 3.1415927f })
        {
        var direction = Vector2.Transform(away, Matrix3x2.CreateRotation(angle));
        for (var standOff = preferredStandOff;
             standOff >= MinimumStandOff;
             standOff -= StandOffStep)
        {
            var candidate = new Vector3(
                targetPosition.X + direction.X * standOff,
                targetPosition.Y + direction.Y * standOff,
                targetPosition.Z);
            float candidateGround;
            try
            {
                candidateGround = groundHeight(candidate.X, candidate.Y);
            }
            catch
            {
                continue;
            }

            if (!float.IsFinite(candidateGround))
                continue;

            candidate.Z = candidateGround;
            if (!(Vector3.Distance(candidate, targetPosition) <= maximumInteractionDistance))
                continue;
            try
            {
                if (isWater?.Invoke(candidate) == true)
                    continue;
            }
            catch
            {
                continue;
            }
            destination = candidate;
            return true;
        }
        }

        return false;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
