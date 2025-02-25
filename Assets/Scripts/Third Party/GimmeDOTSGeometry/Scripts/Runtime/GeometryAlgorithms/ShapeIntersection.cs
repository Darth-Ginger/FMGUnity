using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    // If the number of shapes supported is n - then the number of intersection methods for each class would
    // be (n - 1).
    // As I do not want to clutter each struct with similar methods (LineCircle-Intersection in Line-struct,
    // (CircleLine-Intersection in Circle-struct), everything is in static methods now
    public static class ShapeIntersection
    {

        public static bool LineLineSegmentIntersection(Line2D l0, LineSegment2D l1, out float2 intersection, float epsilon = 10e-5f)
        {
            var dir = l1.b - l1.a;
            Line2D tmpL1 = new Line2D(l1.a, dir);
            if (LineIntersection(l0, tmpL1, out intersection, epsilon))
            {
                float length = math.lengthsq(dir);
                var dirToIntersection0 = intersection - (float2)l1.a;
                if ((math.dot(dir, dirToIntersection0) > epsilon && math.lengthsq(dirToIntersection0) <= length + epsilon)
                    || math.all(intersection == (float2)l1.a))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool LineIntersection(Line2D l0, Line2D l1, out float2 intersection, float epsilon = 10e-5f)
        {
            intersection = float.NaN;

            float2 diff0 = l1.direction;
            float2 diff1 = l0.point - l1.point;
            float2 diff2 = l0.direction;

            float nom0 = diff1.x * diff0.y - diff1.y * diff0.x;
            float denom = diff2.x * diff0.y - diff2.y * diff0.x;

            //Parallel
            if (math.abs(denom) < epsilon) return false;

            intersection = math.mad(nom0 / denom, -l0.direction, l0.point);
            return true;
        }

        public static bool LineIntersection(Line3D l0, Line3D l1, out float3 intersection, float mergeDistance = 10e-5f, float epsilon = 10e-5f)
        {
            intersection = float.NaN;

            //Parallel
            if (1.0f - math.abs(math.dot(l0.direction, l1.direction)) < epsilon) return false;

            float3 normalDir = math.cross(l0.direction, l1.direction);
            float3 pointDir = l1.point - l0.point;

            float dist = VectorUtil.ScalarProjection(pointDir, math.normalize(normalDir));
            if (math.abs(dist) > mergeDistance) return false;
            else
            {
                float s = (math.dot(math.cross(pointDir, l1.direction), normalDir)) / math.lengthsq(normalDir);
                intersection = l0.point + s * l0.direction;
                return true;
            }
            
        }

        /// <summary>
        /// Intersects two line segments with each other. If they are parallel, false is returned and the intersection
        /// is invalid (NaN). 
        /// 
        /// GPU: GGGIntersecton
        /// </summary>
        /// <param name="s0"></param>
        /// <param name="s1"></param>
        /// <param name="intersection"></param>
        /// <returns></returns>
        public static bool LineSegmentIntersection(LineSegment2D s0, LineSegment2D s1, out float2 intersection)
        {
            float2 diff0 = s1.a - s1.b;
            float2 diff1 = s0.a - s1.a;
            float2 diff2 = s0.a - s0.b;

            float nom0 = diff1.x * diff0.y - diff1.y * diff0.x;
            float nom1 = diff1.x * diff2.y - diff1.y * diff2.x;
            float denom = diff2.x * diff0.y - diff2.y * diff0.x;
            float2 par = new float2(nom0, nom1) / denom;

            if (math.all(par >= 0.0f) && math.all(par <= 1.0f))
            {
                intersection = math.mad(par.x, s0.b - s0.a, s0.a);
                return true;
            }

            intersection = float.NaN;
            return false;
        }

        //https://math.stackexchange.com/questions/475953/how-to-calculate-the-intersection-of-two-planes
        public static bool PlaneIntersection(Plane p0, Plane p1, out Line3D intersectionLine, float mergeDistance = 10e-5f, float epsilon = 10e-5f)
        {
            intersectionLine = Line3D.Invalid;
            //Planes are parallel to each other
            if (1.0f - math.abs(math.dot(p0.normal, p1.normal)) < epsilon) return false;

            //Not going to take long before I will have to start writing a linear equation solver again... (in jobs...)
            //Gauss-Jordan, I am coming... but not today!
            var pointOnPlane0 = p0.normal * p0.distance;
            var pointOnPlane1 = p1.normal * p1.distance;

            var direction = math.cross(p0.normal, p1.normal);

            var perpL0Dir = math.normalize(math.cross(direction, p0.normal));
            var perpL1Dir = math.normalize(math.cross(direction, p1.normal));

            var perpL0 = new Line3D(pointOnPlane0, perpL0Dir);
            var perpL1 = new Line3D(pointOnPlane1, perpL1Dir);

            //Not mentioned in the math stackexchange... but... this only, ONLY works with the points on the plane defined by the
            //normals. Otherwise, the lines might simply not meet and intersect.
            //(the points right now lie on the sphere centered at the origin - any other point is not
            //- this means they are also displaced radially with the plane distance, which enables this to work)
            bool intersected = LineIntersection(perpL0, perpL1, out float3 intersectionPoint, mergeDistance, epsilon);

            intersectionLine = new Line3D(intersectionPoint, math.normalize(direction));
            return intersected;
        }

        public static bool PlaneLineIntersection(Plane p0, Line3D l0, out float3 intersection, float epsilon = 10e-5f)
        {
            intersection = float.NaN;
            //Plane and line are parallel
            float scale = math.dot(l0.direction, p0.normal);
            if (math.abs(scale) < epsilon) return false;

            var pointOnPlane = p0.normal * p0.distance;
            var diff = (float3)pointOnPlane - l0.point;

            //Make sure l0.direction is normalized, or this won't work
            float height = math.dot(diff, p0.normal);
            intersection = l0.point + (height / scale) * l0.direction;
            return true;
        }

        /// <summary>
        /// Calculates the intersection between a line segment and a plane. False is returned if they do not intersect.
        /// 
        /// GPU: GGGIntersection
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="l0"></param>
        /// <param name="intersection"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static bool PlaneLineSegmentIntersection(Plane p0, LineSegment3D l0, out float3 intersection, float epsilon = 10e-5f)
        {
            intersection = float.NaN;
            float3 dir = l0.b - l0.a;
            float3 normalizedDir = math.normalize(dir);

            float scale = math.dot(normalizedDir, p0.normal);
            var pointOnPlane = p0.normal * p0.distance;
            var diff = (float3)(pointOnPlane - l0.a);
            float height = math.dot(diff, p0.normal);

            //If the point lies on the plane - return it (regardless of the ls direction)
            if(math.abs(height) < epsilon)
            {
                intersection = l0.a;
                return true;
            }
            if (math.abs(scale) < epsilon) return false;

            float dist = (height / scale);
            if (dist >= -epsilon && dist * dist <= math.lengthsq(dir) + epsilon)
            {
                intersection = (float3)l0.a + dist * normalizedDir;
                return true;
            }
            return false;
        }

        public static FixedList128Bytes<float3> PlaneCuboidIntersections(Plane p0, Bounds bounds, float epsilon = 10e-7f)
        {
            FixedList128Bytes<float3> intersections = new FixedList128Bytes<float3>();

            var corners = bounds.GetCornerPoints();

            FixedList512Bytes<LineSegment3D> segments = new FixedList512Bytes<LineSegment3D>
            {
                new LineSegment3D() { a = corners[0], b = corners[1] },
                new LineSegment3D() { a = corners[0], b = corners[2] },
                new LineSegment3D() { a = corners[0], b = corners[4] },

                new LineSegment3D() { a = corners[7], b = corners[3] },
                new LineSegment3D() { a = corners[7], b = corners[6] },
                new LineSegment3D() { a = corners[7], b = corners[5] },

                new LineSegment3D() { a = corners[1], b = corners[3] },
                new LineSegment3D() { a = corners[1], b = corners[5] },

                new LineSegment3D() { a = corners[4], b = corners[6] },
                new LineSegment3D() { a = corners[4], b = corners[5] },

                new LineSegment3D() { a = corners[2], b = corners[3] },
                new LineSegment3D() { a = corners[2], b = corners[6] },
            };

            for (int i = 0; i < segments.Length; i++)
            {
                if(PlaneLineSegmentIntersection(p0, segments[i], out float3 intersection, epsilon)) {
                    intersections.Add(intersection);
                }
            }

            return intersections;
        }

        /// <summary>
        /// Returns the intersections between a line segment and a box / cuboid. The number of intersections
        /// may be 0, 1 or 2. If the line segment is completely contained inside the box, inside is set to
        /// true.
        /// 
        /// GPU: GGGIntersection
        /// </summary>
        /// <param name="line"></param>
        /// <param name="bounds"></param>
        /// <param name="intersection0"></param>
        /// <param name="intersection1"></param>
        /// <param name="inside"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static int LineSegmentCuboidIntersections(LineSegment3D line, Bounds bounds, 
            out float3 intersection0, out float3 intersection1, out bool inside, float epsilon = 10e-7f)
        {
            int intersections = 0;
            inside = false;

            intersection0 = float3.zero;
            intersection1 = float3.zero;

            if (bounds.Contains(line.a) && bounds.Contains(line.b))
            {
                inside = true;
                return intersections;
            }

            float3 forwardDir = math.normalize(line.b - line.a);
            float3 backwardDir = -forwardDir;

            Ray forwardRay = new Ray()
            {
                direction = forwardDir,
                origin = line.a
            };
            Ray backwardRay = new Ray()
            {
                direction = backwardDir,
                origin = line.b
            };

            
            if(bounds.IntersectRay(forwardRay, out float forwardDist))
            {
                intersection0 = (float3)line.a + forwardDir * forwardDist;
                intersections++;
            }

            if(bounds.IntersectRay(backwardRay, out float backwardDist))
            {
                intersection1 = (float3)line.b + backwardDir * backwardDist;
                if (intersections == 0 || math.distance(intersection1, intersection0) < epsilon)
                {
                    intersections++;
                }
            }
            return intersections;
        }

        /// <summary>
        /// Intersects a line segment with a sphere. There are 0, 1 or 2 possible points they cross. 
        /// The number of intersections is returned. If the number of intersections is 1, only the
        /// first intersection point returned is valid (the second one will contain invalid data).
        /// 
        /// If the line segment is completely contained within the sphere, inside is set to true.
        /// 
        /// GPU: GGGIntersection
        /// </summary>
        /// <param name="line"></param>
        /// <param name="center"></param>
        /// <param name="radiusSq"></param>
        /// <param name="intersection0"></param>
        /// <param name="intersection1"></param>
        /// <returns></returns>
        public static int LineSegmentSphereIntersections(LineSegment3D line, float3 center, float radiusSq, out float3 intersection0, out float3 intersection1, out bool inside)
        {
            float3 a = line.a;
            float3 b = line.b;

            float distSqA = math.distancesq(a, center);
            float distSqB = math.distancesq(b, center);

            intersection0 = float3.zero;
            intersection1 = float3.zero;

            if (distSqA < radiusSq && distSqB < radiusSq)
            {
                inside = true;
                return 0;
            }

            inside = false;

            float3 aToCenter = center - a;
            float3 dir = b - a;

            float3 closestPoint = a + math.project(aToCenter, dir);
            float closestPointDistSq = math.distancesq(center, closestPoint);

            if (closestPointDistSq > radiusSq + 10e-7f) return 0;
            else if (closestPointDistSq >= radiusSq - 10e-7f && closestPointDistSq <= radiusSq + 10e-7f)
            {
                intersection0 = closestPoint;
                bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= 0.0f && math.dot(-dir, closestPoint - b) >= 0.0f;
                return closestPointIsOnSegment ? 1 : 0;
            }
            else
            {
                //Pythagoras and Euclid - hand in hand
                //(I am glad I did not skip book III)
                //(I.e. the line towards the center of a sphere that bisects another line through the closest plane to the center, creates right angles... so to speak)
                float distFromClosestPointSq = radiusSq - closestPointDistSq;
                float distFromClosestPoint = math.sqrt(distFromClosestPointSq);

                float3 normalizedDir = math.normalize(dir);

                //A inside - B outside
                if (distSqA < radiusSq && distSqB >= radiusSq)
                {
                    intersection0 = closestPoint + normalizedDir * distFromClosestPoint;
                    return 1;
                }
                //B inside - A outside
                else if (distSqB < radiusSq && distSqA >= radiusSq)
                {
                    intersection0 = closestPoint - normalizedDir * distFromClosestPoint;
                    return 1;
                }
                //A and B outside
                else
                {
                    intersection0 = closestPoint + normalizedDir * distFromClosestPoint;
                    intersection1 = closestPoint - normalizedDir * distFromClosestPoint;
                    bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= 0.0f && math.dot(-dir, closestPoint - b) >= 0.0f;
                    return closestPointIsOnSegment ? 2 : 0;
                }
            }
        }

        /// <summary>
        /// Intersects a line segment with a triangle. If they do not intersect, false is returned. Otherwise, true is returned
        /// and the out parameter intersection holds the position where.
        /// </summary>
        /// <param name="ls"></param>
        /// <param name="triangle"></param>
        /// <param name="intersection"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static bool LineSegmentTriangleIntersection(LineSegment3D ls, NativeTriangle3D triangle, out float3 intersection, float epsilon = 10e-7f) 
        {
            float3 normal = triangle.GetNormal();
            var plane = new Plane(normal, triangle.a);
            var dir = ls.b - ls.a;
            float dirLengthSq = dir.sqrMagnitude;
            var ray = new Ray()
            {
                origin = ls.a,
                direction = ls.b - ls.a,
            };

            intersection = float.NaN;
            if(plane.Raycast(ray, out float dist))
            {
                if(dist * dist < dirLengthSq)
                {
                    intersection = ls.a + dir.normalized * dist;

                    float3 perpAB = math.cross(normal, triangle.b - triangle.a);
                    float3 perpBC = math.cross(normal, triangle.c - triangle.b);
                    float3 perpCA = math.cross(normal, triangle.a - triangle.c);

                    float3 dirAI = intersection - triangle.a;
                    float3 dirBI = intersection - triangle.b;
                    float3 dirCI = intersection - triangle.c;

                    return math.dot(perpAB, dirAI) >= -epsilon
                        && math.dot(perpBC, dirBI) >= -epsilon
                        && math.dot(perpCA, dirCI) >= -epsilon;
                }
            }
            return false;
        }

        /// <summary>
        /// Intersects a line with a rectangle. There are 0, 1 or 2 possible points the line can cross.
        /// The number of intersections is returned. If the number is 1, only the first
        /// intersection point returned is valid (the second one will contain invalid data).
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <param name="rect"></param>
        /// <param name="intersection0"></param>
        /// <param name="intersection1"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static int LineRectangleIntersections(Line2D line, Rect rect, out float2 intersection0, out float2 intersection1, float epsilon = 10e-5f)
        {
            intersection0 = float2.zero;
            intersection1 = float2.zero;

            var min = (float2)rect.min;
            var max = (float2)rect.max;

            var ra = min;
            var rb = new float2(max.x, min.y);
            var rc = max;
            var rd = new float2(min.x, max.y);

            var l0 = new LineSegment2D(ra, rb);
            var l1 = new LineSegment2D(rb, rc);
            var l2 = new LineSegment2D(rc, rd);
            var l3 = new LineSegment2D(rd, ra);

            bool i0Exists = LineLineSegmentIntersection(line, l0, out var i0);
            bool i1Exists = LineLineSegmentIntersection(line, l1, out var i1);
            bool i2Exists = LineLineSegmentIntersection(line, l2, out var i2);
            bool i3Exists = LineLineSegmentIntersection(line, l3, out var i3);

            //Note: On the diagonal, we might hit up to all four line segments at their corner points
            //      which is why we have to compare it with the first hit point and see if they are approximately equal
            int intersections = 0;
            if (i0Exists)
            {
                intersection0 = i0;
                intersections++;
            }
            if (i1Exists)
            {
                if (intersections == 0)
                {
                    intersection0 = i1;
                    intersections++;
                }
                //Removes double counting of the corners
                else if (math.any(math.abs(i1 - intersection0) > epsilon))
                {
                    intersection1 = i1;
                    intersections++;
                }
            }
            if (i2Exists)
            {
                if (intersections == 0)
                {
                    intersection0 = i2;
                    intersections++;
                }
                else if (math.any(math.abs(i2 - intersection0) > epsilon))
                {
                    intersection1 = i2;
                    intersections++;
                }
            }
            if (i3Exists)
            {
                if (intersections == 0)
                {
                    intersection0 = i3;
                    intersections++;
                }
                else if (math.any(math.abs(i3 - intersection0) > epsilon))
                {
                    intersection1 = i3;
                    intersections++;
                }
            }


            return math.clamp(intersections, 0, 2);
            
        }

        /// <summary>
        /// Intersects a line segment with a rectangle. There are 0, 1 or 2 possible points the line can cross.
        /// The number of intersections is returned. If the number is 1, only the first
        /// intersection point returned is valid (the second one will contain invalid data).
        /// 
        /// If the line segment is completely contained within the rectangle, inside is set to true.
        /// 
        /// GPU: GGGIntersection
        /// </summary>
        /// <param name="line"></param>, flo
        /// <param name="rect"></param>
        /// <param name="intersection0"></param>
        /// <param name="intersection1"></param>
        /// <param name="inside"></param>
        /// <returns></returns>
        public static int LineSegmentRectangleIntersections(LineSegment2D line, Rect rect, out float2 intersection0, out float2 intersection1, out bool inside, float epsilon = 10e-5f)
        {
            var a = line.a;
            var b = line.b;

            var min = rect.min;
            var max = rect.max;

            intersection0 = float2.zero;
            intersection1 = float2.zero;

            if((a.x < min.x && b.x < min.x)
                || (a.y < min.y && b.y < min.y)
                || (a.x > max.x && b.x > max.y)
                || (a.y > max.y && b.y > max.y))
            {
                inside = false;
                return 0;
            }

            bool aInside = rect.Contains(a);
            bool bInside = rect.Contains(b);

            if(aInside && bInside)
            {
                inside = true;
                return 0;
            }

            inside = false;

            var ra = min;
            var rb = new float2(max.x, min.y);
            var rc = max;
            var rd = new float2(min.x, max.y);

            var l0 = new LineSegment2D(ra, rb);
            var l1 = new LineSegment2D(rb, rc);
            var l2 = new LineSegment2D(rc, rd);
            var l3 = new LineSegment2D(rd, ra);

            bool i0Exists = LineSegmentIntersection(line, l0, out var i0);
            bool i1Exists = LineSegmentIntersection(line, l1, out var i1);
            bool i2Exists = LineSegmentIntersection(line, l2, out var i2);
            bool i3Exists = LineSegmentIntersection(line, l3, out var i3);

            if (aInside || bInside)
            {
                //Only one of them can exist
                if (i0Exists) intersection0 = i0;
                if (i1Exists) intersection0 = i1;
                if (i2Exists) intersection0 = i2;
                if (i3Exists) intersection0 = i3;

                return 1;
            }
            else
            {
                //Number of intersections is either 0 or 2
                //Note: On the diagonal, we might hit up to all four line segments at their corner points
                //      which is why we have to compare it with the first hit point and see if they are approximately equal
                int intersections = 0;

                if (i0Exists)
                {
                    intersection0 = i0;
                    intersections++;
                }
                if(i1Exists)
                {
                    if (intersections == 0)
                    {
                        intersection0 = i1;
                        intersections++;
                    }
                    //Removes double counting of the corners
                    else if (math.any(math.abs(i1 - intersection0) > epsilon))
                    {
                        intersection1 = i1;
                        intersections++;
                    }
                }
                if(i2Exists)
                {
                    if (intersections == 0)
                    {
                        intersection0 = i2;
                        intersections++;
                    }
                    else if (math.any(math.abs(i2 - intersection0) > epsilon))
                    {
                        intersection1 = i2;
                        intersections++;
                    }
                }
                if(i3Exists)
                {
                    if (intersections == 0)
                    {
                        intersection0 = i3;
                        intersections++;
                    }
                    else if (math.any(math.abs(i3 - intersection0) > epsilon))
                    {
                        intersection1 = i3;
                        intersections++;
                    }
                }


                return math.clamp(intersections, 0, 2);
            }
        }

        /// <summary>
        /// Intersects a line segment with a circle. There are 0, 1 or 2 possible points they cross. 
        /// The number of intersections is returned. If the number is 1, only the
        /// first intersection point returned is valid (the second one will contain invalid data).
        /// 
        /// If the line segment is completely contained within the circle, inside is set to true.
        /// 
        /// GPU: GGGIntersection
        /// </summary>
        /// <param name="line"></param>
        /// <param name="center"></param>
        /// <param name="radiusSq"></param>
        /// <param name="intersection0"></param>
        /// <param name="intersection1"></param>
        /// <returns></returns>
        public static int LineSegmentCircleIntersections(LineSegment2D line, float2 center, float radiusSq, out float2 intersection0, out float2 intersection1, out bool inside)
        {
            float2 a = line.a;
            float2 b = line.b;

            float distSqA = math.distancesq(a, center);
            float distSqB = math.distancesq(b, center);

            intersection0 = float2.zero;
            intersection1 = float2.zero;

            if (distSqA < radiusSq && distSqB < radiusSq)
            {
                inside = true;
                return 0;
            } 

            inside = false;

            float2 aToCenter = center - a;
            float2 dir = b - a;

            float2 closestPoint = a + math.project(aToCenter, dir);
            float closestPointDistSq = math.distancesq(center, closestPoint);

            if (closestPointDistSq > radiusSq + 10e-7f) return 0;
            else if (closestPointDistSq >= radiusSq - 10e-7f && closestPointDistSq <= radiusSq + 10e-7f)
            {
                intersection0 = closestPoint;
                bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= 0.0f && math.dot(-dir, closestPoint - b) >= 0.0f;
                return closestPointIsOnSegment ? 1 : 0;
            }
            else
            {
                //Pythagoras and Euclid - hand in hand
                //(I am glad I did not skip book III)
                //(I.e. the line towards the center of a circle that bisects another line through the closest point to the center, creates right angles... so to speak)
                float distFromClosestPointSq = radiusSq - closestPointDistSq;
                float distFromClosestPoint = math.sqrt(distFromClosestPointSq);

                float2 normalizedDir = math.normalize(dir);

                //A inside - B outside
                if (distSqA < radiusSq && distSqB >= radiusSq)
                {
                    intersection0 = closestPoint + normalizedDir * distFromClosestPoint;
                    return 1;
                }
                //B inside - A outside
                else if (distSqB < radiusSq && distSqA >= radiusSq)
                {
                    intersection0 = closestPoint - normalizedDir * distFromClosestPoint;
                    return 1;
                }
                //A and B outside
                else
                {
                    intersection0 = closestPoint + normalizedDir * distFromClosestPoint;
                    intersection1 = closestPoint - normalizedDir * distFromClosestPoint;
                    bool closestPointIsOnSegment = math.dot(dir, closestPoint - a) >= 0.0f && math.dot(-dir, closestPoint - b) >= 0.0f;
                    return closestPointIsOnSegment ? 2 : 0;
                }
            }
        }

    }
}
