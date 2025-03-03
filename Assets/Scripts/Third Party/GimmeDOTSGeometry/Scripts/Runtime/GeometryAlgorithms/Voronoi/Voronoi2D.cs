using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Voronoi2D 
    {
        /// <summary>
        /// Returns the index of a Voronoi Lookup Table (VoLT) a given position falls into.
        /// If the position is out of the bounds, the index returned is invalid (there are no bound checks)
        /// </summary>
        /// <param name="dimension"></param>
        /// <param name="bounds"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateVoronoiLookupTableIndex(int2 dimension, Rect bounds, float2 position)
        {
            float2 pos = math.unlerp(bounds.min, bounds.max, position) * dimension;
            return math.mad((int)pos.y, dimension.x, (int)pos.x);
        }

        /// <summary>
        /// Calculates a Voronoi Lookup Table (VoLT) with a certain dimension within the given bounds
        /// </summary>
        /// <param name="dimension">The dimension in X and Y (similar to a texture resolution)</param>
        /// <param name="bounds">The enclosing boundary of the points</param>
        /// <param name="points">The sites of the voronoi diagram</param>
        /// <param name="table">The table is returned after the scheduled Job is completed</param>
        /// <param name="allocations"></param>
        /// <param name="allocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateVoronoiLookupTable(int2 dimension, Rect bounds, NativeArray<float2> points,
            ref NativeArray<int> table, out JobAllocations allocations, Allocator allocator = Allocator.TempJob, 
            JobHandle dependsOn = default)
        {
            allocations = new JobAllocations();

            if(points.Length <= 1)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points has to be larger or equal to 2!");
                return default;
            }

            var rectRef = new NativeReference<Rect>(allocator);
            rectRef.Value = bounds;

            allocations.allocatedMemory.Add(rectRef);

            var voronoiLookupJob = new Voronoi2DJobs.Voronoi2DLookupTableJob()
            {
                bounds = rectRef,
                gridSize = dimension,
                sites = points,
                table = table,
            };
            return voronoiLookupJob.Schedule(table.Length, 32, dependsOn);
        }

        /// <summary>
        /// Calculates a Voronoi Lookup Table (VoLT) with a certain dimension. A job is scheduled to calculate
        /// the bounding rectangle
        /// </summary>
        /// <param name="dimension">The dimension in X and Y (similar to a texture resolution)</param>
        /// <param name="points">The sites of the voronoi diagram</param>
        /// <param name="table">The table is returned after the scheduled Job is completed</param>
        /// <param name="allocations"></param>
        /// <param name="allocator"></param>
        /// <param name="addedMargin">Additional margin space for the boundary</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateVoronoiLookupTable(int2 dimension, NativeArray<float2> points,
            ref NativeArray<int> table, out JobAllocations allocations, Allocator allocator = Allocator.TempJob,
            float addedMargin = 1.0f, JobHandle dependsOn = default)
        {
            allocations = new JobAllocations();

            if (points.Length <= 1)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points has to be larger or equal to 2!");
                return default;
            }

            var rectRef = new NativeReference<Rect>(allocator);

            allocations.allocatedMemory.Add(rectRef);

            var boundsJob = new HullAlgorithmJobs.BoundingRectangleJob()
            {
                boundingRect = rectRef,
                points = points,
                addedMargin = addedMargin,
            };
            var boundsHandle = boundsJob.Schedule(dependsOn);


            var voronoiLookupJob = new Voronoi2DJobs.Voronoi2DLookupTableJob()
            {
                bounds = rectRef,
                gridSize = dimension,
                sites = points,
                table = table,
            };
            return voronoiLookupJob.Schedule(table.Length, 32, boundsHandle);
        }


        /// <summary>
        /// Calculates and returns the polygons that form a Voronoi Diagram by forming the dual graph of the delaunay triangulation
        /// </summary>
        /// <param name="bounds">The enclosing boundary of the points</param>
        /// <param name="points">>The sites of the voronoi diagram</param>
        /// <param name="polygons">An array that is filled with the resulting polygons. Its length should be equal to the number of points</param>
        /// <param name="polygonSites">An array that is filled with integers for each polygon. Each integer corresponds to the site the polygon belongs to</param>
        /// <param name="allocations"></param>
        /// <param name="allocator"></param>
        /// <param name="epsilon0">The error rate for forming the dual graph</param>
        /// <param name="epsilon1">The error rate for boundary intersections</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateVoronoi(Rect bounds, NativeArray<float2> points, ref NativeArray<NativePolygon2D> polygons,
            ref NativeArray<int> polygonSites, out JobAllocations allocations, Allocator allocator = Allocator.TempJob, 
            float epsilon0 = 10e-8f, float epsilon1 = 10e-6f, JobHandle dependsOn = default)
        {

            allocations = new JobAllocations();
            
            if (points.Length <= 1)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points has to be larger or equal to 2!");
                return default;
            }

            if (points.Length != polygons.Length)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points and polygons has to be equal!");
                return default;
            }

            for (int i = 0; i < polygons.Length; i++)
            {
                polygons[i].Clear();
            }

            var rectRef = new NativeReference<Rect>(allocator);
            var voronoiHalfEdges = new NativeList<HalfEdge>(allocator);
            var vertices = new NativeList<float2>(allocator);
            var halfEdgeSites = new NativeList<int>(allocator);
            var swappedIdx = new NativeReference<int>(allocator);

            var dag = new NativeGraph(allocator);
            var triangleBuffer = new NativeList<Delaunay2DJobs.DelaunayTriangleData>(allocator);
            var delaunayHalfEdges = new NativeList<HalfEdge>(allocator);
            var triangulation = new NativeList<int3>(allocator);

            allocations.allocatedMemory.Add(triangulation);     // 0
            allocations.allocatedMemory.Add(rectRef);           // 1
            allocations.allocatedMemory.Add(delaunayHalfEdges); // 2
            allocations.allocatedMemory.Add(voronoiHalfEdges);  //3
            allocations.allocatedMemory.Add(vertices);          // 4
            allocations.allocatedMemory.Add(halfEdgeSites);     // 5
            allocations.allocatedMemory.Add(dag);               // 6
            allocations.allocatedMemory.Add(triangleBuffer);    // 7
            allocations.allocatedMemory.Add(swappedIdx);        // 8

            JobHandle nextHandle = dependsOn;
            if (points.Length <= 2)
            {
                //We cannot build a delaunay triangulation with two points, but we can simply add the
                //correct half edges in this case!
                delaunayHalfEdges.Add(new HalfEdge()
                {
                    back = -1,
                    fwd = -1,
                    twin = 1,
                    vertexBack = 0,
                    vertexFwd = 1,
                    face = -1,
                });

                delaunayHalfEdges.Add(new HalfEdge() {
                    back = -1,
                    fwd = -1,
                    twin = 0,
                    vertexBack = 1,
                    vertexFwd = 0,
                    face = -1,
                });

            }
            else
            {

                var findLargestAndPermuteJob = new Delaunay2DJobs.FindLexicographicLargestJob()
                {
                    points = points,
                    swappedIdx = swappedIdx,
                };

                var findLargestHandle = findLargestAndPermuteJob.Schedule(dependsOn);

                var delaunay2DJob = new Delaunay2DJobs.DelaunayTriangulationJob()
                {
                    dag = dag,
                    points = points,
                    triangulation = triangulation,
                    triangleBuffer = triangleBuffer,
                    halfEdges = delaunayHalfEdges,
                };

                nextHandle = delaunay2DJob.Schedule(findLargestHandle);
            }

            rectRef.Value = bounds;

            var voronoiJob = new Voronoi2DJobs.Voronoi2DFromDelaunayJob()
            {
                bounds = rectRef,
                delaunayHalfEdges = delaunayHalfEdges,
                voronoiHalfEdges = voronoiHalfEdges,
                sites = points,
                voronoiVertices = vertices,
                delaunayTriangulation = triangulation,
                halfEdgeSites = halfEdgeSites,
                epsilon = epsilon0,
            };
            var voronoiHandle = voronoiJob.Schedule(nextHandle);

            var toPolygonJob = new Voronoi2DJobs.Voronoi2DToPolygonsJob()
            {
                halfEdges = voronoiHalfEdges,
                polygons = polygons,
                vertices = vertices,
                epsilon = epsilon1,
                bounds = rectRef,
                halfEdgeSites = halfEdgeSites,
                polygonSites = polygonSites,
            };

            return toPolygonJob.Schedule(voronoiHandle);
        }


        /// <summary>
        /// Calculates and returns the polygons that form a Voronoi Diagram by forming the dual graph of the delaunay triangulation. A job is scheduled to calculate
        /// the bounding rectangle
        /// </summary>
        /// <param name="points">>The sites of the voronoi diagram</param>
        /// <param name="polygons">An array that is filled with the resulting polygons. Its length should be equal to the number of points</param>
        /// <param name="polygonSites">An array that is filled with integers for each polygon. Each integer corresponds to the site the polygon belongs to</param>
        /// <param name="allocations"></param>
        /// <param name="allocator"></param>
        /// <param name="addedMargin">Additional margin space for the boundary</param>
        /// <param name="epsilon0">The error rate for forming the dual graph</param>
        /// <param name="epsilon1">The error rate for boundary intersections</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateVoronoi(NativeArray<float2> points, ref NativeArray<NativePolygon2D> polygons, 
            ref NativeArray<int> polygonSites, out JobAllocations allocations, Allocator allocator = Allocator.TempJob, 
            float addedMargin = 1.0f, float epsilon0 = 10e-8f, float epsilon1 = 10e-6f, JobHandle dependsOn = default)
        {
            allocations = new JobAllocations();

            if(points.Length <= 1)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points has to be larger or equal to 2!");
                return default;
            }

            if (points.Length != polygons.Length)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points and polygons has to be equal!");
                return default;
            }

            for(int i = 0; i< polygons.Length; i++)
            {
                polygons[i].Clear();
            }

            var rectRef = new NativeReference<Rect>(allocator);
            var voronoiHalfEdges = new NativeList<HalfEdge>(allocator);
            var vertices = new NativeList<float2>(allocator);
            var halfEdgeSites = new NativeList<int>(allocator);
            var swappedIdx = new NativeReference<int>(allocator);

            var dag = new NativeGraph(allocator);
            var triangleBuffer = new NativeList<Delaunay2DJobs.DelaunayTriangleData>(allocator);
            var delaunayHalfEdges = new NativeList<HalfEdge>(allocator);
            var triangulation = new NativeList<int3>(allocator);

            allocations.allocatedMemory.Add(rectRef);
            allocations.allocatedMemory.Add(delaunayHalfEdges);
            allocations.allocatedMemory.Add(voronoiHalfEdges);
            allocations.allocatedMemory.Add(vertices);
            allocations.allocatedMemory.Add(halfEdgeSites);
            allocations.allocatedMemory.Add(dag);
            allocations.allocatedMemory.Add(triangleBuffer);
            allocations.allocatedMemory.Add(triangulation);
            allocations.allocatedMemory.Add(swappedIdx);

            JobHandle nextHandle = dependsOn;
            if (points.Length <= 2)
            {
                //We cannot build a delaunay triangulation with two points, but we can simply add the
                //correct half edges in this case!

                delaunayHalfEdges.Add(new HalfEdge()
                {
                    back = -1,
                    fwd = -1,
                    twin = 1,
                    vertexBack = 0,
                    vertexFwd = 1,
                });

                delaunayHalfEdges.Add(new HalfEdge()
                {
                    back = -1,
                    fwd = -1,
                    twin = 0,
                    vertexBack = 1,
                    vertexFwd = 0,
                });

            }
            else
            {

                var findLargestAndPermuteJob = new Delaunay2DJobs.FindLexicographicLargestJob()
                {
                    points = points,
                    swappedIdx = swappedIdx,
                };

                var findLargestHandle = findLargestAndPermuteJob.Schedule(dependsOn);

                var delaunay2DJob = new Delaunay2DJobs.DelaunayTriangulationJob()
                {
                    dag = dag,
                    points = points,
                    triangulation = triangulation,
                    triangleBuffer = triangleBuffer,
                    halfEdges = delaunayHalfEdges,
                };

                nextHandle = delaunay2DJob.Schedule(findLargestHandle);
            }

            var boundsJob = new HullAlgorithmJobs.BoundingRectangleJob()
            {
                boundingRect = rectRef,
                points = points,
                addedMargin = addedMargin,
            };
            var boundsHandle = boundsJob.Schedule(nextHandle);

            var voronoiJob = new Voronoi2DJobs.Voronoi2DFromDelaunayJob()
            {
                bounds = rectRef,
                delaunayHalfEdges = delaunayHalfEdges,
                voronoiHalfEdges = voronoiHalfEdges,
                sites = points,
                voronoiVertices = vertices,
                delaunayTriangulation = triangulation,
                halfEdgeSites = halfEdgeSites,
                epsilon = epsilon0,
            };
            var voronoiHandle = voronoiJob.Schedule(boundsHandle);

            var toPolygonJob = new Voronoi2DJobs.Voronoi2DToPolygonsJob()
            {
                halfEdges = voronoiHalfEdges,
                polygons = polygons,
                vertices = vertices,
                epsilon = epsilon1,
                bounds = rectRef,
                halfEdgeSites = halfEdgeSites,
                polygonSites = polygonSites,
            };

            return toPolygonJob.Schedule(voronoiHandle);
        }


    }
}
