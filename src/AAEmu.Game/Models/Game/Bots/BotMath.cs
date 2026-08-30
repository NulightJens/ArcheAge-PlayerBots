using System.Numerics;

namespace AAEmu.Game.Models.Game.Bots;

public static class BotMath
{
    public static Vector3 Forward(float yawRadians)
    {
        return new Vector3(-MathF.Sin(yawRadians), MathF.Cos(yawRadians), 0f);
    }
}
