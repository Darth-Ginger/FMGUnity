using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class RectExtensions 
    {
        /// <summary>
        /// Returns the rectangle that encloses both this rectangle and another one
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static Rect CombineWith(this Rect rect, Rect other)
        {
            float2 min = math.min(rect.min, other.min);
            float2 max = math.max(rect.max, other.max);

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        /// <summary>
        /// Extends the rectangle by the specified amount to each side
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="amount"></param>
        public static void Expand(ref this Rect rect, float amount)
        {
            rect.xMax += amount;
            rect.xMin -= amount;
            rect.yMax += amount;
            rect.yMin -= amount;
        }


        public static float Area(this Rect rect)
        {
            var size = rect.size;
            return size.x * size.y;
        }

        public static float OverlapArea(this Rect rect0, Rect rect1)
        {
            if (!rect0.Overlaps(rect1)) return 0.0f;

            if (rect0.Contains(rect1)) return rect1.Area();
            if (rect1.Contains(rect0)) return rect0.Area();

            float2 rect0Min = rect0.min;
            float2 rect0Max = rect0.max;

            float2 rect1Min = rect1.min;
            float2 rect1Max = rect1.max;

            float2 min = math.max(rect0Min, rect1Min);
            float2 max = math.min(rect0Max, rect1Max);

            return (max.x - min.x) * (max.y - min.y);
        }

        /// <summary>
        /// Returns true if the other-rect is completely contained in the this-rect
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool Contains(this Rect rect, Rect other)
        {

            return rect.xMin <= other.xMin 
                && rect.xMax >= other.xMax 
                && rect.yMin <= other.yMin 
                && rect.yMax >= other.yMax;
        }


        /// <summary>
        /// Divides the rectangle into four smaller boundaries (like in a quadtree). The results
        /// are ordered in a Z-Curve 
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Rect[] Subdivide(this Rect rect)
        {
            var subRects = new Rect[4];

            var min = (float2)rect.min;
            var halfScale = (float2)(rect.size / 2.0f);

            var topLeftMin = (float2)min + new float2(0.0f, halfScale.y);
            var topLeftMax = topLeftMin + halfScale;

            var topRightMin = min + halfScale;
            var topRightMax = topRightMin + halfScale;

            var bottomLeftMin = min;
            var bottomLeftMax = bottomLeftMin + halfScale;

            var bottomRightMin = min + new float2(halfScale.x, 0.0f);
            var bottomRightMax = bottomRightMin + halfScale;

            subRects[0] = Rect.MinMaxRect(bottomLeftMin.x, bottomLeftMin.y, bottomLeftMax.x, bottomLeftMax.y);
            subRects[1] = Rect.MinMaxRect(bottomRightMin.x, bottomRightMin.y, bottomRightMax.x, bottomRightMax.y);
            subRects[2] = Rect.MinMaxRect(topLeftMin.x, topLeftMin.y, topLeftMax.x, topLeftMax.y);
            subRects[3] = Rect.MinMaxRect(topRightMin.x, topRightMin.y, topRightMax.x, topRightMax.y);

            return subRects;
        }



        /// <summary>
        /// Divides the rectangle into four smaller boundaries (like in a quadtree). The results
        /// are ordered in a Z-Curve 
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static void Subdivide(this Rect rect, ref NativeList<Rect> subdividedRectangles)
        {
            var min = (float2)rect.min;
            var halfScale = (float2)(rect.size / 2.0f);

            var topLeftMin = (float2)min + new float2(0.0f, halfScale.y);
            var topLeftMax = topLeftMin + halfScale;

            var topRightMin = min + halfScale;
            var topRightMax = topRightMin + halfScale;

            var bottomLeftMin = min;
            var bottomLeftMax = bottomLeftMin + halfScale;

            var bottomRightMin = min + new float2(halfScale.x, 0.0f);
            var bottomRightMax = bottomRightMin + halfScale;

            subdividedRectangles.Add(Rect.MinMaxRect(bottomLeftMin.x, bottomLeftMin.y, bottomLeftMax.x, bottomLeftMax.y));
            subdividedRectangles.Add(Rect.MinMaxRect(bottomRightMin.x, bottomRightMin.y, bottomRightMax.x, bottomRightMax.y));
            subdividedRectangles.Add(Rect.MinMaxRect(topLeftMin.x, topLeftMin.y, topLeftMax.x, topLeftMax.y));
            subdividedRectangles.Add(Rect.MinMaxRect(topRightMin.x, topRightMin.y, topRightMax.x, topRightMax.y));
        }

        public static Vector4 ToVector4(this Rect rect)
        {
            return new Vector4(rect.min.x, rect.min.y, rect.max.x, rect.max.y);
        }

        public static Vector2[] GetCorners(this Rect rect)
        {
            var corners = new Vector2[4];

            var min = rect.min;
            var max = rect.max;

            corners[0] = min;
            corners[1] = new Vector2(max.x, min.y);
            corners[2] = max;
            corners[3] = new Vector2(min.x, max.y);

            return corners;
        }
    }
}
