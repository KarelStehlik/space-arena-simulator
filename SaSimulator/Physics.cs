using System;
using System.Numerics;

namespace SaSimulator
{
    internal static class Physics
    {
        // Angle between the 2 angles, normalized to 0-2PI
        private static float RelativeAngle(float firstAngle, float secondAngle)
        {
            return (float)((secondAngle - firstAngle + 4 * Math.PI) % (2 * Math.PI));
        }

        public static bool IsPointInCone(Vector2 point, Vector2 coneOrigin, float coneRotation, float coneAngle)
        {
            Vector2 difference = point - coneOrigin;
            float direction = (float)Math.Atan2(difference.Y, difference.X);
            float rel = RelativeAngle(direction, coneRotation);
            return rel < coneAngle / 2 || rel > 2 * Math.PI - coneAngle / 2;
        }

        public static bool ConeCircleIntersect(Vector2 circleCentre, float circleRadius, Vector2 coneOrigin, float coneRotation, float coneAngle)
        {
            Vector2 difference = circleCentre - coneOrigin;
            float angle = (float)Math.Atan2(difference.Y, difference.X);
            float rel = RelativeAngle(angle, coneRotation);
            if (rel < coneAngle / 2 || rel > 2 * Math.PI - coneAngle / 2) // circle centre is in cone
            {
                return true;
            }
            float dist = Vector2.Distance(circleCentre, coneOrigin);
            float angleOfClosestEdgeToCircle = Math.Min(RelativeAngle(angle, coneRotation + coneAngle), RelativeAngle(angle, coneRotation - coneAngle));
            return dist * Math.Abs(Math.Sin(angleOfClosestEdgeToCircle)) < circleRadius;

        }

        // clamp an angle so that it differs by at most [maxDeviation] from [middle]
        public static float ClampAngle(float angle, float middle, float maxDeviation)
        {
            float rel = RelativeAngle(middle, angle);
            if (rel < maxDeviation || rel > 2 * Math.PI - maxDeviation)
            {
                return angle;
            }
            return rel > 0 ? middle + maxDeviation : middle - maxDeviation;
        }

        public readonly struct Transform
        {
            public readonly Distance x, y;
            public readonly double rotation;
            public readonly Vector2 Position { get { return new((float)x.Cells, (float)y.Cells); } }

            public Transform(Distance x, Distance y, double rotation)
            {
                this.x = x;
                this.y = y;
                this.rotation = rotation % (2 * Math.PI);
                if (this.rotation < 0)
                {
                    this.rotation += 2 * Math.PI;
                }
            }

            // assume[vector] is relative to[transform], and its units are cells. get its world position.
            public static Vector2 operator +(Transform transform, Vector2 vector)
            {
                float cos = (float)Math.Cos(transform.rotation), sin = (float)Math.Sin(transform.rotation);
                return new((float)transform.x.Cells + vector.X * cos - vector.Y * sin, (float)transform.y.Cells + vector.X * sin + vector.Y * cos);
            }

            // assume [second] is relative to [first]. get its world position.
            public static Transform operator +(Transform first, Transform second)
            {
                double cos = Math.Cos(first.rotation), sin = Math.Sin(first.rotation);
                return new Transform(first.x + second.x * cos - second.y * sin, first.y + second.x * sin + second.y * cos, first.rotation + second.rotation);
            }

            // assume [first] and [second] are world positions. Find a transform "x" such that [first] + "x" = [second] (give or take floating point inaccuracy)
            public static Transform operator -(Transform first, Transform second)
            {
                double cos = Math.Cos(first.rotation), sin = Math.Sin(first.rotation);
                Distance dx = second.x - first.x, dy = second.y - first.y;
                return new Transform((dx) * cos + (dy) * sin, (dx) * (-sin) + (dy) * cos, second.rotation - first.rotation);
            }
        }
    }
}
