using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe interface IOctree<T> : IDisposable where T : unmanaged
    {

        public bool IsCreated { get; }
        public int Count { get; }

        public void Insert(float3 position, T value);
        public bool Remove(float3 position, T value);


        public bool Update(T value, float3 oldPosition, float3 newPosition);

        public int3 GetCellCoordinates(float3 position);


        public JobHandle GetCellsInRadius(float3 center, float radius, ref NativeList<uint> result, JobHandle dependsOn);

        //A single query takes comparatively long, so default innerLoopBatchCount is 1
        public JobHandle GetCellsInRadii(NativeArray<float3> centers, NativeArray<float> radii,
            ref NativeParallelHashSet<uint> result, JobHandle dependsOn, int innerLoopBatchCount = 1);


        public JobHandle GetCellsInBounds(Bounds bounds, ref NativeList<uint> result, JobHandle dependsOn);

        //A single query takes comparatively long, so default innerLoopBatchCount is 1
        public JobHandle GetCellsInBounds(NativeArray<Bounds> bounds, ref NativeParallelHashSet<uint> result,
            JobHandle dependsOn, int innerLoopBatchCount = 1);



        public NativeList<OctreeNode> GetNodes();
        public float3 GetBottomLeftPosition();
        public float3 GetScale();
        public NativeParallelMultiHashMap<uint, T> GetDataBuckets();

        public int GetMaxDepth();

        public OctreeNode* GetRoot();

        public void Clear();
    }
}
