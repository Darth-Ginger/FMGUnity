using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class PositionFilterJobs
    {

        [BurstCompile]
        public struct Position2DRadiusFilterJob : IJob
        {

            public float2 center;
            public float radius;

            [NoAlias, ReadOnly]
            public NativeArray<float2> positions;

            [NoAlias, WriteOnly]
            public NativeList<float2> result;

            public void Execute()
            {
                float radiusSq = this.radius * this.radius;
                for(int i = 0; i < this.positions.Length; i++)
                {
                    if (math.distancesq(this.positions[i], this.center) < radiusSq)
                    {
                        this.result.Add(this.positions[i]);
                    }
                }
            }
        }

        [BurstCompile]
        public struct Position3DRadiusFilterJob : IJob
        {

            public float3 center;
            public float radius;

            [NoAlias, ReadOnly]
            public NativeArray<float3> positions;

            [NoAlias, WriteOnly]
            public NativeList<float3> result;

            public void Execute()
            {
                float radiusSq = this.radius * this.radius;
                for(int i = 0; i < this.positions.Length; i++)
                {
                    if (math.distancesq(this.positions[i], this.center) < radiusSq)
                    {
                        this.result.Add(this.positions[i]);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>All positions within the circle defined by the radius and center</returns>
        public static JobHandle FilterPositionsOutsideRadius(float2 center, float radius, NativeArray<float2> positions,
            ref NativeList<float2> filteredPositions, JobHandle dependsOn = default)
        {
            var job = new Position2DRadiusFilterJob()
            {
                center = center,
                radius = radius,
                positions = positions,
                result = filteredPositions
            };

            return job.Schedule(dependsOn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>All positions within the sphere defined by the radius and center</returns>
        public static JobHandle FilterPositionsOutsideRadius(float3 center, float radius, NativeArray<float3> positions,
            ref NativeList<float3> filteredPositions, JobHandle dependsOn = default)
        {
            var job = new Position3DRadiusFilterJob()
            {
                center = center,
                radius = radius,
                positions = positions,
                result = filteredPositions
            };

            return job.Schedule(dependsOn);
        }
    }
}
