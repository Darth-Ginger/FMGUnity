
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct NativeTriangle3D 
    {

        public float3 a;
        public float3 b;
        public float3 c;

        public NativeTriangle3D(float3 a, float3 b, float3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public float Area()
        {
            var dirA = this.b - this.a;
            var dirC = this.c - this.a;

            return math.length(math.cross(dirA, dirC)) * 0.5f;
        }

        public float3 GetNormal()
        {
            return math.normalize(math.cross(this.b - this.a, this.c - this.a));
        }

        public float3 GetCenter()
        {
            return (this.a + this.b + this.c) * 0.3333333f;
        }

        public float3 GetPointFromBarycentricCoordinates(float u, float v, float w)
        {
            return math.mad(u, this.a, math.mad(v, this.b, w * this.c));
        }

        public bool HasRightAngle(float threshold)
        {
            float3 dA = this.b - this.a;
            float3 dB = this.c - this.b;
            float3 dC = this.a - this.c;

            float3 sqrDist = new float3(math.lengthsq(dA), math.lengthsq(dB), math.lengthsq(dC));
            float3 hyp = sqrDist.yxx + sqrDist.zzy - sqrDist;
            bool3 thresholdTest = (hyp > -threshold) & (hyp < threshold);
            return math.any(thresholdTest);
        }

        public static void CalculateCircumcircle(NativeTriangle3D triangle, out float3 center, out float radiusSq, float mergeDistance = 10e-5f, float epsilon = 10e-5f)
        {
            var dirB = triangle.b - triangle.a;
            var dirC = triangle.c - triangle.a;
            var normal = triangle.GetNormal();

            var bisectionA = math.mad(dirB, 0.5f, triangle.a);
            var bisectionC = math.mad(dirC, 0.5f, triangle.a);

            var bisectionDirA = math.normalize(math.cross(normal, dirB));
            var bisectionDirC = math.normalize(math.cross(normal, dirC));

            var lA = new Line3D() { direction = bisectionDirA, point = bisectionA };
            var lB = new Line3D() { direction = bisectionDirC, point = bisectionC };

            //All points lie on a plane, so in theory this is safe
            ShapeIntersection.LineIntersection(lA, lB, out center, mergeDistance, epsilon);

            radiusSq = math.distancesq(center, triangle.a);
        }
    }
}
