using System;
using System.Numerics;

namespace SaSimulator
{
    internal static class Physics
    {
        public static bool IsPointInCone(Vector2 point, Vector2 coneOrigin, float coneRotation, float coneAngle)
        {
            Vector2 difference = point - coneOrigin;
            float direction = (float)Math.Atan2(difference.Y, difference.X);
            float directionChange = (float)((direction - coneRotation + 4 * Math.PI) % (2 * Math.PI));
            return directionChange < coneAngle / 2 || directionChange > 2 * Math.PI - coneAngle / 2;
        }
    }
}
