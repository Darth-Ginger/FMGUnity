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
    public unsafe struct NativeSparseOctree<T> : IOctree<T>, IDisposable where T : unmanaged, IEquatable<T>
    {

        private static readonly int D = 3;

        #region Private Variables

        private Allocator allocator;

        private int maxDepth;
        private int axisDivisions;

        private float3 bottomLeftDown;
        private float3 scale;

        private NativeList<OctreeNode> octreeNodes;
        private NativeParallelMultiHashMap<uint, T> dataBuckets;
        private NativeReference<int> count;

        //Using a list as a stack is way faster than a NativeQueue, because of the internal allocations the queue makes all the time
        [NoAlias]
        private NativeList<int> free;


        #endregion

        public NativeParallelMultiHashMap<uint, T> GetDataBuckets() => this.dataBuckets;
        public NativeList<OctreeNode> GetNodes() => this.octreeNodes;
        public float3 GetBottomLeftPosition() => this.bottomLeftDown;
        public float3 GetScale() => this.scale;
        public int GetMaxDepth() => this.maxDepth;

        public int Count => this.count.Value;

        public OctreeNode* GetRoot()
        {
            return (OctreeNode*)this.octreeNodes.GetUnsafePtr();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bounds">Bounds of the octree</param>
        /// <param name="maxDepth">Maximum depth / height of the octree - also determines the number of cells</param>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        public NativeSparseOctree(Bounds bounds, int maxDepth, int capacity, Allocator allocator)
            : this(bounds.center, bounds.size, maxDepth, capacity, allocator)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position">Center of the octree</param>
        /// <param name="scale">The size of the octree in meters</param>
        /// <param name="maxDepth">Maximum depth / height of the octree - also determines the number of cells</param>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        public NativeSparseOctree(float3 position, float3 scale, int maxDepth, int capacity, Allocator allocator)
        {
            if(maxDepth <= 0)
            {
                Debug.LogError("Max depth must be a value greater than 0");
            } else if(maxDepth >= 10)
            {
                Debug.LogError("Max depth can't be more than 10, as it would not be possible to lay a z-order curve with 32-bits precision");
            }

            this.maxDepth = maxDepth;
            this.axisDivisions = 1 << this.maxDepth;

            this.scale = scale;
            this.bottomLeftDown = position - this.scale * 0.5f;

            this.dataBuckets = new NativeParallelMultiHashMap<uint, T>(capacity, allocator);
            this.octreeNodes = new NativeList<OctreeNode>(capacity, allocator);
            this.free = new NativeList<int>(1, allocator);

            var rootNode = new OctreeNode();
            rootNode.children.Length = 8;
            this.octreeNodes.Add(rootNode);

            this.allocator = allocator;
            this.count = new NativeReference<int>(allocator);
            this.count.Value = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 GetPositionPercentage(float3 position)
        {
            return (position - this.bottomLeftDown) / this.scale;
        }

        private int ChildrenCount(OctreeNode* node)
        {
            int children = 0;
            for (int i = 0; i < 8; i++)
            {
                if (node->children[i] != 0) children++;
            }
            return children;
        }

        private void DeleteSubtree(uint code)
        {
            var node = this.GetRoot();

            int furthestDepthWithMultipleChildren = -1;
            OctreeNode* furthestNodeWithMultipleChildren = null;
            int reachedDepth = 0;
            if (this.ChildrenCount(node) > 1)
            {
                furthestDepthWithMultipleChildren = reachedDepth;
                furthestNodeWithMultipleChildren = node;
            }

            int inverseDepth = this.maxDepth - 1 - reachedDepth;
            uint mask = (uint)(0x00000007 << (inverseDepth * D));
            uint childIndex = (code & mask) >> (inverseDepth * D);

            while (node->children[(int)childIndex] != 0)
            {

                reachedDepth++;
                node = (OctreeNode*)this.octreeNodes.GetUnsafePtr() + node->children[(int)childIndex];
                if (this.ChildrenCount(node) > 1)
                {
                    furthestDepthWithMultipleChildren = reachedDepth;
                    furthestNodeWithMultipleChildren = node;
                }

                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000007 << (inverseDepth * D));
                childIndex = (code & mask) >> (inverseDepth * D);
            }

            reachedDepth = furthestDepthWithMultipleChildren;
            inverseDepth = this.maxDepth - 1 - reachedDepth;

            mask = (uint)(0x00000007 << (inverseDepth * D));
            childIndex = (code & mask) >> (inverseDepth * D);
            node = furthestNodeWithMultipleChildren;

            while (node->children[(int)childIndex] != 0)
            {
                var parent = node;
                int offset = parent->children[(int)childIndex];
                node = (OctreeNode*)this.octreeNodes.GetUnsafePtr() + offset;

                parent->children[(int)childIndex] = 0;
                this.free.Add(offset);

                reachedDepth++;
                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000007 << (inverseDepth * D));
                childIndex = (code & mask) >> (inverseDepth * D);
            }
        }

        private OctreeNode* TraverseAsFarAsPossible(uint path, out int reachedDepth)
        {
            reachedDepth = 0;

            var node = this.GetRoot();

            int inverseDepth = this.maxDepth - 1 - reachedDepth;
            uint mask = (uint)(0x00000007 << (inverseDepth * D));
            uint childIndex = (path & mask) >> (inverseDepth * D);

            while (node->children[(int)childIndex] != 0)
            {
                node = (OctreeNode*)this.octreeNodes.GetUnsafePtr() + node->children[(int)childIndex];
                reachedDepth++;
                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000007 << (inverseDepth * D));
                childIndex = (path & mask) >> (inverseDepth * D);
            }
            return node;
        }

        private OctreeNode* InsertNodesUntilMaxDepth(OctreeNode* node, uint path, int reachedDepth)
        {
            int inverseDepth = this.maxDepth - 1 - reachedDepth;
            uint mask = (uint)(0x00000007 << (inverseDepth * D));
            uint childIndex = (path & mask) >> (inverseDepth * D);

            var newNode = new OctreeNode();
            newNode.children.Length = 8;

            int ptr = -1;
            if (this.free.Length > 0)
            {
                int freeIdx = this.free[this.free.Length - 1];
                this.free.Length--;

                this.octreeNodes[freeIdx] = newNode;
                ptr = freeIdx;
            }
            else
            {
                this.octreeNodes.Add(newNode);
                ptr = this.octreeNodes.Length - 1;
            }

            node->children[(int)childIndex] = ptr;
            node = (OctreeNode*)this.octreeNodes.GetUnsafePtr() + ptr;
            reachedDepth++;

            while(reachedDepth != this.maxDepth)
            {
                inverseDepth = this.maxDepth - 1 - reachedDepth;
                mask = (uint)(0x00000007 << (inverseDepth * D));
                childIndex = (path & mask) >> (inverseDepth * D);

                newNode = new OctreeNode();
                newNode.children.Length = 8;

                if (this.free.Length > 0)
                {
                    int freeIdx = this.free[this.free.Length - 1];
                    this.free.Length--;

                    this.octreeNodes[freeIdx] = newNode;
                    ptr = freeIdx;
                }
                else
                {
                    this.octreeNodes.Add(newNode);
                    ptr = this.octreeNodes.Length - 1;
                }

                node->children[(int)childIndex] = ptr;
                node = (OctreeNode*)this.octreeNodes.GetUnsafePtr() + ptr;
                reachedDepth++;
            }
            return (OctreeNode*)node->children[(int)childIndex];
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
                int3 coord = (int3)(percentage * this.axisDivisions);

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
            this.octreeNodes.Clear();
            this.free.Clear();
            this.count.Value = 0;
        }

        /// <summary>
        /// Important Note: If you have a lot of things moving in the octree, it will be a lot cheaper
        /// performance-wise to recreate the whole tree by calling Clear() and then reinserting each
        /// element.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="oldPosition"></param>
        /// <param name="newPosition"></param>
        /// <returns>Returns false if the value was not found at the old position or the new position is outside the octree</returns>
        public bool Update(T value, float3 oldPosition, float3 newPosition)
        {
            var newPercentage = this.GetPositionPercentage(newPosition);
            var oldPercentage = this.GetPositionPercentage(oldPosition);

            int3 newCoord = (int3)(newPercentage * this.axisDivisions);
            int3 oldCoord = (int3)(oldPercentage * this.axisDivisions);

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
        /// that that does not necessarily mean the value is not present somewhere in the octree, just that it couldn't be found where you would expect it.</returns>
        public bool Remove(float3 position, T value)
        {
            var percentage = this.GetPositionPercentage(position);
            if (math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int3 coord = (int3)(percentage * this.axisDivisions);

                uint code = MathUtilDOTS.PositionToMortonCode(coord);

                if (this.dataBuckets.TryGetFirstValue(code, out T val, out var it))
                {
                    if (val.Equals(value))
                    {
                        this.dataBuckets.Remove(it);
                        this.count.Value = this.count.Value - 1;

                        //If we removed the last element in the bucket, we have to update the octree nodes
                        if (!this.dataBuckets.ContainsKey(code))
                        {
                            this.DeleteSubtree(code);
                        }
                        return true;
                    }

                    while (this.dataBuckets.TryGetNextValue(out val, ref it))
                    {
                        if (val.Equals(value))
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
        public int3 GetCellCoordinates(float3 position)
        {
            var percentage = this.GetPositionPercentage(position);
            return (int3)(percentage * this.axisDivisions);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="position"></param>
        /// <param name="code"></param>
        /// <returns>The morton code (cell id) for the closest cell to the given position.
        /// If the position is outside the bounds of this octree or the cell does not exist,
        /// false is returned</returns>
        public bool GetCell(float3 position, out uint code)
        {
            bool success = false;
            code = uint.MaxValue;
            var percentage = this.GetPositionPercentage(position);
            if(math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int3 coord = (int3)(percentage * this.axisDivisions);

                uint mortonCode = MathUtilDOTS.PositionToMortonCode(coord);

                if(this.dataBuckets.ContainsKey(mortonCode))
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
            public float3 bottomLeftDown;
            public float3 scale;

            public float3 center;
            public float radius;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeList<OctreeNode> octreeNodes;

            [NoAlias, WriteOnly]
            public NativeList<uint> result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(OctreeNode node, Bounds parentBounds, int depth, uint currentCode)
            {
                if(depth == this.maxDepth)
                {

                    if(ShapeOverlap.CuboidSphereOverlap(parentBounds, this.center, this.radiusSq))
                    {
                        this.result.Add(currentCode);
                    }
                } else
                {
                    var sphereBounds = new Bounds(this.center, new Vector3(this.radius, this.radius, this.radius) * 2.0f);

                    var boundsBuffer = new NativeList<Bounds>(8, Allocator.Temp);
                    parentBounds.Subdivide(ref boundsBuffer);


                    for(int i = 0; i < boundsBuffer.Length; i++)
                    {
                        int inverseDepth = this.maxDepth - 1 - depth;
                        var subBounds = boundsBuffer[i];
                        if(subBounds.Intersects(sphereBounds))
                        {
                            int nextNode = node.children[i];
                            if(nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInRadiusRecursion(this.octreeNodes[nextNode], subBounds, depth + 1, nextCode);
                            }
                        }
                    }
                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.radiusSq = this.radius * this.radius;

                float3 min = this.bottomLeftDown;
                float3 max = this.bottomLeftDown + this.scale;

                var bounds = new Bounds((min + max) * 0.5f, max - min);

                this.GetCellsInRadiusRecursion(this.octreeNodes[0], bounds, 0, 0);
            }
        }

        [BurstCompile]
        public struct GetCellsInRadiiJob : IJobParallelFor
        {
            public float3 bottomLeftDown;
            public float3 scale;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeArray<float3> centers;
            [NoAlias, ReadOnly]
            public NativeArray<float> radii;

            [NoAlias, ReadOnly]
            public NativeList<OctreeNode> octreeNodes;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(int index, OctreeNode node, Bounds parentBounds, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {

                    if (ShapeOverlap.CuboidSphereOverlap(parentBounds, this.centers[index], this.radiusSq))
                    {
                        this.result.Add(currentCode);
                    }
                }
                else
                {
                    float radius = this.radii[index];
                    var sphereBounds = new Bounds(this.centers[index], new Vector3(radius, radius, radius) * 2.0f);

                    var boundsBuffer = new NativeList<Bounds>(8, Allocator.Temp);
                    parentBounds.Subdivide(ref boundsBuffer);


                    for (int i = 0; i < boundsBuffer.Length; i++)
                    {
                        int inverseDepth = this.maxDepth - 1 - depth;
                        var subBounds = boundsBuffer[i];
                        if (subBounds.Intersects(sphereBounds))
                        {
                            int nextNode = node.children[i];
                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInRadiusRecursion(index, this.octreeNodes[nextNode], subBounds, depth + 1, nextCode);
                            }
                        }
                    }
                }
            }

            public void Execute(int index)
            {
                float3 min = this.bottomLeftDown;
                float3 max = this.bottomLeftDown + this.scale;

                this.radiusSq = this.radii[index] * this.radii[index];

                var bounds = new Bounds((min + max) * 0.5f, max - min);

                this.GetCellsInRadiusRecursion(index, this.octreeNodes[0], bounds, 0, 0);
            }
        }

        [BurstCompile]
        public struct GetCellsInBoundsJob : IJob
        {
            public Bounds queryBounds;


            public float3 bottomLeftDown;
            public float3 scale;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeList<OctreeNode> octreeNodes;

            [NoAlias, WriteOnly]
            public NativeList<uint> result;

            private void GetCellsInBoundsRecursion(OctreeNode node, float3 min, float3 max, int depth, uint currentCode)
            {
                if(depth == this.maxDepth)
                {
                    this.result.Add(currentCode);
                } else
                {
                    var parentBounds = new Bounds((min + max) * 0.5f, max - min);

                    var boundsBuffer = new NativeList<Bounds>(8, Allocator.Temp);
                    parentBounds.Subdivide(ref boundsBuffer);

                    for (int i = 0; i < boundsBuffer.Length; i++)
                    {
                        int inverseDepth = this.maxDepth - 1 - depth;
                        var subBounds = boundsBuffer[i];
                        if (subBounds.Intersects(this.queryBounds))
                        {
                            int nextNode = node.children[i];
                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInBoundsRecursion(this.octreeNodes[nextNode], subBounds.min, subBounds.max, depth + 1, nextCode);
                            }
                        }
                    }
                }
            }

            public void Execute()
            {
                this.result.Clear();

                float3 min = this.bottomLeftDown;
                float3 max = this.bottomLeftDown + this.scale;

                this.GetCellsInBoundsRecursion(this.octreeNodes[0], min, max, 0, 0);
            }
        }

        [BurstCompile]
        public struct GetCellsInMultipleBoundsJob : IJobParallelFor
        {
            public float3 bottomLeftDown;
            public float3 scale;

            public int maxDepth;

            [NoAlias, ReadOnly]
            public NativeArray<Bounds> queryBounds;

            [NoAlias, ReadOnly]
            public NativeList<OctreeNode> octreeNodes;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private void GetCellsInBoundsRecursion(int index, OctreeNode node, float3 min, float3 max, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    this.result.Add(currentCode);
                }
                else
                {
                    var parentBounds = new Bounds((min + max) * 0.5f, max - min);

                    var boundsBuffer = new NativeList<Bounds>(8, Allocator.Temp);
                    parentBounds.Subdivide(ref boundsBuffer);

                    for (int i = 0; i < boundsBuffer.Length; i++)
                    {
                        int inverseDepth = this.maxDepth - 1 - depth;
                        var subBounds = boundsBuffer[i];
                        if (subBounds.Intersects(this.queryBounds[index]))
                        {
                            int nextNode = node.children[i];
                            if (nextNode != 0)
                            {
                                uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                                this.GetCellsInBoundsRecursion(index, this.octreeNodes[nextNode], subBounds.min, subBounds.max, depth + 1, nextCode);
                            }
                        }
                    }
                }
            }

            public void Execute(int index)
            {
                float3 min = this.bottomLeftDown;
                float3 max = this.bottomLeftDown + this.scale;

                this.GetCellsInBoundsRecursion(index, this.octreeNodes[0], min, max, 0, 0);
            }
        }

        /// <summary>
        /// Searches for all cells within a given sphere (center + radius) and writes
        /// the morton code (cell ids) to a result-list
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle GetCellsInRadius(float3 center, float radius, ref NativeList<uint> result, JobHandle dependsOn = default)
        {
            var job = new GetCellsInRadiusJob()
            {
                bottomLeftDown = this.bottomLeftDown,
                center = center,
                maxDepth = this.maxDepth,
                octreeNodes = this.octreeNodes,
                radius = radius,
                result = result,
                scale = this.scale
            };
            
            return job.Schedule(dependsOn);
        }

        /// <summary>
        /// Searches for all cells within multiple spheres (center + radius) and writes
        /// the morton code (cell ids) to a result-list
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle GetCellsInRadii(NativeArray<float3> centers, NativeArray<float> radii,
            ref NativeParallelHashSet<uint> result, JobHandle dependsOn = default, int innerLoopBatchCount = 1)
        {
            if (centers.Length != radii.Length)
            {
                Debug.LogError("Centers and Radii arrays do not match in size!");
            }

            result.Clear();

            var job = new GetCellsInRadiiJob()
            {
                bottomLeftDown = this.bottomLeftDown,
                centers = centers,
                maxDepth = this.maxDepth,
                octreeNodes = this.octreeNodes,
                radii = radii,
                result = result.AsParallelWriter(),
                scale = this.scale
            };

            return job.Schedule(centers.Length, innerLoopBatchCount, dependsOn);
        }


        /// <summary>
        /// Searches for all cells within the given bounds and writes
        /// the morton code (cell ids) to a result-list
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle GetCellsInBounds(Bounds bounds, ref NativeList<uint> result, JobHandle dependsOn = default)
        {
            var job = new GetCellsInBoundsJob()
            {
                bottomLeftDown = this.bottomLeftDown,
                maxDepth = this.maxDepth,
                octreeNodes = this.octreeNodes,
                queryBounds = bounds,
                result = result,
                scale = this.scale
            };

            return job.Schedule(dependsOn);
        }

        /// <summary>
        /// Searches for all cells within all given bounds and writes
        /// the morton code (cell ids) to a result-list
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle GetCellsInBounds(NativeArray<Bounds> bounds, ref NativeParallelHashSet<uint> result,
            JobHandle dependsOn = default, int innerLoopBatchCount = 1) {

            result.Clear();

            var job = new GetCellsInMultipleBoundsJob()
            {
                bottomLeftDown = this.bottomLeftDown,
                maxDepth = this.maxDepth,
                octreeNodes = this.octreeNodes,
                queryBounds = bounds,
                result = result.AsParallelWriter(),
                scale = this.scale
            };

            return job.Schedule(bounds.Length, innerLoopBatchCount, dependsOn);
        }


        public bool IsCreated => this.dataBuckets.IsCreated || this.octreeNodes.IsCreated || this.free.IsCreated || this.count.IsCreated;

        public void Dispose()
        {
            if(this.dataBuckets.IsCreated)
            {
                this.dataBuckets.Dispose();
            }

            this.octreeNodes.DisposeIfCreated();
            this.free.DisposeIfCreated();
            this.count.DisposeIfCreated();
        }

        public void DisposeIfCreated()
        {
            if (this.IsCreated) this.Dispose();
        }

    }
}
