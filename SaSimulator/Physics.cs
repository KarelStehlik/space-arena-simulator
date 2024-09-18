using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Runtime.ConstrainedExecution;

namespace SaSimulator
{
    internal static class Physics
    {
        public struct WeightedRecord<T>(T item, float weight)
        {
            public readonly T item = item;
            public readonly float weight = weight;
        }

        public static T WeightedChoice<T>(List<WeightedRecord<T>> records, Random rng)
        {
            float roll = (float)rng.NextDouble() * records.Select(r => r.weight).Sum();
            foreach(WeightedRecord<T> record in records)
            {
                if(record.weight > roll)
                {
                    return record.item;
                }
                roll -= record.weight;
            }
            return records.Last().item;
        }

        public static float Square(float x)
        {
            return x * x;
        }

        // Angle between the 2 angles, normalized to 0-2PI
        private static float RelativeAngle(float firstAngle, float secondAngle)
        {
            return (float)((secondAngle - firstAngle + 8 * Math.PI) % (2 * Math.PI));
        }

        public static bool IsPointInCone(Vector2 point, Vector2 coneOrigin, float coneRotation, float coneAngle)
        {
            Vector2 difference = point - coneOrigin;
            float direction = (float)Math.Atan2(difference.Y, difference.X);
            float rel = RelativeAngle(direction, coneRotation);
            return rel < coneAngle / 2 || rel > 2 * Math.PI - coneAngle / 2;
        }

        public static bool ConeCircleIntersect(Vector2 circleCenter, float circleRadius, Transform coneOrigin, float coneAngle)
        {
            Vector2 center = circleCenter.RelativeTo(coneOrigin);
            float angle = (float)Math.Atan2(center.Y, center.X);
            float dist = center.Length();

            if(dist<circleRadius)
            {
                return true;
            }

            float maxAllowedAngle = coneAngle / 2 + (float)Math.Asin(circleRadius / dist);

            return angle < maxAllowedAngle && angle > -maxAllowedAngle;
        }

        // clamp an angle so that it differs by at most [maxDeviation] from [middle]
        public static float ClampAngle(float angle, float middle, float maxDeviation)
        {
            float rel = RelativeAngle(middle, angle);
            if (rel < maxDeviation || rel > 2 * Math.PI - maxDeviation)
            {
                return angle;
            }
            return rel < Math.PI ? middle + maxDeviation : middle - maxDeviation;
        }

        /// <summary>
        /// position and rotation.
        /// </summary>
        public readonly struct Transform
        {
            public readonly Distance x, y;
            public readonly float rotation;
            public readonly Vector2 Position { get { return new(x.Cells, y.Cells); } }

            public Transform(Distance x, Distance y, float rotation)
            {
                this.x = x;
                this.y = y;
                this.rotation = rotation % (2 * (float)Math.PI);
                if (this.rotation < 0)
                {
                    this.rotation += 2 * (float)Math.PI;
                }
            }

            // assume[vector] is relative to[transform], and its units are cells. get its world position.
            public static Vector2 operator +(Transform transform, Vector2 vector)
            {
                float cos = (float)Math.Cos(transform.rotation), sin = (float)Math.Sin(transform.rotation);
                return new(transform.x.Cells + vector.X * cos - vector.Y * sin, transform.y.Cells + vector.X * sin + vector.Y * cos);
            }

            // assume [second] is relative to [first]. get its world position.
            public static Transform operator +(Transform first, Transform second)
            {
                float cos = (float)Math.Cos(first.rotation), sin = (float)Math.Sin(first.rotation);
                return new Transform(first.x + second.x * cos - second.y * sin, first.y + second.x * sin + second.y * cos, first.rotation + second.rotation);
            }

            // assume [this] and [other] are world positions. Find a transform "x" such that [other] + "x" = [this] (give or take floating point inaccuracy)
            public Transform RelativeTo(Transform other)
            {
                float cos = (float)Math.Cos(other.rotation), sin = (float)Math.Sin(other.rotation);
                Distance dx = x - other.x, dy = y - other.y;
                return new Transform(dx * cos + dy * sin, dx * (-sin) + dy * cos, rotation - other.rotation);
            }
        }
    }

    internal static class PhysicsExtensions
    {
        // assume [first] and [other] are world positions. Find the position of [first] relative to [other]
        public static Vector2 RelativeTo(this Vector2 first, Physics.Transform other)
        {
            float cos = (float)Math.Cos(other.rotation), sin = (float)Math.Sin(other.rotation);
            float dx = first.X - other.x.Cells, dy = first.Y - other.y.Cells;
            return new Vector2(dx * cos + dy * sin, dx * (-sin) + dy * cos);
        }
    }
}
