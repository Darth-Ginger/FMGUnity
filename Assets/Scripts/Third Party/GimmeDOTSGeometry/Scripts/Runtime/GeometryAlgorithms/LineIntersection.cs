using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class LineIntersection 
    {
        /// <summary>
        /// Detects all intersections between line segments in a combinatorical way
        /// i. e. each segment is checked against all other segments (which is O(n²))
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="intersections"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FindLineSegmentIntersectionsCombinatorial(NativeList<LineSegment2D> segments, 
            ref NativeList<float2> intersections, JobHandle dependsOn = default)
        {
            var intersectionJob = new LineIntersectionJobs.FindLineSegmentIntersectionsCombinatorial()
            {
                intersections = intersections,
                segments = segments,
            };

            return intersectionJob.Schedule(dependsOn);
        }

        /// <summary>
        /// Detects all intersections between line segments in a combinatorical way
        /// i. e. each segment is checked against all other segments (which is O(n²))
        /// The checks are done in parallel
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="intersections"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FindLineSegmentIntersectionsCombinatorialParallel(NativeList<LineSegment2D> segments, 
            ref NativeList<float2> intersections, JobHandle dependsOn = default)
        {
            //We have to reserve at least n² space for intersections (that would be the worst-case)
            //If you sure that you have less intersections... well, remove this code
            if (intersections.Capacity < segments.Length * segments.Length)
            {
                intersections.Capacity = segments.Length * segments.Length;
            }
            intersections.Clear();

            var intersectionJob = new LineIntersectionJobs.FindLineSegmentIntersectionsCombinatorialParallel()
            {
                intersections = intersections.AsParallelWriter(),
                segments = segments,
            };

            return intersectionJob.Schedule(segments.Length, 64, dependsOn);
        }

        /// <summary>
        /// Detects all intersections between line segments using an output-dependent
        /// sweepline algorithm. In theory this is faster than using the combinatorical
        /// version, but in practice this only happens after having several thousand
        /// line segments. And only if the floating-precision errors are tame (so as
        /// to have as few O(n)-restarts as possible). Use with caution.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="intersections"></param>
        /// <param name="restartOnPrecisionErrors">Restarts (an O(n)-operation) the algorithm status after a precision error has been detected</param>
        /// <param name="epsilon">Choose this value to be smaller than the order of magnitude of the line segments</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FindLineSegmentIntersectionsSweep(NativeList<LineSegment2D> segments,
            ref NativeList<float2> intersections, bool restartOnPrecisionErrors = true, float epsilon = 10e-4f, JobHandle dependsOn = default)
        {
            var prepJob = new LineIntersectionJobs.PrepareLineSegmentsSweep()
            {
                epsilon = epsilon,
                segments = segments,
            };

            var prepHandle = prepJob.Schedule(segments.Length, 64, dependsOn);

            var sweepJob = new LineIntersectionJobs.FindLineIntersectionsSweep()
            {
                epsilon = epsilon,
                intersections = intersections,
                restart = restartOnPrecisionErrors,
                segments = segments,
            };

            return sweepJob.Schedule(prepHandle);
        }


        /// <summary>
        /// Detects all intersections between line segments and a plane in a combinatorical way
        /// i. e. each segment is checked against the plane (which is O(n))
        /// The checks are done in parallel
        /// </summary>
        /// <param name="intersectionPlane"></param>
        /// <param name="segments"></param>
        /// <param name="intersections"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle FindLSPlaneIntersectionsCombinatorialParallel(Plane intersectionPlane, 
            NativeList<LineSegment3D> segments, ref NativeList<float3> intersections, float epsilon = 10e-6f,
            JobHandle dependsOn = default)
        {
            //We have to reserve at least n space for intersections (that would be the worst-case)
            //If you sure that you have less intersections... well, remove this code
            if (intersections.Capacity < segments.Length)
            {
                intersections.Capacity = segments.Length;
            }
            intersections.Clear();

            var intersectionJob = new LineIntersectionJobs.FindLSPlaneIntersectionsCombinatorialParallel()
            {
                intersections = intersections.AsParallelWriter(),
                segments = segments,
                intersectionPlane = intersectionPlane,
                epsilon = epsilon,
            };

            return intersectionJob.Schedule(segments.Length, 64, dependsOn);
        }

    }
}
