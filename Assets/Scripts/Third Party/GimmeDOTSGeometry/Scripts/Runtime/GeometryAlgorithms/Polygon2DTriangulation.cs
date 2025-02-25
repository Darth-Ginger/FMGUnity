using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Polygon2DTriangulation
    {

        #region Fan Triangulation

        private static void FanTriangulationSafetyChecks(NativePolygon2D polygon2D)
        {
            if (!polygon2D.IsSimple())
            {
                Debug.LogError("[Gimme DOTS Geometry]: Only simple polygons may be fan triangulated!");
            }
#if !GDG_LENIENT_SAFETY_CHECKS

            if (!polygon2D.IsConvex())
            {
                Debug.LogError("[Gimme DOTS Geometry]: Only convex polygons can be fan triangulated!");
            }
#endif
        }

        private static void TriangulationSafetyChecks(NativePolygon2D polygon2D)
        {
#if !GDG_LENIENT_SAFETY_CHECKS

            if (!polygon2D.AreHolesValid())
            {
                Debug.LogError("[Gimme DOTS Geometry]: Holes of the given polygon are invalid!");
            }
#endif
        }




        /// <summary>
        /// Creates a fan triangulation for convex polygons
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="clockwiseWinding">The order in which the triangles in the triangulation should be returned</param>
        public static List<int> FanTriangulation(NativePolygon2D polygon2D, bool clockwiseWinding = true)
        {
            FanTriangulationSafetyChecks(polygon2D);

            //ListPool is only available in later Unity Versions
            var triangles = new List<int>();
            FanTriangulation(polygon2D, ref triangles, clockwiseWinding);
            return triangles;
        }

        /// <summary>
        /// Creates a fan triangulation for convex polygons
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="triangles"></param>
        /// <param name="clockwiseWinding">The order in which the triangles in the triangulation should be returned</param>
        public static void FanTriangulation(NativePolygon2D polygon2D, ref List<int> triangles, bool clockwiseWinding = true)
        {
            FanTriangulationSafetyChecks(polygon2D);

            triangles.Clear();

            var points = polygon2D.GetPoints();

            if (points.Length >= 3)
            {
                for (int i = 1; i < points.Length - 1; i++)
                {
                    if (clockwiseWinding)
                    {
                        triangles.Add(i + 1);
                        triangles.Add(i);
                        triangles.Add(0);
                    }
                    else
                    {
                        triangles.Add(0);
                        triangles.Add(i);
                        triangles.Add(i + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a job for fast triangulation of convex polygons.
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="triangles"></param>
        /// <param name="clockwiseWinding">The order in which the triangles in the triangulation should be returned</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FanTriangulationJob(NativePolygon2D polygon2D, ref NativeList<int> triangles, bool clockwiseWinding = true, JobHandle dependsOn = default)
        {
            FanTriangulationSafetyChecks(polygon2D);

            triangles.Clear();
            var job = new Polygon2DTriangulationJobs.FanTriangulationJob()
            {
                polyPoints = polygon2D.points,
                separators = polygon2D.separators,
                triangles = triangles,
                clockwiseWinding = clockwiseWinding,
            };

            return job.Schedule(dependsOn);
        }

        #endregion

        #region Ear Clipping Triangulation

        private static bool EarTest(NativeTriangle2D triangle, UnsafeList<float2> points, HashSet<int> reflexVertices)
        {
            bool isEar = true;
            foreach(int reflexVertex in reflexVertices)
            {
                var point = points[reflexVertex];

                if(math.any(triangle.a != point) 
                    && math.any(triangle.b != point) 
                    && math.any(triangle.c != point) 
                    && triangle.IsPointInside(point))
                {
                    isEar = false;
                    break;
                }
            }
            return isEar;
        }


        //Great paper: https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
        //But kind of inaccurate from chapter 5 on. What the author has not considered is that
        //multiple holes can map to a single outer-bound vertex, in which case the order of handling
        //the holes is very important to avoid loops in the vertex order (the sorting).
        //This is of course only necessary, if the holes are not in the correct order (which I can't
        //assume a user knows that that is even a problem)

        /// <summary>
        /// Triangulates any polygon (with holes) using ear clipping algorithm (which is O(n²)-ish)
        /// Here an <see href="https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf">Article</see>
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="clockwiseWinding">The winding in which the triangles in the triangulation should be returned</param>
        public static List<int> EarClippingTriangulate(NativePolygon2D polygon2D, bool clockwiseWinding = true)
        {
            TriangulationSafetyChecks(polygon2D);

            var triangles = new List<int>();
            EarClippingTriangulate(polygon2D, ref triangles, clockwiseWinding);
            return triangles;
        }

        /// <summary>
        /// Triangulates any polygon (with holes) using ear clipping algorithm (which is O(n²)-ish)
        /// Here an <see href="https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf">Article</see>
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="triangles"></param>
        /// <param name="clockwiseWinding">The winding in which the triangles in the triangulation should be returned</param>
        public static void EarClippingTriangulate(NativePolygon2D polygon2D, ref List<int> triangles, bool clockwiseWinding = true)
        {
            TriangulationSafetyChecks(polygon2D);

            var simplePolygon = polygon2D;
            bool allocatedSimplePolygon = false;
            if(!simplePolygon.IsSimple())
            {
                Debug.LogWarning("[Gimme DOTS Geometry]: Ear-clipping triangulation can only be used with simple polygons. Polygon will be transformed internally.");
                simplePolygon = NativePolygon2D.MakeSimple(Allocator.TempJob, polygon2D);
                allocatedSimplePolygon = true;
            }

            triangles.Clear();

            var points = simplePolygon.points;
            if (points.Length >= 3)
            {
                List<int> currentVertices = ListPool<int>.Get();
                List<int> ears = ListPool<int>.Get();

                var convexVertices = new HashSet<int>();
                var reflexVertices = new HashSet<int>();

                int length = points.Length;
                for(int i = 0; i < length; i++)
                {
                    currentVertices.Add(i);

                    var pointA = points.ElementAt(MathUtil.Mod(i - 1, length));
                    var pointB = points.ElementAt(i);
                    var pointC = points.ElementAt((i + 1) % length);

                    var angle = Vector2.SignedAngle(pointC - pointB, pointA - pointB);


                    if (angle < 0.0f)
                    {
                        reflexVertices.Add(i);
                    } else
                    {
                        convexVertices.Add(i);
                    }
                }

                foreach (var idx in convexVertices)
                {

                    var pointA = points.ElementAt(MathUtil.Mod(idx - 1, length));
                    var pointB = points.ElementAt(idx);
                    var pointC = points.ElementAt((idx + 1) % length);

                    var triangle = new NativeTriangle2D(pointA, pointB, pointC);
                    if (EarTest(triangle, points, reflexVertices))
                    {
                        ears.Add(idx);
                    }
                }

                while(currentVertices.Count > 3 && ears.Count > 0)
                {
                    int ear = ears[0];
                    int count = currentVertices.Count;

                    int listIndex = currentVertices.IndexOf(ear);
                    int prevIndex = MathUtil.Mod(listIndex - 1, count);
                    int nextIndex = (listIndex + 1) % count;

                    int vertex = currentVertices[listIndex];
                    int prevVertex = currentVertices[prevIndex];
                    int nextVertex = currentVertices[nextIndex];

                    if (clockwiseWinding)
                    {
                        triangles.Add(nextVertex);
                        triangles.Add(vertex);
                        triangles.Add(prevVertex);
                    }
                    else
                    {
                        triangles.Add(prevVertex);
                        triangles.Add(vertex);
                        triangles.Add(nextVertex);
                    }

                    int prePrevIndex = MathUtil.Mod(listIndex - 2, count);
                    int nextNextIndex = MathUtil.Mod(listIndex + 2, count);

                    int prePrevVertex = currentVertices[prePrevIndex];
                    int nextNextVertex = currentVertices[nextNextIndex];

                    float2 prePrevPoint = points[prePrevVertex];
                    float2 prevPoint = points[prevVertex];
                    float2 nextPoint = points[nextVertex];
                    float2 nextNextPoint = points[nextNextVertex];

                    var prevTriangle = new NativeTriangle2D(prePrevPoint, prevPoint, nextPoint);
                    var nextTriangle = new NativeTriangle2D(prevPoint, nextPoint, nextNextPoint);

                    bool prevIsEar = EarTest(prevTriangle, points, reflexVertices);
                    if (ears.Contains(prevVertex))
                    {
                        if (!prevIsEar)
                        {
                            ears.Remove(prevVertex);
                        }
                    } else if(reflexVertices.Contains(prevVertex))
                    {
                        var angle = Vector2.SignedAngle(nextPoint - prevPoint, prePrevPoint - prevPoint);

                        if(angle >= 0.0f)
                        {
                            reflexVertices.Remove(prevVertex);
                            convexVertices.Add(prevVertex);

                            if(EarTest(prevTriangle, points, reflexVertices))
                            {
                                ears.Add(prevVertex);
                            }
                        }
                    } else if(prevIsEar)
                    {
                        ears.Add(prevVertex);
                    }

                    bool nextIsEar = EarTest(nextTriangle, points, reflexVertices);
                    if (ears.Contains(nextVertex))
                    {
                        if (!nextIsEar)
                        {
                            ears.Remove(nextVertex);
                        }
                    } else if(reflexVertices.Contains(nextVertex))
                    {
                        var angle = Vector2.SignedAngle(nextNextPoint - nextPoint, prevPoint - nextPoint);

                        if(angle >= 0.0f)
                        {
                            reflexVertices.Remove(nextVertex);
                            convexVertices.Add(nextVertex);

                            if(EarTest(nextTriangle, points, reflexVertices))
                            {
                                ears.Add(nextVertex);
                            }
                        }
                    } else if(nextIsEar)
                    {
                        ears.Add(nextVertex);
                    }

                    currentVertices.Remove(vertex);
                    ears.Remove(ear);

                    ears.Sort();
                }

                for(int i = 0; i < currentVertices.Count; i++)
                {
                    int idx = clockwiseWinding ? (currentVertices.Count - 1 - i) : i;
                    triangles.Add(currentVertices[idx]);
                }

                if(allocatedSimplePolygon)
                {
                    simplePolygon.Dispose();
                }
                ListPool<int>.Return(currentVertices);
                ListPool<int>.Return(ears);
            }
        }

        /// <summary>
        /// Triangulates any polygon (with holes) using ear clipping algorithm (which is O(n²)-ish)
        /// Here an <see href="https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf">Article</see>
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="triangles"></param>
        /// <param name="clockwiseWinding">The winding in which the triangles in the triangulation should be returned</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle EarClippingTriangulationJob(NativePolygon2D polygon2D, ref NativeList<int> triangles, bool clockwiseWinding = true, JobHandle dependsOn = default)
        {
            TriangulationSafetyChecks(polygon2D);

            triangles.Clear();
            var job = new Polygon2DTriangulationJobs.EarClippingTriangulationJob()
            {
                polyPoints = polygon2D.points,
                triangles = triangles,
                clockwiseWinding = clockwiseWinding,
            };
            return job.Schedule(dependsOn);
        }

        /// <summary>
        /// Triangulates any polygon (with holes) using monotone triangulation (which is O(n log n))
        /// </summary>
        /// <param name="polygon2D"></param>
        /// <param name="triangles"></param>
        /// <param name="allocations"></param>
        /// <param name="allocator"></param>
        /// <param name="clockwiseWinding">The winding in which the triangles in the triangulation should be returned</param>
        /// <param name="epsilon"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle YMonotoneTriangulationJob(NativePolygon2D polygon2D, ref NativeList<int> triangles, Allocator allocator = Allocator.TempJob, bool clockwiseWinding = true, float epsilon = 10e-6f, JobHandle dependsOn = default)
        {
            TriangulationSafetyChecks(polygon2D);

            triangles.Clear();

            var monotonePolyPointMapping = new NativeList<int>(1, allocator);
            var monotonePolySeparators = new NativeList<int>(1, allocator);

            var monotoneDecompositionJob = new Polygon2DTriangulationJobs.MonotoneDecompositionJob()
            {
                epsilon = epsilon,
                polyPoints = polygon2D.points,
                polySeparators = polygon2D.separators,
                monotonePolyPointMapping = monotonePolyPointMapping,
                monotonePolySeparators = monotonePolySeparators
            };

            var triangulationJob = new Polygon2DTriangulationJobs.YMonotoneTriangulationJob()
            {
                polyPoints = polygon2D.points,
                triangles = triangles,
                monotonePolyPointMapping = monotonePolyPointMapping,
                monotonePolySeparators = monotonePolySeparators,
                clockwiseWinding = clockwiseWinding
            };

            var monotoneHandle = monotoneDecompositionJob.Schedule(dependsOn);
            var triangulationHandle = triangulationJob.Schedule(monotoneHandle);

            var deallocateMapping = monotonePolyPointMapping.Dispose(triangulationHandle);
            var deallocateSeparators = monotonePolySeparators.Dispose(triangulationHandle);

            return JobHandle.CombineDependencies(deallocateMapping, deallocateSeparators);
        }

        #endregion
    }
}
