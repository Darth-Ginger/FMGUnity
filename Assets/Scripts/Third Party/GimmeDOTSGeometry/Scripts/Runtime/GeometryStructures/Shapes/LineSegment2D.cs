using System;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// A simple struct representing a segment of a 2D Line.
    /// GPU: GGGShapes
    /// </summary>
    [Serializable]
    public struct LineSegment2D : IEquatable<LineSegment2D>
    {
        public Vector2 a;
        public Vector2 b;

        public static readonly LineSegment2D positiveInfinite = new LineSegment2D() { a = Vector2.positiveInfinity, b = Vector2.positiveInfinity };
        public static readonly LineSegment2D negativeInfinite = new LineSegment2D() { a = Vector2.negativeInfinity, b = Vector2.negativeInfinity };


        public LineSegment2D(Vector2 a, Vector2 b)
        {
            this.a = a;
            this.b = b;
        }

        public float Length()
        {
            return math.distance(this.a, this.b);
        }

        /// <summary>
        /// Returns true, if the given point is to the left of the line given its direction
        /// </summary>
        /// <param name="line"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool PointIsToTheLeft(LineSegment2D ls, float2 point)
        {
            float2 dir = ls.b - ls.a;
            float2 perp = dir.Perpendicular();
            return math.dot(point - (float2)ls.a, perp) > 0;
        }

        public static float Distance(LineSegment2D ls, float2 point)
        {
            float2 dir = ls.b - ls.a;
            float2 pointDir = point - (float2)ls.a;

            float dist = math.length(pointDir);
            dist = math.min(dist, math.distance(point, ls.b));

            float scalar = VectorUtil.ScalarProjection(pointDir, dir);
            if(scalar >= 0.0f && scalar <= 1.0f)
            {
                float2 linePoint = (float2)ls.a + scalar * dir;
                dist = math.min(dist, math.distance(linePoint, point));
            }
            return dist;
        }

        public float2 GetCenter()
        {
            return (this.a + this.b) * 0.5f;
        }

        public override string ToString()
        {
            string format = "{0:0.00}";
            return $"{string.Format(format, a.x)}, {string.Format(format, a.y)} | {string.Format(format, b.x)}, {string.Format(format, b.y)}";
        }

        public override int GetHashCode()
        {
            int hash = 5381;

            int hashA = this.a.GetHashCode();
            int hashB = this.b.GetHashCode();

            hash = ((int)(math.rol((uint)hash, 5) | math.ror((uint)hash, 27)) + hash) ^ hashA;
            hash = ((int)(math.rol((uint)hash, 5) | math.ror((uint)hash, 27)) + hash) ^ hashB;

            return hash;
        }

        public bool Equals(LineSegment2D other)
        {
            return this.a.Equals(other.a) && this.b.Equals(other.b);
        }
    }
}
