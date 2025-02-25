

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class Polygon2DPointLocation
    {


        public unsafe static JobHandle ArePointsInPolygon(NativePolygon2D polygon, NativeArray<float2> queryPoints, ref NativeArray<bool> queryResult, 
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {

            var separators = polygon.separators;

            var offsets = new NativeArray<int>(polygon.points.Length + 1, allocator);

            UnsafeUtility.MemSet(offsets.GetUnsafePtr(), 0, offsets.Length * UnsafeUtility.SizeOf<int>());

            int prev = 0;
            for (int i = 0; i < separators.Length; i++)
            {
                offsets[separators[i]] = separators[i] - prev;
                prev = separators[i];
            }
            offsets[polygon.points.Length] = polygon.points.Length - prev;


            var polyJob = new Polygon2DPointLocationJobs.NativePolygon2DPointLocationJob()
            {
                polyPoints = polygon.points,
                queryPoints = queryPoints,
                queryResult = queryResult,
                offsets = offsets,
            };

            return polyJob.Schedule(dependsOn);
        }


        public unsafe static JobHandle ArePointsInPolygonParallel(NativePolygon2D polygon, NativeArray<float2> queryPoints, ref NativeArray<bool> queryResult,
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var separators = polygon.separators;

            var offsets = new NativeArray<int>(polygon.points.Length + 1, allocator);

            UnsafeUtility.MemSet(offsets.GetUnsafePtr(), 0, offsets.Length * UnsafeUtility.SizeOf<int>());

            int prev = 0;
            for (int i = 0; i < separators.Length; i++)
            {
                offsets[separators[i]] = separators[i] - prev;
                prev = separators[i];
            }
            offsets[polygon.points.Length] = polygon.points.Length - prev;

            var polyJob = new Polygon2DPointLocationJobs.NativePolygon2DPointLocationJobParallel()
            {
                polyPoints = polygon.points,
                queryPoints = queryPoints,
                queryResult = queryResult,
                offsets = offsets
            };


            var jobHandle = polyJob.Schedule(queryPoints.Length, 64, dependsOn);

            return jobHandle;
        }

    }
}
