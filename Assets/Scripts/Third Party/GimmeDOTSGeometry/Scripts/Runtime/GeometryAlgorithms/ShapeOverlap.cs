using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class ShapeOverlap
    {

        /// <summary>
        /// returns true if the capsule and the sphere overlap - does not calculate intersection points or shapes
        /// </summary>
        /// <param name="capsule"></param>
        /// <param name="center"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool CapsuleSphereOverlap(Capsule capsule, float3 center, float radiusSq)
        {
            float3 a = capsule.a;
            float3 b = capsule.b;

            float distA = math.distance(a, center);
            float distB = math.distance(b, center);
            float radius = math.sqrt(radiusSq);

            if (distA <= radius + capsule.radius || distB <= radius + capsule.radius) return true;

            float3 aTocenter = center - a;
            float3 dir = b - a;

            float3 closestPoint = a + math.project(aTocenter, dir);
            float closestPointDist = math.distance(center, closestPoint);

            if (closestPointDist > radius + capsule.radius) return false;
            else
            {
                float capsuleRadiusSq = capsule.radius * capsule.radius;
                bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= -capsuleRadiusSq 
                    && math.dot(-dir, closestPoint - b) >= -capsuleRadiusSq;
                return closestPointIsOnSegment;
            }
        }

        /// <summary>
        /// Returns true if the line segment and the sphere overlap - does not calculate the intersection points
        /// </summary>
        /// <param name="line"></param>
        /// <param name="center"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool LineSegmentSphereOverlap(LineSegment3D line, float3 center, float radiusSq)
        {
            float3 a = line.a;
            float3 b = line.b;

            float distSqA = math.distancesq(a, center);
            float distSqB = math.distancesq(b, center);

            if (distSqA <= radiusSq || distSqB <= radiusSq) return true;
            
            float3 aToCenter = center - a;
            float3 dir = b - a;

            float3 closestPoint = a + math.project(aToCenter, dir);
            float closestPointDistSq = math.distancesq(center, closestPoint);

            //Line outside circle
            if (closestPointDistSq > radiusSq + 10e-7f) return false;
            else
            {
                bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= 0.0f && math.dot(-dir, closestPoint - b) >= 0.0f;
                return closestPointIsOnSegment;
            }
        }



        /// <summary>
        /// Returns true if the line segment and the cuboid overlap - does not calculate the intersection points
        /// </summary>
        /// <param name="line"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static bool LineSegmentCuboidOverlap(LineSegment3D line, Bounds bounds)
        {
            if (bounds.Contains(line.a) || bounds.Contains(line.b)) return true;

            float3 lsMin = math.min(line.a, line.b);
            float3 lsMax = math.max(line.a, line.b);

            Bounds lsBounds = new Bounds((lsMin + lsMax) * 0.5f, lsMax - lsMin);

            if (!bounds.Overlaps(lsBounds)) return false;

            float3 min = bounds.min;
            float3 max = bounds.max;

            if ((lsMin.x < min.x && lsMax.x < min.x)
                || (lsMin.y < min.y && lsMax.y < min.y)
                || (lsMin.z < min.z && lsMax.z < min.z)
                || (lsMin.x > max.x && lsMax.x > max.x)
                || (lsMin.y > max.y && lsMax.y > max.y)
                || (lsMin.z > max.z && lsMax.z > max.z))
            {
                return false;
            }

            return bounds.IntersectRay(new Ray() { direction = line.b - line.a, origin = line.a });
        }

        /// <summary>
        /// Returns true if the line segment and the circle overlap - does not calculate intersection points
        /// </summary>
        /// <param name="line"></param>
        /// <param name="center"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool LineSegmentCircleOverlap(LineSegment2D line, float2 center, float radiusSq)
        {
            float2 a = line.a;
            float2 b = line.b;

            float distSqA = math.distancesq(a, center);
            float distSqB = math.distancesq(b, center);

            if (distSqA <= radiusSq || distSqB <= radiusSq) return true;

            float2 aToCenter = center - a;
            float2 dir = b - a;

            float2 closestPoint = a + math.project(aToCenter, dir);
            float closestPointDistSq = math.distancesq(center, closestPoint);

            //Line outside circle
            if (closestPointDistSq > radiusSq + 10e-7f) return false;
            else
            {
                bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= 0.0f && math.dot(-dir, closestPoint - b) >= 0.0f;
                return closestPointIsOnSegment;
            }
        }

        /// <summary>
        /// Returns true if the line segment and the rectangle overlap - does not calculate intersection points
        /// </summary>
        /// <param name="line"></param>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static bool LineSegmentRectangleOverlap(LineSegment2D line, Rect rect)
        {
            if(rect.Contains(line.a) || rect.Contains(line.b)) return true;

            float2 lsMin = math.min(line.a, line.b);
            float2 lsMax = math.max(line.a, line.b);

            Rect lsRect = new Rect(lsMin.x, lsMin.y, lsMax.x - lsMin.x, lsMax.y - lsMin.y);

            if (!rect.Overlaps(lsRect)) return false;

            float2 min = rect.min;
            float2 max = rect.max;

            if ((lsMin.x < min.x && lsMax.x < min.x)
                || (lsMin.y < min.y && lsMax.y < min.y)
                || (lsMin.x > max.x && lsMax.x > max.x)
                || (lsMin.y > max.y && lsMax.y > max.y))
            {
                return false;
            }


            //At least one corner has to be left and one corner has to be right of the
            //line segment if it is intersecting

            float2 topLeft = new float2(min.x, max.y);
            float2 bottomRight = new float2(max.x, min.y);

            int leftCounter = 0;
            if (LineSegment2D.PointIsToTheLeft(line, min)) leftCounter++;
            if (LineSegment2D.PointIsToTheLeft(line, max)) leftCounter++;
            if (LineSegment2D.PointIsToTheLeft(line, topLeft)) leftCounter++;
            if (LineSegment2D.PointIsToTheLeft(line, bottomRight)) leftCounter++;

            return leftCounter > 0 && leftCounter < 4;
        }


        /// <summary>
        /// Returns true, if the second circle is completely contained within the first circle (used for Ball*-Tree 2D)
        /// </summary>
        /// <param name="circleCenter0"></param>
        /// <param name="radiusSq0"></param>
        /// <param name="circleCenter1"></param>
        /// <param name="radiusSq1"></param>
        /// <returns></returns>
        public static bool CircleContainsCircle(float2 circleCenter0, float radiusSq0, float2 circleCenter1, float radiusSq1)
        {
            //Even without the squares, we would have one sqrt call (because of calculating the actual length). I prefer this version
            //as squares for distance checks are quite handy in most of the algorithms in this asset
            float dirLength = math.lengthsq(circleCenter1 - circleCenter0);
            return dirLength + radiusSq1 + 2.0f * Mathf.Sqrt(dirLength * radiusSq1) <= radiusSq0;
        }

        /// <summary>
        /// Returns true, if the second sphere is completely bounded by the first sphere (used for Ball*-Tree 3D)
        /// </summary>
        /// <param name="sphereCenter0"></param>
        /// <param name="radiusSq0"></param>
        /// <param name="sphereCenter1"></param>
        /// <param name="radiusSq1"></param>
        /// <returns></returns>
        public static bool SphereContainsSphere(float3 sphereCenter0, float radiusSq0, float3 sphereCenter1, float radiusSq1)
        {
            float dirLength = math.lengthsq(sphereCenter1 - sphereCenter0);
            return dirLength + radiusSq1 + 2.0f * Mathf.Sqrt(dirLength * radiusSq1) <= radiusSq0;
        }
        
        /// <summary>
        /// Returns true if the circle is inside the rectangle (used for Ball*-Tree)
        /// </summary>
        /// <param name="rectangle"></param>
        /// <param name="circleCenter"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool RectangleContainsCircle(Rect rectangle, float2 circleCenter, float radiusSq)
        {
            var min = rectangle.min;
            var max = rectangle.max;

            float radius = math.sqrt(radiusSq);
            var circleMax = circleCenter + radius;
            var circleMin = circleCenter - radius;

            return math.all(circleMax <= max) && math.all(circleMin >= min);
        }



        /// <summary>
        /// Returns true if the rectangle is inside the circle (used for KD-Tree)
        /// </summary>
        /// <param name="circleCenter"></param>
        /// <param name="radiusSq"></param>
        /// <param name="rectangle"></param>
        /// <returns></returns>
        public static bool CircleContainsRectangle(float2 circleCenter, float radiusSq, Rect rectangle)
        {
            var bottomLeft = rectangle.min;
            var topRight = rectangle.max;

            var bottomRight = new Vector2(topRight.x, bottomLeft.y);
            var topLeft = new Vector2(bottomLeft.x, topRight.y);


            return math.distancesq(circleCenter, bottomLeft) <= radiusSq
                && math.distancesq(circleCenter, topRight) <= radiusSq
                && math.distancesq(circleCenter, bottomRight) <= radiusSq
                && math.distancesq(circleCenter, topLeft) <= radiusSq;
        }

        /// <summary>
        /// Returns true if the sphere is inside the cuboid (used for Ball*-Tree)
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="sphereCenter"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool CuboidContainsSphere(Bounds bounds, float3 sphereCenter, float radiusSq)
        {
            var min = bounds.min;
            var max = bounds.max;

            float radius = math.sqrt(radiusSq);
            var sphereMax = sphereCenter + radius;
            var sphereMin = sphereCenter - radius;

            return math.all(sphereMax <= max) && math.all(sphereMin >= min);
        }

        /// <summary>
        /// Returns true if the bounds (cuboid) are completely inside a sphere 
        /// </summary>
        /// <param name="sphereCenter"></param>
        /// <param name="radiusSq"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static bool SphereContainsCuboid(float3 sphereCenter, float radiusSq, float3 min, float3 max)
        {

            float3 bottomRightDown = new float3(max.x, min.y, min.z);
            float3 topLeftDown = new float3(min.x, min.y, max.z);
            float3 topRightDown = new float3(max.x, min.y, max.z);
            float3 bottomLeftUp = new float3(min.x, max.y, min.z);
            float3 bottomRightUp = new float3(max.x, max.y, min.z);
            float3 topLeftUp = new float3(min.x, max.y, max.z);

            return math.distancesq(sphereCenter, min) <= radiusSq
                && math.distancesq(sphereCenter, bottomRightDown) <= radiusSq
                && math.distancesq(sphereCenter, topLeftDown) <= radiusSq
                && math.distancesq(sphereCenter, topRightDown) <= radiusSq

                && math.distancesq(sphereCenter, max) <= radiusSq
                && math.distancesq(sphereCenter, bottomLeftUp) <= radiusSq
                && math.distancesq(sphereCenter, bottomRightUp) <= radiusSq
                && math.distancesq(sphereCenter, topLeftUp) <= radiusSq;
        }

        /// <summary>
        /// Returns true if the bounds (cuboid) are completely inside a sphere 
        /// </summary>
        /// <param name="sphereCenter"></param>
        /// <param name="radiusSq"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static bool SphereContainsCuboid(float3 sphereCenter, float radiusSq, Bounds bounds)
        {
            float3 min = bounds.min;
            float3 max = bounds.max;

            float3 bottomRightDown = new float3(max.x, min.y, min.z);
            float3 topLeftDown = new float3(min.x, min.y, max.z);
            float3 topRightDown = new float3(max.x, min.y, max.z);
            float3 bottomLeftUp = new float3(min.x, max.y, min.z);
            float3 bottomRightUp = new float3(max.x, max.y, min.z);
            float3 topLeftUp = new float3(min.x, max.y, max.z);

            return math.distancesq(sphereCenter, min) <= radiusSq
                && math.distancesq(sphereCenter, bottomRightDown) <= radiusSq
                && math.distancesq(sphereCenter, topLeftDown) <= radiusSq
                && math.distancesq(sphereCenter, topRightDown) <= radiusSq

                && math.distancesq(sphereCenter, max) <= radiusSq
                && math.distancesq(sphereCenter, bottomLeftUp) <= radiusSq
                && math.distancesq(sphereCenter, bottomRightUp) <= radiusSq
                && math.distancesq(sphereCenter, topLeftUp) <= radiusSq;
        }

        /// <summary>
        /// Returns true if the circles overlap each other
        /// </summary>
        /// <param name="circleCenter0"></param>
        /// <param name="radiusSq0"></param>
        /// <param name="circleCenter1"></param>
        /// <param name="radiusSq1"></param>
        /// <returns></returns>
        public static bool CircleCircleOverlap(float2 circleCenter0, float radiusSq0, float2 circleCenter1, float radiusSq1)
        {
            float dirLength = math.lengthsq(circleCenter1 - circleCenter0);
            return radiusSq0 + radiusSq1 + 2.0f * math.sqrt(radiusSq0 * radiusSq1) > dirLength;
        }

        /// <summary>
        /// Returns true if the spheres overlap each other
        /// </summary>
        /// <param name="sphereCenter0"></param>
        /// <param name="radiusSq0"></param>
        /// <param name="sphereCenter1"></param>
        /// <param name="radiusSq1"></param>
        /// <returns></returns>
        public static bool SphereSphereOverlap(float3 sphereCenter0, float radiusSq0, float3 sphereCenter1, float radiusSq1)
        {
            float dirLength = math.lengthsq(sphereCenter1 - sphereCenter0);
            return radiusSq0 + radiusSq1 + 2.0f * math.sqrt(radiusSq0 * radiusSq1) > dirLength;
        }

        /// <summary>
        /// Returns true if the rectangle and the circle are overlapping (used for KD-Trees, Ball*-Trees and R* Trees).
        /// 
        /// GPU: GGGOverlap
        /// </summary>
        /// <param name="rectangle"></param>
        /// <param name="circleCenter"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool RectangleCircleOverlap(Rect rectangle, float2 circleCenter, float radiusSq)
        {
            float2 min = rectangle.min;
            float2 max = rectangle.max;

            float2 closestPoint = math.max(min, math.min(circleCenter, max));

            return math.distancesq(circleCenter, closestPoint) <= radiusSq;
        }

        /// <summary>
        /// Returns true if the cuboid / bounds and the sphere are overlapping (used for KD-Trees, Ball*-Trees and R*-Trees)
        /// 
        /// GPU: GGGOverlap
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="sphereCenter"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool CuboidSphereOverlap(float3 min, float3 max, float3 sphereCenter, float radiusSq)
        {
            float3 closestPoint = math.max(min, math.min(sphereCenter, max));

            return math.distancesq(sphereCenter, closestPoint) <= radiusSq;
        }

        /// <summary>
        /// Returns true if the cuboid / bounds and the sphere are overlapping (used for KD-Trees, Ball*-Trees and R*-Trees)
        /// 
        /// GPU: GGGOverlap
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="sphereCenter"></param>
        /// <param name="radiusSq"></param>
        /// <returns></returns>
        public static bool CuboidSphereOverlap(Bounds bounds, float3 sphereCenter, float radiusSq)
        {
            float3 min = bounds.min;
            float3 max = bounds.max;
            
            float3 closestPoint = math.max(min, math.min(sphereCenter, max));

            return math.distancesq(sphereCenter, closestPoint) <= radiusSq;
        }

    }
}
