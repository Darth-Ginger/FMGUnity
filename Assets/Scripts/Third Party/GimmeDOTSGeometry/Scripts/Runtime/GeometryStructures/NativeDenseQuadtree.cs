using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe struct NativeDenseQuadtree<T> : IQuadtree<T>, IDisposable where T : unmanaged, IEquatable<T>
    {

        private static readonly int D = 2;

        #region Private Variables

        private Allocator allocator;

        private int maxDepth;
        private int axisDivisions;

        private float3 bottomLeft;
        private float2 scale;

        private NativeList<QuadtreeNode> quadtreeNodes;
        private NativeParallelMultiHashMap<uint, T> dataBuckets;
        private NativeReference<int> count;

        #endregion

        public NativeParallelMultiHashMap<uint, T> GetDataBuckets() => this.dataBuckets;

        public NativeList<QuadtreeNode> GetNodes() => this.quadtreeNodes;

        public float3 GetBottomLeftPosition() => this.bottomLeft;

        public float2 GetScale() => this.scale;

        public int GetMaxDepth() => this.maxDepth;


        public int Count => this.count.Value;


        public QuadtreeNode* GetRoot()
        {
            return (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr();
        }


        private void AddAllNodesRecursively(QuadtreeNode* parent, int depth)
        {
            if (depth >= this.maxDepth) return;

            for(int i = 0; i < 4; i++)
            {
                var childNode = new QuadtreeNode();
                childNode.children.Length = 4;

                this.quadtreeNodes.Add(childNode);
                int ptr = this.quadtreeNodes.Length - 1;

                parent->children[i] = ptr;

                this.AddAllNodesRecursively((QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr() + ptr, depth + 1);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bounds">Bounds of the quadtree</param>
        /// <param name="maxDepth">Maximum depth / height of the quadtree - also determines the number of cells</param>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        public NativeDenseQuadtree(Rect bounds, int maxDepth, int capacity, Allocator allocator)
            : this(new float3(bounds.center.x, 0.0f, bounds.center.y), new float2(bounds.width, bounds.height), maxDepth, capacity, allocator)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position">Center of the quadtree</param>
        /// <param name="scale">The size of the quadtree in meters</param>
        /// <param name="maxDepth">Maximum depth / height of the quadtree - also determines the number of cells</param>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        public NativeDenseQuadtree(float3 position, float2 scale, int maxDepth, int capacity, Allocator allocator)
        {
            if (maxDepth <= 0)
            {
                Debug.LogError("Max depth must be a value greater than 0");
            }
            else if (maxDepth >= 16)
            {
                Debug.LogError("Max depth can't be more than 16, as it would not be possible to lay a z-order curve with 32-bits precision");
            }

            this.maxDepth = maxDepth;
            this.axisDivisions = 1 << this.maxDepth;

            this.scale = scale;
            this.bottomLeft = position - new float3(this.scale.x, 0.0f, this.scale.y) * 0.5f;

            this.dataBuckets = new NativeParallelMultiHashMap<uint, T>(capacity, allocator);

            //Sum of [x² / 4 ^ i] from i = 0 to ld x
            int completeNodeCount = (4 * this.axisDivisions * this.axisDivisions - 1) / 3;

            this.quadtreeNodes = new NativeList<QuadtreeNode>(completeNodeCount, allocator);

            var rootNode = new QuadtreeNode();
            rootNode.children.Length = 4;
            this.quadtreeNodes.Add(rootNode);

            this.allocator = allocator;
            this.count = new NativeReference<int>(allocator);
            this.count.Value = 0;

            this.AddAllNodesRecursively((QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr(), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 GetPositionPercentage(float3 position)
        {
            return (position.xz - this.bottomLeft.xz) / this.scale;
        }

        //Generic Methods do not work in Burst (only in the editor), so we have to copy the code sadly
        private void ResizeAndCopy()
        {
            var newHashMap = new NativeParallelMultiHashMap<uint, T>(this.dataBuckets.Capacity * 2, this.allocator);
            var copyJob = new NativeParallelMultiHashMapJobs.CopyParallelMultiHashMapJob<uint, T>
            {
                destination = newHashMap,
                source = this.dataBuckets
            };
            //One day - one day, I am allowed to replace this with .Schedule().Complete(), once Burst allows generic methods.
            //Sry, that this is slow until then...
            copyJob.Execute();

            this.dataBuckets.Dispose();
            this.dataBuckets = newHashMap;
        }

        private void CheckForResize()
        {
            if(this.count.Value + 1 >= this.dataBuckets.Capacity)
            {
                this.ResizeAndCopy();
            }
        }

        public void Insert(float3 position, T value)
        {
            var percentage = this.GetPositionPercentage(position);
            if(math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int2 coord = (int2)(percentage * this.axisDivisions);

                uint code = MathUtilDOTS.PositionToMortonCode(coord);

                this.CheckForResize();
                this.dataBuckets.Add(code, value);
                this.count.Value = this.count.Value + 1;
            }
        }

        public void Clear()
        {
            //Keep the tree nodes, only clear the data
            this.dataBuckets.Clear();
            this.count.Value = 0;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="oldPosition"></param>
        /// <param name="newPosition"></param>
        /// <returns>Returns false if the value was not found at the old position or the new position is outside the quadtree</returns>
        public bool Update(T value, float3 oldPosition, float3 newPosition)
        {
            var newPercentage = this.GetPositionPercentage(newPosition);
            var oldPercentage = this.GetPositionPercentage(oldPosition);

            int2 newCoord = (int2)(newPercentage * this.axisDivisions);
            int2 oldCoord = (int2)(oldPercentage * this.axisDivisions);

            uint newCode = MathUtilDOTS.PositionToMortonCode(newCoord);
            uint oldCode = MathUtilDOTS.PositionToMortonCode(oldCoord);

            if(newCode != oldCode
                && math.all(newPercentage >= 0.0f)
                && math.all(newPercentage <= 1.0f))
            {
                if(this.Remove(oldPosition, value))
                {
                    this.Insert(newPosition, value);
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="value"></param>
        /// <returns>True, if the value was found in the bucket that represents the given position. False otherwise. Note, that if the function returns false,
        /// that that does not necessarily mean the value is not present somewhere in the quadtree, just that it couldn't be found where you would expect it.</returns>
        public bool Remove(float3 position, T value)
        {
            var percentage = this.GetPositionPercentage(position);
            if (math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int2 coord = (int2)(percentage * this.axisDivisions);

                uint code = MathUtilDOTS.PositionToMortonCode(coord);

                if (this.dataBuckets.TryGetFirstValue(code, out T val, out var it))
                {
                    if (value.Equals(val))
                    {
                        this.dataBuckets.Remove(it);
                        this.count.Value = this.count.Value - 1;

                        return true;
                    }

                    while (this.dataBuckets.TryGetNextValue(out val, ref it))
                    {
                        if (val.Equals(value))
                        {
                            this.dataBuckets.Remove(it);
                            this.count.Value = this.count.Value - 1;

                            return true;
                        }
                    }
                }
            }
            return false;
        }


        public int2 GetCellCoordinates(float3 position)
        {
            var percentage = this.GetPositionPercentage(position);
            return (int2)(percentage * this.axisDivisions);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="code"></param>
        /// <returns>The morton code (cell id) for the closest cell to the given position.
        /// If the position is outside the bounds of this quadtree or the cell does not exist,
        /// false is returned</returns>
        public bool GetCell(float3 position, out uint code)
        {
            bool success = false;
            code = uint.MaxValue;
            var percentage = this.GetPositionPercentage(position);
            if (math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int2 coord = (int2)(percentage * this.axisDivisions);

                code = MathUtilDOTS.PositionToMortonCode(coord);
                success = true;
            }
            return success;
        }

        [BurstCompile]
        public struct GetCellsInRadiusJob : IJob
        {

            public float3 bottomLeft;
            public float2 scale;

            public float2 center;
            public float radius;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeList<QuadtreeNode> quadtreeNodes;

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeList<uint> result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(QuadtreeNode node, Rect parentRect, int depth, uint currentCode)
            {

                if(depth == this.maxDepth)
                {
                    if(ShapeOverlap.RectangleCircleOverlap(parentRect, this.center, this.radiusSq)
                        //Only add buckets that contain some data
                        && this.dataBuckets.ContainsKey(currentCode))
                    {
                        this.result.Add(currentCode);
                    }
                } else
                {
                    var rectangleBuffer = new NativeList<Rect>(4, Allocator.Temp);
                    parentRect.Subdivide(ref rectangleBuffer);

                    var circleMin = this.center - this.radius;
                    var circleMax = this.center + this.radius;

                    var circleRect = Rect.MinMaxRect(circleMin.x, circleMin.y, circleMax.x, circleMax.y);

                    int inverseDepth = this.maxDepth - 1 - depth;
                    for(int i = 0; i < rectangleBuffer.Length; i++)
                    {
                        var subRect = rectangleBuffer[i];
                        if(subRect.Overlaps(circleRect))
                        {
                            int nextNode = node.children[i];
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInRadiusRecursion(this.quadtreeNodes[nextNode], subRect, depth + 1, nextCode);
                        }
                    }
                }

            }

            public void Execute()
            {
                this.result.Clear();
                this.radiusSq = this.radius * this.radius;

                float2 min = new float2(this.bottomLeft.x, this.bottomLeft.z);
                float2 max = min + this.scale;

                var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                this.GetCellsInRadiusRecursion(this.quadtreeNodes[0], rect, 0, 0);
            }
        }


        public JobHandle GetCellsInRadius(float2 center, float radius, ref NativeList<uint> result, JobHandle dependsOn = default)
        {
            var job = new GetCellsInRadiusJob()
            {
                bottomLeft = this.bottomLeft,
                center = center,
                maxDepth = this.maxDepth,
                quadtreeNodes = this.quadtreeNodes,
                radius = radius,
                result = result,
                scale = this.scale,
                dataBuckets = this.dataBuckets
            };

            return job.Schedule(dependsOn);
        }


        [BurstCompile]
        public struct GetCellsInRadiiJob : IJobParallelFor
        {

            public float3 bottomLeft;
            public float2 scale;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeArray<float2> centers;
            [NoAlias, ReadOnly]
            public NativeArray<float> radii;

            [NoAlias, ReadOnly]
            public NativeList<QuadtreeNode> quadtreeNodes;

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(int index, QuadtreeNode node, Rect parentRect, int depth, uint currentCode)
            {

                if (depth == this.maxDepth)
                {
                    if (ShapeOverlap.RectangleCircleOverlap(parentRect, this.centers[index], this.radiusSq)
                        //Only add buckets that contain some data
                        && this.dataBuckets.ContainsKey(currentCode))
                    {
                        this.result.Add(currentCode);
                    }
                }
                else
                {
                    var rectangleBuffer = new NativeList<Rect>(4, Allocator.Temp);
                    parentRect.Subdivide(ref rectangleBuffer);

                    var circleMin = this.centers[index] - this.radii[index];
                    var circleMax = this.centers[index] + this.radii[index];

                    var circleRect = Rect.MinMaxRect(circleMin.x, circleMin.y, circleMax.x, circleMax.y);

                    int inverseDepth = this.maxDepth - 1 - depth;
                    for (int i = 0; i < rectangleBuffer.Length; i++)
                    {
                        var subRect = rectangleBuffer[i];
                        if (subRect.Overlaps(circleRect))
                        {
                            int nextNode = node.children[i];
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInRadiusRecursion(index, this.quadtreeNodes[nextNode], subRect, depth + 1, nextCode);
                        }
                    }
                }

            }

            public void Execute(int index)
            {
                float2 min = new float2(this.bottomLeft.x, this.bottomLeft.z);
                float2 max = min + this.scale;

                this.radiusSq = this.radii[index] * this.radii[index];

                var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                this.GetCellsInRadiusRecursion(index, this.quadtreeNodes[0], rect, 0, 0);
            }
        }


        public JobHandle GetCellsInRadii(NativeArray<float2> centers, NativeArray<float> radii,
            ref NativeParallelHashSet<uint> result, JobHandle dependsOn = default, int innerLoopBatchCount = 1)
        {
            if (centers.Length != radii.Length)
            {
                Debug.LogError("Centers and Radii arrays do not match in size!");
            }

            result.Clear();

            var job = new GetCellsInRadiiJob()
            {
                bottomLeft = this.bottomLeft,
                centers = centers,
                maxDepth = this.maxDepth,
                quadtreeNodes = this.quadtreeNodes,
                radii = radii,
                result = result.AsParallelWriter(),
                scale = this.scale,
                dataBuckets = this.dataBuckets
            };

            return job.Schedule(centers.Length, innerLoopBatchCount, dependsOn);
        }

        [BurstCompile]
        public struct GetCellsInRectangleJob : IJob
        {

            public float3 bottomLeft;
            public float2 scale;

            public int maxDepth;

            public Rect rect;


            [NoAlias, ReadOnly]
            public NativeList<QuadtreeNode> quadtreeNodes;

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeList<uint> result;

            private void GetCellsInRectangleRecursion(QuadtreeNode node, float2 min, float2 max, int depth, uint currentCode)
            {
                if(depth == this.maxDepth)
                {
                    if (this.dataBuckets.ContainsKey(currentCode))
                    {
                        this.result.Add(currentCode);
                    }
                } else
                {
                    var parentRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                    var rectangleBuffer = new NativeList<Rect>(4, Allocator.Temp);
                    parentRect.Subdivide(ref rectangleBuffer);


                    int inverseDepth = this.maxDepth - 1 - depth;
                    for (int i = 0; i < rectangleBuffer.Length; i++)
                    {
                        var subRect = rectangleBuffer[i];
                        if (subRect.Overlaps(this.rect))
                        {
                            int nextNode = node.children[i];
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInRectangleRecursion(this.quadtreeNodes[nextNode], subRect.min, subRect.max, depth + 1, nextCode);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                float2 min = new float2(this.bottomLeft.x, this.bottomLeft.z);
                float2 max = min + this.scale;

                this.GetCellsInRectangleRecursion(this.quadtreeNodes[0], min, max, 0, 0);
            }
        }

        public JobHandle GetCellsInRectangle(Rect rect, ref NativeList<uint> result, JobHandle dependsOn = default)
        {
            var job = new GetCellsInRectangleJob()
            {
                bottomLeft = this.bottomLeft,
                maxDepth = this.maxDepth,
                quadtreeNodes = this.quadtreeNodes,
                dataBuckets = this.dataBuckets,
                rect = rect,
                result = result,
                scale = this.scale,
            };

            return job.Schedule(dependsOn);
        }



        [BurstCompile]
        public struct GetCellsInRectanglesJob : IJobParallelFor
        {

            public float3 bottomLeft;
            public float2 scale;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeArray<Rect> rectangles;


            [NoAlias, ReadOnly]
            public NativeList<QuadtreeNode> quadtreeNodes;

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private void GetCellsInRectangleRecursion(int index, QuadtreeNode node, float2 min, float2 max, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    if (this.dataBuckets.ContainsKey(currentCode))
                    {
                        this.result.Add(currentCode);
                    }
                }
                else
                {
                    var parentRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                    var rectangleBuffer = new NativeList<Rect>(4, Allocator.Temp);
                    parentRect.Subdivide(ref rectangleBuffer);


                    int inverseDepth = this.maxDepth - 1 - depth;
                    for (int i = 0; i < rectangleBuffer.Length; i++)
                    {
                        var subRect = rectangleBuffer[i];
                        if (subRect.Overlaps(this.rectangles[index]))
                        {
                            int nextNode = node.children[i];
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInRectangleRecursion(index, this.quadtreeNodes[nextNode], subRect.min, subRect.max, depth + 1, nextCode);
                        }
                    }

                }
            }

            public void Execute(int index)
            {

                float2 min = new float2(this.bottomLeft.x, this.bottomLeft.z);
                float2 max = min + this.scale;

                this.GetCellsInRectangleRecursion(index, this.quadtreeNodes[0], min, max, 0, 0);
            }
        }

        public JobHandle GetCellsInRectangles(NativeArray<Rect> rectangles, ref NativeParallelHashSet<uint> result,
            JobHandle dependsOn = default, int innerLoopBatchCount = 1)
        {
            result.Clear();

            var job = new GetCellsInRectanglesJob()
            {
                bottomLeft = this.bottomLeft,
                maxDepth = this.maxDepth,
                quadtreeNodes = this.quadtreeNodes,
                result = result.AsParallelWriter(),
                scale = this.scale,
                rectangles = rectangles
            };

            return job.Schedule(rectangles.Length, innerLoopBatchCount, dependsOn);
        }

        public bool IsCreated => this.dataBuckets.IsCreated || this.quadtreeNodes.IsCreated || this.count.IsCreated;

        public void Dispose()
        {
            if (this.dataBuckets.IsCreated)
            {
                this.dataBuckets.Dispose();
            }
            this.quadtreeNodes.DisposeIfCreated();
            this.count.DisposeIfCreated();
        }

        public void DisposeIfCreated()
        {
            if (this.IsCreated) this.Dispose();
        }
    }
}
