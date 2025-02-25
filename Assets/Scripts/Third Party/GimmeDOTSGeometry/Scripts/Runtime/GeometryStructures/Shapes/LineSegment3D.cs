using System;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    [Serializable]
    public struct LineSegment3D : IEquatable<LineSegment3D>
    {
        public Vector3 a;
        public Vector3 b;

        public static readonly LineSegment3D positiveInfinite = new LineSegment3D() { a = Vector3.positiveInfinity, b = Vector3.positiveInfinity };
        public static readonly LineSegment3D negativeInfinite = new LineSegment3D() { a = Vector3.negativeInfinity, b = Vector3.negativeInfinity };



        public LineSegment3D(Vector3 a, Vector3 b)
        {
            this.a = a;
            this.b = b;
        }

        public float Length()
        {
            return math.distance(this.a, this.b);
        }

        public override string ToString()
        {
            string format = "{0:0.00}";
            return $"{string.Format(format, a.x)}, {string.Format(format, a.y)}, {string.Format(format, a.z)} | {string.Format(format, b.x)}, {string.Format(format, b.y)}, {string.Format(format, b.z)}";
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

        public bool Equals(LineSegment3D other)
        {
            return this.a.Equals(other.a) && this.b.Equals(other.b);
        }

        public float3 GetCenter()
        {
            return (this.a + this.b) * 0.5f;
        }
    }
}
