using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace GimmeDOTSGeometry
{
    public unsafe struct NativeSparseQuadtree<T> : IQuadtree<T>, IDisposable where T : unmanaged, IEquatable<T>
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

        //Using a list as a stack is way faster than a NativeQueue, because of the internal allocations the queue makes all the time
        [NoAlias]
        private NativeList<int> free;

        #endregion

        public NativeParallelMultiHashMap<uint, T> GetDataBuckets() => this.dataBuckets;
        public NativeList<QuadtreeNode> GetNodes() => this.quadtreeNodes;
        public float3 GetBottomLeftPosition() => this.bottomLeft;
        public float2 GetScale() => this.scale;
        public int GetMaxDepth() => this.maxDepth;

        public int Count => this.count.Value;

        public QuadtreeNode* GetRoot() {
            return (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bounds">Bounds of the quadtree</param>
        /// <param name="maxDepth">Maximum depth / height of the quadtree - also determines the number of cells</param>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        public NativeSparseQuadtree(Rect bounds, int maxDepth, int capacity, Allocator allocator)
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
        public NativeSparseQuadtree(float3 position, float2 scale, int maxDepth, int capacity, Allocator allocator)
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
            this.quadtreeNodes = new NativeList<QuadtreeNode>(capacity, allocator);
            this.free = new NativeList<int>(1, allocator);

            var rootNode = new QuadtreeNode();
            rootNode.children.Length = 4;
            this.quadtreeNodes.Add(rootNode);

            this.allocator = allocator;
            this.count = new NativeReference<int>(allocator);
            this.count.Value = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 GetPositionPercentage(float3 position)
        {
            return (position.xz - this.bottomLeft.xz) / this.scale;
        }

        private int ChildrenCount(QuadtreeNode* node)
        {
            int children = 0;
            for(int i = 0; i < 4; i++)
            {
                if (node->children[i] != 0) children++;
            }
            return children;
        }

        private void DeleteSubtree(uint code)
        {
            var node = this.GetRoot();

            int furthestDepthWithMultipleChildren = -1;
            QuadtreeNode* furthestNodeWithMultipleChildren = null;
            int reachedDepth = 0;
            if (this.ChildrenCount(node) > 1)
            {
                furthestDepthWithMultipleChildren = reachedDepth;
                furthestNodeWithMultipleChildren = node;
            }

            int inverseDepth = this.maxDepth - 1 - reachedDepth;
            uint mask = (uint)(0x00000003 << (inverseDepth * D));
            uint childIndex = (code & mask) >> (inverseDepth * D);

            while (node->children[(int)childIndex] != 0) {

                reachedDepth++;
                node = (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr() + node->children[(int)childIndex];
                if (this.ChildrenCount(node) > 1)
                {
                    furthestDepthWithMultipleChildren = reachedDepth;
                    furthestNodeWithMultipleChildren = node;
                }

                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000003 << (inverseDepth * D));
                childIndex = (code & mask) >> (inverseDepth * D);
            }

            reachedDepth = furthestDepthWithMultipleChildren;
            inverseDepth = this.maxDepth - 1 - reachedDepth;

            mask = (uint)(0x00000003 << (inverseDepth * D));
            childIndex = (code & mask) >> (inverseDepth * D);
            node = furthestNodeWithMultipleChildren;

            while (node->children[(int)childIndex] != 0)
            {
                var parent = node;
                int offset = parent->children[(int)childIndex];
                node = (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr() + offset;
                
                parent->children[(int)childIndex] = 0;
                this.free.Add(offset);

                reachedDepth++;
                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000003 << (inverseDepth * D));
                childIndex = (code & mask) >> (inverseDepth * D);
            }
        }

        private QuadtreeNode* TraverseAsFarAsPossible(uint path, out int reachedDepth)
        {
            reachedDepth = 0;

            var node = this.GetRoot();

            int inverseDepth = this.maxDepth - 1 - reachedDepth;
            uint mask = (uint)(0x00000003 << (inverseDepth * D));
            uint childIndex = (path & mask) >> (inverseDepth * D);

            while (node->children[(int)childIndex] != 0)
            {
                node = (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr() + node->children[(int)childIndex];
                reachedDepth++;
                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000003 << (inverseDepth * D));
                childIndex = (path & mask) >> (inverseDepth * D);
            }
            return node;
        }

        private QuadtreeNode* InsertNodesUntilMaxDepth(QuadtreeNode* node, uint path, int reachedDepth)
        {
            int inverseDepth = this.maxDepth - 1 - reachedDepth;
            uint mask = (uint)(0x00000003 << (inverseDepth * D));
            uint childIndex = (path & mask) >> (inverseDepth * D);

            var newNode = new QuadtreeNode();
            newNode.children.Length = 4;

            int ptr = -1;
            if (this.free.Length > 0)
            {
                int freeIdx = this.free[this.free.Length - 1];
                this.free.Length--;

                this.quadtreeNodes[freeIdx] = newNode;
                ptr = freeIdx;
            }
            else
            {
                this.quadtreeNodes.Add(newNode);
                ptr = this.quadtreeNodes.Length - 1;
            }

            node->children[(int)childIndex] = ptr;
            node = (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr() + ptr;
            reachedDepth++;

            while(reachedDepth != this.maxDepth)
            {
                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000003 << (inverseDepth * D));
                childIndex = (path & mask) >> (inverseDepth * D);

                newNode = new QuadtreeNode();
                newNode.children.Length = 4;

                if (this.free.Length > 0)
                {
                    int freeIdx = this.free[this.free.Length - 1];
                    this.free.Length--;

                    this.quadtreeNodes[freeIdx] = newNode;
                    ptr = freeIdx;
                }
                else
                {
                    this.quadtreeNodes.Add(newNode);
                    ptr = this.quadtreeNodes.Length - 1;
                }

                node->children[(int)childIndex] = ptr;
                node = (QuadtreeNode*)this.quadtreeNodes.GetUnsafePtr() + ptr;
                reachedDepth++;
            }
            return (QuadtreeNode*)node->children[(int)childIndex];
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
            if (this.count.Value + 1 >= this.dataBuckets.Capacity)
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

                if(this.dataBuckets.ContainsKey(code))
                {
                    this.CheckForResize();
                    this.dataBuckets.Add(code, value);
                    this.count.Value = this.count.Value + 1;
                } else
                {
                    var closestNode = this.TraverseAsFarAsPossible(code, out int reachedDepth);
                    this.InsertNodesUntilMaxDepth(closestNode, code, reachedDepth);

                    this.CheckForResize();
                    this.dataBuckets.Add(code, value);
                    this.count.Value = this.count.Value + 1;
                }
            }
        }

        public void Clear()
        {
            this.dataBuckets.Clear();
            this.quadtreeNodes.Clear();
            this.free.Clear();
            this.count.Value = 0;
        }

        /// <summary>
        /// Important Note: If you have a lot of things moving in the quadtree, it will be a lot cheaper
        /// performance-wise to recreate the whole tree by calling Clear() and then reinserting each
        /// element.
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

            if (newCode != oldCode 
                && math.all(newPercentage >= 0.0f) 
                && math.all(newPercentage <= 1.0f))
            {
                if (this.Remove(oldPosition, value))
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
            if(math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int2 coord = (int2)(percentage * this.axisDivisions);

                uint code = MathUtilDOTS.PositionToMortonCode(coord);

                if(this.dataBuckets.TryGetFirstValue(code, out T val, out var it))
                {
                    if(val.Equals(value))
                    {
                        this.dataBuckets.Remove(it);
                        this.count.Value = this.count.Value - 1;

                        //If we removed the last element in the bucket, we have to update the quadtree nodes
                        if (!this.dataBuckets.ContainsKey(code))
                        {
                            this.DeleteSubtree(code);
                        }
                        return true;
                    }

                    while(this.dataBuckets.TryGetNextValue(out val, ref it))
                    {
                        if(val.Equals(value))
                        {
                            this.dataBuckets.Remove(it);
                            this.count.Value = this.count.Value - 1;

                            if (!this.dataBuckets.ContainsKey(code))
                            {
                                this.DeleteSubtree(code);
                            }

                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position"></param>
        /// <returns>The coordinates of the cell closest to the given position. They are
        /// unchecked (might be negative or bigger than the number of cells in the tree)</returns>
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

                uint mortonCode = MathUtilDOTS.PositionToMortonCode(coord);

                if (this.dataBuckets.ContainsKey(mortonCode))
                {
                    code = mortonCode;
                    success = true;
                }
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

            [NoAlias, WriteOnly]
            public NativeList<uint> result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(QuadtreeNode node, Rect parentRect, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    if(ShapeOverlap.RectangleCircleOverlap(parentRect, this.center, this.radiusSq))
                    {
                        this.result.Add(currentCode);
                    }
                }
                else
                {

                    var rectangleBuffer = new NativeList<Rect>(4, Allocator.Temp);
                    parentRect.Subdivide(ref rectangleBuffer);

                    var circleMin = this.center - this.radius;
                    var circleMax = this.center + this.radius;

                    var circleRect = Rect.MinMaxRect(circleMin.x, circleMin.y, circleMax.x, circleMax.y);

                    int inverseDepth = this.maxDepth - 1 - depth;
                    for (int i = 0; i < rectangleBuffer.Length; i++)
                    {
                        var subRect = rectangleBuffer[i];
                        if (subRect.Overlaps(circleRect))
                        {
                            int nextNode = node.children[i];

                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInRadiusRecursion(this.quadtreeNodes[nextNode], subRect, depth + 1, nextCode);
                            }
                        }
                    }
                }
            }

            public void Execute()
            {
                this.result.Clear();

                float2 min = new float2(this.bottomLeft.x, this.bottomLeft.z);
                float2 max = min + this.scale;

                this.radiusSq = this.radius * this.radius;

                var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                this.GetCellsInRadiusRecursion(this.quadtreeNodes[0], rect, 0, 0);
            }
        }

        /// <summary>
        /// Searches for all cells within a given circle (center + radius) and writes
        /// the morton code (cell ids) to a result-list
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
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

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(int index, QuadtreeNode node, Rect parentRect, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    if (ShapeOverlap.RectangleCircleOverlap(parentRect, this.centers[index], this.radiusSq))
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

                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInRadiusRecursion(index, this.quadtreeNodes[nextNode], subRect, depth + 1, nextCode);
                            }
                        }
                    }
                }
            }

            public void Execute(int index)
            {
                this.radiusSq = this.radii[index] * this.radii[index];

                float2 min = new float2(this.bottomLeft.x, this.bottomLeft.z);
                float2 max = min + this.scale;

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

            [NoAlias, WriteOnly]
            public NativeList<uint> result;

            private void GetCellsInRectangleRecursion(QuadtreeNode node, float2 min, float2 max, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    this.result.Add(currentCode);
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
                        if (subRect.Overlaps(this.rect))
                        {
                            int nextNode = node.children[i];
                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInRectangleRecursion(this.quadtreeNodes[nextNode], subRect.min, subRect.max, depth + 1, nextCode);
                            }
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

        /// <summary>
        /// Searches for all cells within the given rectangle and writes
        /// the morton code (cell ids) to a result-list
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle GetCellsInRectangle(Rect rect, ref NativeList<uint> result, JobHandle dependsOn = default)
        {
            var job = new GetCellsInRectangleJob()
            {
                bottomLeft = this.bottomLeft,
                maxDepth = this.maxDepth,
                quadtreeNodes = this.quadtreeNodes,
                result = result,
                scale = this.scale,
                rect = rect
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

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private void GetCellsInRectangleRecursion(int index, QuadtreeNode node, float2 min, float2 max, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    this.result.Add(currentCode);
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
                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInRectangleRecursion(index, this.quadtreeNodes[nextNode], subRect.min, subRect.max, depth + 1, nextCode);
                            }
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


        public bool IsCreated => this.dataBuckets.IsCreated || this.quadtreeNodes.IsCreated || this.free.IsCreated || this.count.IsCreated;

        public void Dispose()
        {
            if(this.dataBuckets.IsCreated)
            {
                this.dataBuckets.Dispose();
            }
            this.quadtreeNodes.DisposeIfCreated();
            this.free.DisposeIfCreated();
            this.count.DisposeIfCreated();
        }

        public void DisposeIfCreated()
        {
            if (this.IsCreated) this.Dispose();
        }
    }
}
