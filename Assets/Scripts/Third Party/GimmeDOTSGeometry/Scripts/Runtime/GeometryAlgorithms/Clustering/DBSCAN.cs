using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class DBSCAN
    {
        public static int UNDEFINED_LABEL = -1;
        public static int NOISE_LABEL = 0;

        [BurstCompile]
        public unsafe struct DBSCAN2DJob : IJob
        {
            [ReadOnly, NoAlias]
            public NativeArray<float2> points;

            [ReadOnly, NoAlias]
            public NativeList<UnsafeList<int>> neighbors;

            [NoAlias]
            public NativeArray<int> clusters;

            public int minPts;

            public int noiseLabel;
            public int undefinedLabel;

            public void Execute()
            {
                UnsafeUtility.MemSet(this.clusters.GetUnsafePtr(), byte.MaxValue, sizeof(int) * this.points.Length);
                int clusterCount = 0;
                UnsafeList<int> pointNeighbors = new UnsafeList<int>(4, Allocator.Temp);
                for(int i = 0; i < this.points.Length; i++)
                {
                    pointNeighbors.Clear();
                    int currentCluster = this.clusters[i];
                    if (currentCluster != this.undefinedLabel) continue;

                    pointNeighbors.AddRange(this.neighbors[i]);
                    if(pointNeighbors.Length < this.minPts)
                    {
                        this.clusters[i] = this.noiseLabel;
                        continue;
                    }

                    clusterCount++;
                    this.clusters[i] = clusterCount;

                    for(int j = 0; j < pointNeighbors.Length; j++)
                    {
                        int neighborIdx = pointNeighbors[j];
                        if (neighborIdx == i) continue;

                        if (this.clusters[neighborIdx] == this.noiseLabel) this.clusters[neighborIdx] = clusterCount;
                        if (this.clusters[neighborIdx] > this.undefinedLabel) continue;

                        this.clusters[neighborIdx] = clusterCount;
                        var neighbors = this.neighbors[neighborIdx];
                        if(neighbors.Length >= this.minPts)
                        {
                            pointNeighbors.AddRange(neighbors);
                        }
                    }

                }
            }
        }

        public static JobHandle DBSCAN2D(NativeList<float2> points, int minPts, float epsilon,
            ref NativeList<UnsafeList<int>> neighbors, ref NativeList<int> clusters, int batchSize = 512, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {

            var allRadiusHandle = SpecialQuery.AllRadiusParallelQuery(epsilon, points.AsArray(), ref neighbors, batchSize, allocator, dependsOn);

            var dbscanJob = new DBSCAN2DJob()
            {
                clusters = clusters.AsArray(),
                neighbors = neighbors,
                points = points.AsArray(),
                minPts = minPts,
                noiseLabel = NOISE_LABEL,
                undefinedLabel = UNDEFINED_LABEL,
            };

            var dbscanHandle = dbscanJob.Schedule(allRadiusHandle);

            return dbscanHandle;
        }


        public static JobHandle DBSCAN2D(NativeArray<float2> points, int minPts, float epsilon,
            ref NativeArray<int> clusters, Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            NativeList<UnsafeList<int>> neighbors = new NativeList<UnsafeList<int>>(allocator);
            var allRadiusHandle = SpecialQuery.AllRadiusQuery(epsilon, points, ref neighbors, allocator, dependsOn);

            var dbscanJob = new DBSCAN2DJob()
            {
                clusters = clusters,
                neighbors = neighbors,
                points = points,
                minPts = minPts,
                noiseLabel = NOISE_LABEL,
            };

            var dbscanHandle = dbscanJob.Schedule(allRadiusHandle);

            return dbscanHandle;
        }
 
    }
}
