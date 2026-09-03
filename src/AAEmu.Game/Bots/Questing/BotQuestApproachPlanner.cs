using System.Numerics;

namespace AAEmu.Game.Bots.Questing;

/// <summary>
/// Converts a live NPC/doodad model position into a nearby terrain-valid interaction
/// point when the model origin itself is not on the movement height surface.
/// </summary>
internal static class BotQuestApproachPlanner
{
    private const float NavigationSurfaceTolerance = 1f;
    private const float InteractionSafetyMargin = 0.5f;
    private const float MinimumStandOff = 0.5f;
    private const float StandOffStep = 0.5f;

    internal static Vector3 ForWorldObject(
        Vector3 botPosition,
        Vector3 targetPosition,
        float interactionRadius,
        Func<float, float, float> groundHeight)
    {
        if (!IsFinite(botPosition) || !IsFinite(targetPosition) ||
            !float.IsFinite(interactionRadius) || interactionRadius <= InteractionSafetyMargin ||
            groundHeight == null)
        {
            return targetPosition;
        }

        float targetGround;
        try
        {
            targetGround = groundHeight(targetPosition.X, targetPosition.Y);
        }
        catch
        {
            return targetPosition;
        }

        // Preserve the normal exact-target path unless the movement boundary would
        // reject the live object's model origin as being off the terrain surface.
        if (!float.IsFinite(targetGround) || targetGround <= 0f ||
            MathF.Abs(targetPosition.Z - targetGround) <= NavigationSurfaceTolerance)
        {
            return targetPosition;
        }

        var away = new Vector2(
            botPosition.X - targetPosition.X,
            botPosition.Y - targetPosition.Y);
        if (away.LengthSquared() < 0.0001f)
            away = Vector2.UnitX;
        else
            away = Vector2.Normalize(away);

        var maximumInteractionDistance = interactionRadius - InteractionSafetyMargin;
        for (var standOff = maximumInteractionDistance;
             standOff >= MinimumStandOff;
             standOff -= StandOffStep)
        {
            var candidate = new Vector3(
                targetPosition.X + away.X * standOff,
                targetPosition.Y + away.Y * standOff,
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

            if (!float.IsFinite(candidateGround) || candidateGround <= 0f)
                continue;

            candidate.Z = candidateGround;
            if (Vector3.Distance(candidate, targetPosition) <= maximumInteractionDistance)
                return candidate;
        }

        return targetPosition;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
