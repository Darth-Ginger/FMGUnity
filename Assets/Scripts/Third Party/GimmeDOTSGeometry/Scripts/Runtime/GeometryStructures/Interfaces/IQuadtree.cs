using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe interface IQuadtree<T> : IDisposable where T : unmanaged
    {

        public bool IsCreated { get; }
        public int Count { get; }

        public void Insert(float3 position, T value);
        public bool Remove(float3 position, T value);


        public bool Update(T value, float3 oldPosition, float3 newPosition);

        public int2 GetCellCoordinates(float3 position);


        public JobHandle GetCellsInRadius(float2 center, float radius, ref NativeList<uint> result, JobHandle dependsOn);

        //A single query takes comparatively long, so default innerLoopBatchCount is 1
        public JobHandle GetCellsInRadii(NativeArray<float2> centers, NativeArray<float> radii, 
            ref NativeParallelHashSet<uint> result, JobHandle dependsOn, int innerLoopBatchCount = 1);


        public JobHandle GetCellsInRectangle(Rect rect, ref NativeList<uint> result, JobHandle dependsOn);

        //A single query takes comparatively long, so default innerLoopBatchCount is 1
        public JobHandle GetCellsInRectangles(NativeArray<Rect> rectangles, ref NativeParallelHashSet<uint> result, 
            JobHandle dependsOn, int innerLoopBatchCount = 1);


        public NativeParallelMultiHashMap<uint, T> GetDataBuckets();
        public NativeList<QuadtreeNode> GetNodes();
        public float3 GetBottomLeftPosition();
        public float2 GetScale();
        public int GetMaxDepth();

        public bool GetCell(float3 position, out uint code);

        public QuadtreeNode* GetRoot();

        public void Clear();
    }
}
