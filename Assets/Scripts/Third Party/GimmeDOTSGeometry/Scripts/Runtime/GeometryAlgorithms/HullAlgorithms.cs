using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class HullAlgorithms
    {
        /// <summary>
        /// Calculates the minimum enclosing rectangle containing all the points provided,
        /// and stores it in "rectangle"
        /// </summary>
        /// <param name="points"></param>
        /// <param name="rectangle"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateBoundingRect(NativeArray<float2> points,
            NativeReference<Rect> rectangle,
            float addedMargin = 0.0f,
            JobHandle dependsOn = default)
        {
            var boundingRectJob = new HullAlgorithmJobs.BoundingRectangleJob()
            {
                boundingRect = rectangle,
                points = points,
                addedMargin = addedMargin
            };

            return boundingRectJob.Schedule(dependsOn);
        }

        /// <summary>
        /// Calculates the minimum enclosing box containing all the points provided,
        /// and stores it in "bounds"
        /// </summary>
        /// <param name="points"></param>
        /// <param name="bounds"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateBoundingBox(NativeArray<float3> points,
            NativeReference<Bounds> bounds,
            float addedMargin = 0.0f,
            JobHandle dependsOn = default)
        {
            var boundsJob = new HullAlgorithmJobs.BoundingBoxJob()
            {
                bounds = bounds,
                points = points,
                addedMargin = addedMargin,
            };

            return boundsJob.Schedule(dependsOn);
        }


        /// <summary>
        /// Calculates the minimum enclosing disc containing all the points provided
        /// within an array
        /// </summary>
        /// <param name="points"></param>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="filter">If true, Akl-Toussaint heuristic is used. This speeds up the calculation when having a lot of points</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FindMinimumEnclosingDisc(NativeArray<float2> points,
            NativeReference<float2> center,
            NativeReference<float> radius,
            JobHandle dependsOn = default)
        {

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
            {
                center = center,
                points = points,
                radius = radius,
            };
            return minDiscJob.Schedule(dependsOn);

        }

        /// <summary>
        /// Calculates the minimum enclosing sphere containing all the points provided
        /// within an array
        /// </summary>
        /// <param name="points"></param>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FindMinimumEnclosingSphere(NativeArray<float3> points,
            NativeReference<float3> center,
            NativeReference<float> radius,
            JobHandle dependsOn = default)
        {
            var minSphereJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                center = center,
                points = points,
                radius = radius
            };
            return minSphereJob.Schedule(dependsOn);
        }


        private static List<float2> GetAklToussaintBoundary(List<float2> points)
        {
            float2 left = Vector2.positiveInfinity;
            float2 right = Vector2.negativeInfinity;
            float2 bottom = left;
            float2 top = right;

            for (int i = 0; i < points.Count; i++)
            {
                var point = points.ElementAt(i);

                if (point.x < left.x) { left = point; }
                if (point.x > right.x) { right = point; }
                if (point.y < bottom.y) { bottom = point; }
                if (point.y > top.y) { top = point; }
            }

            float2 leftToBottom = bottom - left;
            float2 bottomToRight = right - bottom;
            float2 rightToTop = top - right;
            float2 topToLeft = left - top;

            List<float2> convexPoly = new List<float2>();

            //Because it might be that one of the points is equal to another (e.g. top = right),
            //In which case we check for the triangle not the quadrilateral (this is what is not on the Wiki-Page haha)
            if (math.any(leftToBottom != float2.zero)) convexPoly.Add(left);
            if (math.any(bottomToRight != float2.zero)) convexPoly.Add(bottom);
            if (math.any(rightToTop != float2.zero)) convexPoly.Add(right);
            if (math.any(topToLeft != float2.zero)) convexPoly.Add(top);

            return convexPoly;
        }

        private static List<float2> AklToussaintFilter(List<float2> points)
        {
            var convexPoly = GetAklToussaintBoundary(points);

            List<float2> filteredList = new List<float2>();

            //Triangle or Quadrangle is always convex (because the points are on the border rectangle) ->
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];

                for (int j = 0; j < convexPoly.Count; j++)
                {
                    float2 start = convexPoly[j];
                    float2 end = convexPoly[(j + 1) % convexPoly.Count];
                    if (VectorUtil.TurnDirection(start, end, point) >= 0)
                    {
                        filteredList.Add(point);
                        break;
                    }
                }

            }

            return filteredList;
        }

        public struct LexicographicPointComparer : IComparer<float2>
        {
            public int Compare(float2 a, float2 b)
            {
                int comp = a.x.CompareTo(b.x);
                if (comp != 0) return comp;
                return a.y.CompareTo(b.y);
            }
        }

        public static JobHandle FindConvexHullIndices(NativeArray<float2> points, ref NativeList<int> indices,
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            return FindConvexHullIndices(points, ref indices, false, allocator, dependsOn);
        }

        public static JobHandle FindConvexHullIndices(NativeArray<float2> points, ref NativeList<int> indices, bool filter,
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            if (points.Length < 3) return dependsOn;

            indices.Clear();

            if (filter)
            {
                var polyBounds = new NativeReference<FixedList64Bytes<float2>>(allocator);
                var filteredPoints = new NativeList<float2>(points.Length, allocator);

                var aklBoundsJob = new HullAlgorithmJobs.AklToussaintBoundaryJob()
                {
                    points = points,
                    bounds = polyBounds
                };

                var aklBoundsHandle = aklBoundsJob.Schedule(dependsOn);
                var aklToussaintJob = new HullAlgorithmJobs.AklToussaintFilterJob()
                {
                    convexBound = polyBounds,
                    inputPoints = points,
                    outputPoints = filteredPoints,
                };

                var aklHandle = aklToussaintJob.Schedule(aklBoundsHandle);

                //Sadly sorting requires access to the Length parameter on Schedule, therefore we have to complete here
                aklHandle.Complete();

                var sortJob = filteredPoints.SortJob(new LexicographicPointComparer());
                var sortHandle = sortJob.Schedule();

                var convexHullJob = new HullAlgorithmJobs.ConvexHullIndicesJob()
                {
                    inputPoints = filteredPoints.AsArray(),
                    indices = indices,
                };

                var hullHandle = convexHullJob.Schedule(sortHandle);
                var disposeFilteredPoints = filteredPoints.Dispose(hullHandle);
                var disposePolyBounds = polyBounds.Dispose(disposeFilteredPoints);
                return JobHandle.CombineDependencies(disposeFilteredPoints, disposePolyBounds);
            }
            else
            {

                var sortJob = points.SortJob(new LexicographicPointComparer());
                var sortHandle = sortJob.Schedule(dependsOn);

                var convexHullJob = new HullAlgorithmJobs.ConvexHullIndicesJob()
                {
                    inputPoints = points,
                    indices = indices,
                };

                return convexHullJob.Schedule(sortHandle);
                
            }
        }


        /// <summary>
        /// Calculates the convex hull of a given set of points and returns
        /// them as a polygon
        /// </summary>
        /// <param name="points"></param>
        /// <param name="polygon"></param>
        /// <param name="allocations"></param>
        /// <param name="jobAllocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CreateConvexHull(NativeArray<float2> points, ref NativePolygon2D polygon,
            Allocator jobAllocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            return CreateConvexHull(points, ref polygon, false, jobAllocator, dependsOn);
        }

        /// <summary>
        /// Calculates the convex hull of a given set of points and returns
        /// them as a polygon
        /// </summary>
        /// <param name="points"></param>
        /// <param name="polygon"></param>
        /// <param name="filter">If true, Akl-Toussaint heuristic is used. This speeds up the calculation when having a lot of points</param>
        /// <param name="allocations"></param>
        /// <param name="jobAllocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public unsafe static JobHandle CreateConvexHull(NativeArray<float2> points, ref NativePolygon2D polygon, bool filter,
            Allocator jobAllocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            if (points.Length < 3) return dependsOn;

            polygon.points.Clear();
            polygon.separators.Clear();

            if (filter)
            {
                var polyBounds = new NativeReference<FixedList64Bytes<float2>>(jobAllocator);
                var filteredPoints = new NativeList<float2>(points.Length, jobAllocator);

                var aklBoundsJob = new HullAlgorithmJobs.AklToussaintBoundaryJob()
                {
                    points = points,
                    bounds = polyBounds
                };

                var aklBoundsHandle = aklBoundsJob.Schedule(dependsOn);
                var aklToussaintJob = new HullAlgorithmJobs.AklToussaintFilterJob()
                {
                    convexBound = polyBounds,
                    inputPoints = points,
                    outputPoints = filteredPoints,
                };

                var aklHandle = aklToussaintJob.Schedule(aklBoundsHandle);

                //Sadly sorting requires access to the Length parameter on Schedule, therefore we have to complete here
                aklHandle.Complete();

                var sortJob = filteredPoints.SortJob(new LexicographicPointComparer());
                var sortHandle = sortJob.Schedule();

                fixed (UnsafeList<float2>* polygonList = &polygon.points)
                {
                    var convexHullJob = new HullAlgorithmJobs.ConvexHullJob()
                    {
                        inputPoints = filteredPoints.AsArray(),
                        outputPoints = polygonList,
                    };

                    var hullHandle = convexHullJob.Schedule(sortHandle);
                    var disposeFilteredPoints = filteredPoints.Dispose(hullHandle);
                    var disposePolyBounds = polyBounds.Dispose(disposeFilteredPoints);
                    return JobHandle.CombineDependencies(disposeFilteredPoints, disposePolyBounds);
                }


            }
            else
            {

                var sortJob = points.SortJob(new LexicographicPointComparer());
                var sortHandle = sortJob.Schedule(dependsOn);


                fixed (UnsafeList<float2>* polygonList = &polygon.points)
                {

                    var convexHullJob = new HullAlgorithmJobs.ConvexHullJob()
                    {
                        inputPoints = points,
                        outputPoints = polygonList,
                    };

                    return convexHullJob.Schedule(sortHandle);
                }
            }
        }


        /// <summary>
        /// Method used is "Andrew's Monotone Chain". Good Explanation here: Computational Geometry (ISBN 978-3-642-09681-5) p. 6 - 8 or just on Wiki
        /// </summary>
        /// <param name="points"></param>
        /// <param name="prefilterPoints">If set to true, the points are first filtered by the Akl-Toussaint Heuristic. 
        /// This may increase performance for large collections of points (but may lower it otherwise)</param>
        /// <returns>A 2D polygon containing the convex hull of all the points</returns>
        public static NativePolygon2D CreateConvexHull(Allocator allocator, List<float2> points, bool prefilterPoints = false)
        {
            if (points.Count <= 3) return new NativePolygon2D(allocator, points);

            var usedPoints = points;
            if (prefilterPoints)
            {
                usedPoints = AklToussaintFilter(points);
            }

            usedPoints.Sort((a, b) =>
            {
                int comp = a.x.CompareTo(b.x);
                if (comp != 0) return comp;
                return a.y.CompareTo(b.y);
            });

            List<float2> upperHull = ListPool<float2>.Get();
            List<float2> lowerHull = ListPool<float2>.Get();

            int pointCount = usedPoints.Count;

            lowerHull.Add(usedPoints[0]);
            lowerHull.Add(usedPoints[1]);

            for (int i = 2; i < pointCount; i++)
            {
                lowerHull.Add(usedPoints[i]);

                int count = lowerHull.Count;
                while (lowerHull.Count > 2 &&
                    VectorUtil.TurnDirection(lowerHull[count - 3], lowerHull[count - 2], lowerHull[count - 1]) >= 0)
                {
                    lowerHull.RemoveAt(count - 2);
                    count = lowerHull.Count;
                }
            }

            upperHull.Add(usedPoints[pointCount - 1]);
            upperHull.Add(usedPoints[pointCount - 2]);

            for (int i = pointCount - 3; i >= 0; i--)
            {
                upperHull.Add(usedPoints[i]);

                int count = upperHull.Count;
                while (upperHull.Count > 2 &&
                    VectorUtil.TurnDirection(upperHull[count - 3], upperHull[count - 2], upperHull[count - 1]) >= 0)
                {
                    upperHull.RemoveAt(count - 2);
                    count = upperHull.Count;
                }
            }

            lowerHull.RemoveAt(0);
            lowerHull.RemoveAt(lowerHull.Count - 1);

            upperHull.AddRange(lowerHull);
            var poly = new NativePolygon2D(allocator, upperHull);

            ListPool<float2>.Return(upperHull);
            ListPool<float2>.Return(lowerHull);

            return poly;
        }


        //Concave Hull based on https://global.discourse-cdn.com/mcneel/uploads/default/original/3X/e/2/e2a9d19b49688b859eb2057ab6e1e8a7f29962fa.pdf
        /// <summary>
        /// Calculates a concave hull of a given set of points and returns
        /// them as a polygon
        /// </summary>
        /// <param name="points"></param>
        /// <param name="polygon"></param>
        /// <param name="concavity">The higher the value, the more convex the resulting polygon. The typical range should be between 1-5</param>
        /// <param name="jobAllocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public unsafe static JobHandle CreateConcaveHull(NativeArray<float2> points, ref NativePolygon2D polygon, float concavity,
            float epsilon = 10e-4f, Allocator jobAllocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            if (points.Length < 3) return dependsOn;

            var convexHullIndices = new NativeList<int>(3, jobAllocator);
            var convexHullHandle = FindConvexHullIndices(points, ref convexHullIndices, jobAllocator, dependsOn);
            convexHullHandle.Complete();

            fixed (UnsafeList<float2>* polygonList = &polygon.points)
            {
                var concaveHullJob = new HullAlgorithmJobs.ConcaveHullJob()
                {
                    inputPoints = points,
                    outputPoints = polygonList,
                    convexHullIndices = convexHullIndices,
                    concavity = concavity,
                    epsilon = epsilon,
                };

               var concaveHullHandle = concaveHullJob.Schedule();
               return convexHullIndices.Dispose(concaveHullHandle);
            }
        }



    }
}