using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Delaunay2D 
    {
        /// <summary>
        /// Calculates the delaunay triangulation (a triangulation that maximizes the minimum angle of all triangles)
        /// of a set of points
        /// </summary>
        /// <param name="points">The set of points to be triangulated</param>
        /// <param name="triangulation">A list of triples of integers where each triple points to three indices in the points-array thus forming a triangle</param>
        /// <param name="allocations"></param>
        /// <param name="preserveOrder">If true, the order of the input points array is preserved (has a small performance impact)</param>
        /// <param name="allocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle CalculateDelaunay(NativeArray<float2> points, ref NativeList<int3> triangulation,
            out JobAllocations allocations, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            allocations = new JobAllocations();

            var dag = new NativeGraph(allocator);
            var triangleBuffer = new NativeList<Delaunay2DJobs.DelaunayTriangleData>(allocator);
            var halfEdges = new NativeList<HalfEdge>(allocator);
            var swappedIdx = new NativeReference<int>(allocator);

            allocations.allocatedMemory.Add(dag);
            allocations.allocatedMemory.Add(triangleBuffer);
            allocations.allocatedMemory.Add(halfEdges);
            allocations.allocatedMemory.Add(swappedIdx);

            if(points.Length <= 2)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Number of points has to be larger than 2 to create a valid triangulation!");
                return default;
            }

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
                halfEdges = halfEdges,
            };

            var delaunayHandle = delaunay2DJob.Schedule(findLargestHandle);

            var swapBackJob = new Delaunay2DJobs.SwapBackJob()
            {
                points = points,
                swappedIdx = swappedIdx,
                triangulation = triangulation,
            };
            return swapBackJob.Schedule(delaunayHandle);

        }

    }
}
