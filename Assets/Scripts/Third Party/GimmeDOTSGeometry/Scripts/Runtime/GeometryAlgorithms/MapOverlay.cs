using Unity.Collections;
using Unity.Jobs;

namespace GimmeDOTSGeometry
{
    public static class MapOverlay
    {

        /// <summary>
        /// Calculates the overlay between two doubly-connected edge lists
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle ComputeOverlay(DCEL a, DCEL b, ref DCEL output, ref AttributeMap attributes, float epsilon = 10e-5f, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {

            NativeList<MapOverlayJobs.LS2D> lineSegments = new NativeList<MapOverlayJobs.LS2D>(allocator);

            var mapOverlayMergeJob = new MapOverlayJobs.MapOverlayMergeJob()
            {
                dcelA = a,
                dcelB = b,
                mergedDCEL = output,
            };

            var mapOverlayDCELSegmentsJob = new MapOverlayJobs.MapOverlayDCELSegmentsJob()
            {
                lineSegments = lineSegments,
                mergedDCEL = output,
            };

            var mapOverlaySweepJob = new MapOverlayJobs.MapOverlaySweepJob()
            {
                epsilon = epsilon,
                lineSegments = lineSegments,
            };

            var mergeHandle = mapOverlayMergeJob.Schedule(dependsOn);
            var dcelSegmentsHandle = mapOverlayDCELSegmentsJob.Schedule(mergeHandle);
            var sweepHandle = mapOverlaySweepJob.Schedule(dcelSegmentsHandle);
            var disposeSegmentsHandle = lineSegments.Dispose(sweepHandle);

            return disposeSegmentsHandle;
        }

        /// <summary>
        /// Calculates the overlay between two polygons (internally converted to DCELs)
        /// </summary>
        /// <param name="polygonA"></param>
        /// <param name="polygonB"></param>
        /// <param name="faceA"></param>
        /// <param name="faceB"></param>
        /// <param name="output"></param>
        /// <param name="attributes"></param>
        /// <param name="allocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public static JobHandle ComputeOverlay(NativePolygon2D polygonA, NativePolygon2D polygonB, int faceA, int faceB, ref DCEL output, 
            ref AttributeMap attributes, float epsilon = 10e-5f, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var dcelA = NativePolygon2D.ConvertToDCEL(polygonA, faceA, allocator);
            var dcelB = NativePolygon2D.ConvertToDCEL(polygonB, faceB, allocator);

            var overlayHandle = ComputeOverlay(dcelA, dcelB, ref output, ref attributes, epsilon, allocator, dependsOn);

            var disposeDCELA = dcelA.Dispose(overlayHandle);
            var disposeDCELB = dcelB.Dispose(disposeDCELA);

            return disposeDCELB;
        }

    }
}
