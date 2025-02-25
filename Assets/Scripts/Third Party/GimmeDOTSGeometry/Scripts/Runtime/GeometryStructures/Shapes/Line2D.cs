using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    [Serializable]
    public struct Line2D
    {

        public float2 point;
        public float2 direction;

        public Line2D(float2 point, float2 direction)
        {
            this.point = point;
            this.direction = direction;
        }
        
        /// <summary>
        /// Returns true, if the given point is to the left of the line given its direction
        /// </summary>
        /// <param name="line"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool PointIsToTheLeft(Line2D line, float2 point)
        {
            float2 perp = line.direction.Perpendicular();
            return math.dot(point, perp) > 0;
        }
        
        public static float Distance(Line2D line, float2 point, float epsilon = 10e-5f)
        {
            if (math.abs(line.direction.x) < epsilon)
            {
                return math.abs(line.point.x - point.x);
            }
            else
            {
                float c = line.point.y - line.point.x * line.direction.y;
                float slope = line.direction.y / line.direction.x;

                float nominator = math.abs(slope * point.x + point.y - c);
                float denominator = math.sqrt(slope * slope + 1);

                return nominator / denominator;
            }
        }

    }
}
