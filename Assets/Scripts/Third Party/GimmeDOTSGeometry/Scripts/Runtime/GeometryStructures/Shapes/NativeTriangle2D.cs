using Unity.Mathematics;
using UnityEngine;


namespace GimmeDOTSGeometry
{
    public struct NativeTriangle2D
    {
        public float2 a;
        public float2 b;
        public float2 c;

        public NativeTriangle2D(float2 a, float2 b, float2 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public float Determinant()
        {
            var dirA = this.b - this.a;
            var dirB = this.c - this.a;

            return dirA.x * dirB.y - dirA.y * dirB.x;
        }

        public float Area()
        {
            return this.Determinant() * 0.5f;
        }

        public Rect GetBounds()
        {
            float2 min = math.min(math.min(this.a, this.b), this.c);
            float2 max = math.max(math.max(this.a, this.b), this.c);
            return new Rect(min, max - min);
        }
        
        //https://stackoverflow.com/questions/2049582/how-to-determine-if-a-point-is-in-a-2d-triangle
        public bool IsPointInside(float2 point)
        {
            float3 xVal0 = new float3(this.a.x, this.b.x, this.c.x);
            float3 yVal0 = new float3(this.a.y, this.b.y, this.c.y);

            float3 xVal1 = xVal0.zxy;
            float3 yVal1 = yVal0.zxy;

            float3 yDiff = point.yyy - yVal1;
            float3 xDiff = point.xxx - xVal1;

            float3 p = (xVal0 - xVal1) * yDiff - (yVal0 - yVal1) * xDiff;

            if ((p.x < 0) != (p.y < 0) && math.all(p.xy != 0)) return false;

            return p.z == 0 || (p.z < 0) == (p.x + p.y <= 0);
        }

        //https://gamedev.stackexchange.com/questions/23743/whats-the-most-efficient-way-to-find-barycentric-coordinates
        public float3 CalculateBarycentricCoordinates(float2 point)
        {
            float2 dirA = this.b - this.a;
            float2 dirB = this.c - this.a;
            float2 dirP = point - this.a;

            float d00 = math.dot(dirA, dirA);
            float d01 = math.dot(dirA, dirB);
            float d11 = math.dot(dirB, dirB);
            float d20 = math.dot(dirP, dirA);
            float d21 = math.dot(dirP, dirB);
            float denom = d00 * d11 - d01 * d01;

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;

            return new float3(u, v, w);
        }

        /// <summary>
        /// Returns true if the triangle contains a right angle (within a threshold)
        /// </summary>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public bool HasRightAngle(float threshold)
        {
            float2 dA = this.b - this.a;
            float2 dB = this.c - this.b;
            float2 dC = this.a - this.c;

            float3 sqrDist = new float3(math.lengthsq(dA), math.lengthsq(dB), math.lengthsq(dC));
            float3 hyp = sqrDist.yxx + sqrDist.zzy - sqrDist;
            bool3 thresholdTest = (hyp > -threshold) & (hyp < threshold);
            return math.any(thresholdTest);
        }

        public float2 GetCenter()
        {
            return (this.a + this.b + this.c) * 0.3333333f;
        }

        public float2 GetPointFromBarycentricCoordinates(float u, float v, float w)
        {
            return math.mad(u, this.a, math.mad(v, this.b, w * this.c));
        }

        public int GetNearestEdge(float2 point)
        {
            float2 edgeA = this.b - this.a;
            float2 edgeB = this.c - this.b;
            float2 edgeC = this.a - this.c;

            float2 dirA = point - this.a;
            float2 dirB = point - this.b;
            float2 dirC = point - this.c;

            float2 orthoA = new float2(-edgeA.y, edgeA.x);
            float2 orthoB = new float2(-edgeB.y, edgeB.x);
            float2 orthoC = new float2(-edgeC.y, edgeC.x);

            float3 dot = new float3(math.dot(orthoA, dirA), math.dot(orthoB, dirB), math.dot(orthoC, dirC));

            bool3 test = dot < 0.0f;
            if (test.x) return 0;
            else if (test.y) return 1;
            else if (test.z) return 2;
            return -1;
        }



        /// <summary>
        /// Calculates the circle that circumscribes all three points of the triangle
        /// </summary>
        /// <param name="triangle"></param>
        /// <param name="center"></param>
        /// <param name="radiusSq"></param>
        public static void CalculateCircumcircle(NativeTriangle2D triangle, out float2 center, out float radiusSq)
        {
            var dirB = triangle.b - triangle.a;
            var dirC = triangle.c - triangle.a;

            var bisectionA = math.mad(dirB, 0.5f, triangle.a);
            var bisectionC = math.mad(dirC, 0.5f, triangle.a);

            var bisectionDirA = new float2(-dirB.y, dirB.x);
            var bisectionDirC = new float2(-dirC.y, dirC.x);

            var lA = new Line2D() { direction = bisectionDirA, point = bisectionA };
            var lB = new Line2D() { direction = bisectionDirC, point = bisectionC };

            ShapeIntersection.LineIntersection(lA, lB, out center);
            radiusSq = math.distancesq(center, triangle.a);
        }

        /// <summary>
        /// Returns true, if the point is inside the circumcircle of the triangle. Only works if the
        /// triangle is sorted counter clockwise
        /// </summary>
        /// <param name="triangle"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        //You can find the matrix for which the determinant is calculated here:
        //https://en.wikipedia.org/wiki/Delaunay_triangulation
        public static bool IsInsideTriangleCircumcircle(NativeTriangle2D triangle, float2 point)
        {
            var dirA = triangle.a - point;
            var dirB = triangle.b - point;
            var dirC = triangle.c - point;

            float dotDirA = math.dot(dirA, dirA);
            float dotDirB = math.dot(dirB, dirB);
            float dotDirC = math.dot(dirC, dirC);

            float a = dotDirA * (dirB.x * dirC.y - dirB.y * dirC.x);
            float b = dotDirB * (dirA.y * dirC.x - dirA.x * dirC.y);
            float c = dotDirC * (dirA.x * dirB.y - dirA.y * dirB.x);

            return a + b + c > 0;
        }

    }
}
