using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class SpecialQuery
    {

        /// <summary>
        /// Given a collection of points and a radius, it will return for each point all other points that are within that radius
        /// </summary>
        /// <param name="allRadius">The search radius for each and all points</param>
        /// <param name="points">The list of points</param>
        /// <param name="result">A list for each point containing the indices of the other points within the radius</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle AllRadiusQuery(float allRadius, NativeArray<float2> points, 
            ref NativeList<UnsafeList<int>> result, Allocator allocator = Allocator.TempJob, 
            JobHandle dependsOn = default)
        {

            var queue = new NativeArray<SpecialQueryJobs.ShapeEventPoint>(points.Length * 2, allocator);

            var createQueueJob = new SpecialQueryJobs.CreateRadiusEventQueueJob()
            {
                points = points,
                radius = allRadius,
                radiusEventQueue = queue,
            };
            var queueHandle = createQueueJob.Schedule(dependsOn);

            var allRadiusQueryJob = new SpecialQueryJobs.AllRadiusQueryJob()
            {
                points = points,
                radius = allRadius,
                result = result,
                radiusEventQueue = queue,
            };
            return allRadiusQueryJob.Schedule(queueHandle);
        }

        public static JobHandle CreateSortedRadiusEventQueue(float allRadius, NativeArray<float2> points, 
            ref NativeArray<SpecialQueryJobs.ShapeEventPoint> queue, JobHandle dependsOn = default)
        {
            var createQueueJob = new SpecialQueryJobs.CreateRadiusEventQueueJob()
            {
                points = points,
                radius = allRadius,
                radiusEventQueue = queue,
            };
            return createQueueJob.Schedule(dependsOn);
        }

        public static JobHandle CreateSortedRectangleEventQueue(Rect allRectangle, NativeArray<float2> points,
            ref NativeArray<SpecialQueryJobs.ShapeEventPoint> queue, JobHandle dependsOn = default)
        {
            var createQueueJob = new SpecialQueryJobs.CreateRectangleEventQueueJob()
            {
                points = points,
                height = allRectangle.height,
                rectEventQueue = queue,
            };
            return createQueueJob.Schedule(dependsOn);
        }

        public static JobHandle PresortedAllRadiusParallelQuery(float allRadius, NativeArray<float2> points,
            ref NativeList<UnsafeList<int>> result, ref NativeArray<SpecialQueryJobs.ShapeEventPoint> presortedQueue,
            int batchSize = 512, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var parallelWriterList = new NativeList<UnsafeList<int>.ParallelWriter>(points.Length, allocator);

            var updateQueueJob = new SpecialQueryJobs.UpdatePresortedRadiusEventQueueJob()
            {
                points = points,
                radius = allRadius,
                radiusEventQueue = presortedQueue,
            };
            var updateHandle = updateQueueJob.Schedule(presortedQueue.Length, 64, dependsOn);

            int offset = batchSize / 2;
            var parallelSortJob = new SpecialQueryJobs.SortPresortedEventQueueParallelJob()
            {
                batchSize = batchSize,
                eventQueue = presortedQueue,
                offset = offset,
            };

            int parallelSortJobs = (presortedQueue.Length / batchSize) + 1;
            if (presortedQueue.Length % batchSize > offset) parallelSortJobs++;
            var parallelSortHandle = parallelSortJob.Schedule(parallelSortJobs, 1, updateHandle);

            var sortJob = new SpecialQueryJobs.SortPresortedEventQueueJob()
            {
                eventQueue = presortedQueue,
            };
            var sortJobHandle = sortJob.Schedule(parallelSortHandle);


            var preparationJob = new SpecialQueryJobs.PrepareParallelQueryJob()
            {
                result = result,
                targetCapacity = points.Length,
                writerList = parallelWriterList,
            };
            var preparationHandle = preparationJob.Schedule(dependsOn);

            var allRadiusQueryJob = new SpecialQueryJobs.AllRadiusParallelQueryJob()
            {
                batchSize = batchSize,
                points = points,
                radius = allRadius,
                radiusEventQueue = presortedQueue,
                result = parallelWriterList,
            };
            int jobs = (points.Length * 2) / batchSize;
            if ((points.Length * 2) % batchSize != 0) jobs++;

            var combinedDependency = JobHandle.CombineDependencies(sortJobHandle, preparationHandle);

            var allRadiusJob = allRadiusQueryJob.Schedule(jobs, 1, combinedDependency);
            return parallelWriterList.Dispose(allRadiusJob);
        }


        public static JobHandle AllRadiusParallelQuery(float allRadius, NativeArray<float2> points,
            ref NativeList<UnsafeList<int>> result, int batchSize = 512,
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var queue = new NativeArray<SpecialQueryJobs.ShapeEventPoint>(points.Length * 2, allocator);
            var parallelWriterList = new NativeList<UnsafeList<int>.ParallelWriter>(points.Length, allocator);

            var createQueueJob = new SpecialQueryJobs.CreateRadiusEventQueueJob()
            {
                points = points,
                radius = allRadius,
                radiusEventQueue = queue,
            };
            var queueHandle = createQueueJob.Schedule(dependsOn);

            var preparationJob = new SpecialQueryJobs.PrepareParallelQueryJob()
            {
                result = result,
                targetCapacity = points.Length,
                writerList = parallelWriterList,
            };
            var preparationHandle = preparationJob.Schedule(dependsOn);

            var allRadiusQueryJob = new SpecialQueryJobs.AllRadiusParallelQueryJob()
            {
                batchSize = batchSize,
                points = points,
                radius = allRadius,
                radiusEventQueue = queue,
                result = parallelWriterList,
            };
            int jobs = (points.Length * 2) / batchSize;
            if ((points.Length * 2) % batchSize != 0) jobs++;

            var combinedDependency = JobHandle.CombineDependencies(queueHandle, preparationHandle);

            var allRadiusJob = allRadiusQueryJob.Schedule(jobs, 1, combinedDependency);
            var disposeQueueJob = queue.Dispose(allRadiusJob);
            var disposeWriterListJob = parallelWriterList.Dispose(allRadiusJob);
            return JobHandle.CombineDependencies(disposeQueueJob, disposeWriterListJob);
        }


        public static JobHandle PresortedAllRectangleParallelQuery(Rect allRectangle, NativeArray<float2> points,
            ref NativeList<UnsafeList<int>> result, ref NativeArray<SpecialQueryJobs.ShapeEventPoint> presortedQueue,
            int batchSize = 512, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var parallelWriterList = new NativeList<UnsafeList<int>.ParallelWriter>(points.Length, allocator);

            var updateQueueJob = new SpecialQueryJobs.UpdatePresortedRectEventQueueJob()
            {
                points = points,
                rectEventQueue = presortedQueue,
                height = allRectangle.height,
            };
            var updateHandle = updateQueueJob.Schedule(presortedQueue.Length, 64, dependsOn);

            int offset = batchSize / 2;
            var parallelSortJob = new SpecialQueryJobs.SortPresortedEventQueueParallelJob()
            {
                batchSize = batchSize,
                eventQueue = presortedQueue,
                offset = offset,
            };

            int parallelSortJobs = (presortedQueue.Length / batchSize) + 1;
            if (presortedQueue.Length % batchSize > offset) parallelSortJobs++;
            var parallelSortHandle = parallelSortJob.Schedule(parallelSortJobs, 1, updateHandle);

            var sortJob = new SpecialQueryJobs.SortPresortedEventQueueJob()
            {
                eventQueue = presortedQueue,
            };
            var sortJobHandle = sortJob.Schedule(parallelSortHandle);

            var preparationJob = new SpecialQueryJobs.PrepareParallelQueryJob()
            {
                result = result,
                targetCapacity = points.Length,
                writerList = parallelWriterList,
            };
            var preparationHandle = preparationJob.Schedule(dependsOn);

            var allRectangleQueryJob = new SpecialQueryJobs.AllRectangleParallelQueryJob()
            {
                batchSize = batchSize,
                points = points,
                result = parallelWriterList,
                allRectangle = allRectangle,
                rectEventQueue = presortedQueue
            };
            int jobs = (points.Length * 2) / batchSize;
            if ((points.Length * 2) % batchSize != 0) jobs++;

            var combinedDependency = JobHandle.CombineDependencies(sortJobHandle, preparationHandle);

            var allRectangleJob = allRectangleQueryJob.Schedule(jobs, 1, combinedDependency);
            return parallelWriterList.Dispose(allRectangleJob);
        }



        /// <summary>
        /// Given a collection of points and a rectangle, it will return for each point all other points that are within that rectangle.
        /// It is assumed that the points lie in the center of the search rectangle
        /// </summary>
        /// <param name="allRectangle">The search rectangle for each and all points</param>
        /// <param name="points">The list of points</param>
        /// <param name="result">A list for each point containing the indices of the other points within the rectangle</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle AllRectangleQuery(Rect allRectangle, NativeArray<float2> points, 
            ref NativeList<UnsafeList<int>> result, 
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {

            var queue = new NativeArray<SpecialQueryJobs.ShapeEventPoint>(points.Length * 2, allocator);

            var createQueueJob = new SpecialQueryJobs.CreateRectangleEventQueueJob()
            {
                points = points,
                rectEventQueue = queue,
                height = allRectangle.height,
            };
            var queueHandle = createQueueJob.Schedule(dependsOn);

            var allRectangleJob = new SpecialQueryJobs.AllRectangleQueryJob()
            {
                points = points,
                rect = allRectangle,
                result = result,
                rectEventQueue = queue,
            };
            return allRectangleJob.Schedule(queueHandle);
        }

        public static JobHandle AllRectangleParallelQuery(Rect allRectangle, NativeArray<float2> points,
            ref NativeList<UnsafeList<int>> result, int batchSize = 512,
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var queue = new NativeArray<SpecialQueryJobs.ShapeEventPoint>(points.Length * 2, allocator);
            var parallelWriterList = new NativeList<UnsafeList<int>.ParallelWriter>(points.Length, allocator);

            var createQueueJob = new SpecialQueryJobs.CreateRectangleEventQueueJob()
            {
                points = points,
                height = allRectangle.height,
                rectEventQueue = queue,
            };
            var queueHandle = createQueueJob.Schedule(dependsOn);

            var preparationJob = new SpecialQueryJobs.PrepareParallelQueryJob()
            {
                result = result,
                targetCapacity = points.Length,
                writerList = parallelWriterList,
            };
            var preparationHandle = preparationJob.Schedule(dependsOn);

            var allRectangleQueryJob = new SpecialQueryJobs.AllRectangleParallelQueryJob()
            {
                batchSize = batchSize,
                points = points,
                result = parallelWriterList,
                allRectangle = allRectangle,
                rectEventQueue = queue,
            };
            int jobs = (points.Length * 2) / batchSize;
            if ((points.Length * 2) % batchSize != 0) jobs++;

            var combinedDependency = JobHandle.CombineDependencies(queueHandle, preparationHandle);

            var allRectangleJob = allRectangleQueryJob.Schedule(jobs, 1, combinedDependency);
            var disposeQueueJob = queue.Dispose(allRectangleJob);
            var disposeWriterListJob = parallelWriterList.Dispose(allRectangleJob);
            return JobHandle.CombineDependencies(disposeQueueJob, disposeWriterListJob);
        }
    }
}
