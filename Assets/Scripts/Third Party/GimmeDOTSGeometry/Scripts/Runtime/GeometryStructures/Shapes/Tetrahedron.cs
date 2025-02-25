using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct Tetrahedron

    {
        public float3 a;
        public float3 b;
        public float3 c;
        public float3 d;

        public Tetrahedron(float3 a, float3 b, float3 c, float3 d)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
        }

        public float Area()
        {
            var triangleA = new NativeTriangle3D(this.a, this.b, this.c);
            var triangleB = new NativeTriangle3D(this.a, this.b, this.d);
            var triangleC = new NativeTriangle3D(this.a, this.c, this.d);
            var triangleD = new NativeTriangle3D(this.b, this.c, this.d);

            return triangleA.Area() + triangleB.Area() + triangleC.Area() + triangleD.Area();
        }

        public float Volume()
        {
            var triangleA = new NativeTriangle3D(this.a, this.b, this.c);

            var dirA = this.b - this.a;
            var dirB = this.c - this.a;

            var perp = math.cross(dirA, dirB);

            var dirD = this.d - this.a;

            float height = math.length(math.project(dirD, perp));

            return (triangleA.Area() * height) / 3.0f;
        }

        public float3 GetCenter()
        {
            return (this.a + this.b + this.c + this.d) * 0.25f;
        }

        public static void CalculateCircumsphere(Tetrahedron tetrahedron, out float3 center, out float radiusSq, float mergeDistance = 10e-5f, float epsilon = 10e-5f)
        {
            var dirB = tetrahedron.b - tetrahedron.a;
            var dirC = tetrahedron.c - tetrahedron.a;
            var dirD = tetrahedron.d - tetrahedron.a;

            var bisectionA = math.mad(dirB, 0.5f, tetrahedron.a);
            var bisectionB = math.mad(dirC, 0.5f, tetrahedron.a);
            var bisectionC = math.mad(dirD, 0.5f, tetrahedron.a);

            var bisectionPlaneA = new Plane(dirB, VectorUtil.ScalarProjection(bisectionA, math.normalize(dirB)));
            var bisectionPlaneB = new Plane(dirC, VectorUtil.ScalarProjection(bisectionB, math.normalize(dirC)));
            var bisectionPlaneC = new Plane(dirD, VectorUtil.ScalarProjection(bisectionC, math.normalize(dirD)));

            //The planes are never parallel in a valid tetrahedon (without infinities and NaNs)
            ShapeIntersection.PlaneIntersection(bisectionPlaneA, bisectionPlaneB, out var intersectionLine, mergeDistance, epsilon);
            ShapeIntersection.PlaneLineIntersection(bisectionPlaneC, intersectionLine, out center, epsilon);

            radiusSq = math.distancesq(center, tetrahedron.a);
        }
    }
}
