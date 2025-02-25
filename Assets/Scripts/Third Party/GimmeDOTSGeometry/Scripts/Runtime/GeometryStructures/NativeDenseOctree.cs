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
    public unsafe struct NativeDenseOctree<T> : IOctree<T>, IDisposable where T : unmanaged, IEquatable<T>
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



        private void AddAllNodesRecursively(OctreeNode* parent, int depth)
        {
            if (depth >= this.maxDepth) return;

            for(int i = 0; i < 8; i++)
            {
                var childNode = new OctreeNode();
                childNode.children.Length = 8;

                this.octreeNodes.Add(childNode);
                int ptr = this.octreeNodes.Length - 1;

                parent->children[i] = ptr;

                this.AddAllNodesRecursively((OctreeNode*)this.octreeNodes.GetUnsafePtr() + ptr, depth + 1);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bounds">Bounds of the octree</param>
        /// <param name="maxDepth">Maximum depth / height of the octree - also determines the number of cells</param>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        public NativeDenseOctree(Bounds bounds, int maxDepth, int capacity, Allocator allocator)
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
        public NativeDenseOctree(float3 position, float3 scale, int maxDepth, int capacity, Allocator allocator)
        {
            if (maxDepth <= 0)
            {
                Debug.LogError("Max depth must be a value greater than 0");
            }
            else if (maxDepth >= 10)
            {
                Debug.LogError("Max depth can't be more than 10, as it would not be possible to lay a z-order curve with 32-bits precision");
            }

            if(maxDepth >= 8)
            {
                Debug.LogWarning("Unity's Job System can not allocate native arrays greater than 2GB (overflow)... octree won't be able to allocate enough memory");
            }

            this.maxDepth = maxDepth;
            this.axisDivisions = 1 << this.maxDepth;

            this.scale = scale;
            this.bottomLeftDown = position - this.scale * 0.5f;

            this.dataBuckets = new NativeParallelMultiHashMap<uint, T>(capacity, allocator);

            //Sum of [x³ / 8 ^ i] from i = 0 to ld x
            int completeNodeCount = (8 * this.axisDivisions * this.axisDivisions * this.axisDivisions - 1) / 7;

            this.octreeNodes = new NativeList<OctreeNode>(completeNodeCount, allocator);

            var rootNode = new OctreeNode();
            rootNode.children.Length = 8;
            this.octreeNodes.Add(rootNode);

            this.allocator = allocator;
            this.count = new NativeReference<int>(allocator);
            this.count.Value = 0;

            this.AddAllNodesRecursively((OctreeNode*)this.octreeNodes.GetUnsafePtr(), 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 GetPositionPercentage(float3 position)
        {
            return (position - this.bottomLeftDown) / this.scale;
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
                int3 coord = (int3)(percentage * this.axisDivisions);

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
        /// <returns>Returns false if the value was not found at the old position or the new position is outside the octree</returns>
        public bool Update(T value, float3 oldPosition, float3 newPosition)
        {
            var newPercentage = this.GetPositionPercentage(newPosition);
            var oldPercentage = this.GetPositionPercentage(oldPosition);

            int3 newCoord = (int3)(newPercentage * this.axisDivisions);
            int3 oldCoord = (int3)(oldPercentage * this.axisDivisions);

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

        public bool Remove(float3 position, T value)
        {
            var percentage = this.GetPositionPercentage(position);
            if(math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int3 coord = (int3)(percentage * this.axisDivisions);

                uint code = MathUtilDOTS.PositionToMortonCode(coord);

                if(this.dataBuckets.TryGetFirstValue(code, out T val, out var it))
                {
                    if(val.Equals(value))
                    {
                        this.dataBuckets.Remove(it);
                        this.count.Value = this.count.Value - 1;

                        return true;
                    }

                    while(this.dataBuckets.TryGetNextValue(out val, ref it))
                    {
                        if(val.Equals(value))
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
            if (math.all(percentage >= 0.0f) && math.all(percentage <= 1.0f))
            {
                int3 coord = (int3)(percentage * this.axisDivisions);

                code = MathUtilDOTS.PositionToMortonCode(coord);

                success = true;
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

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeList<uint> result;


            private float radiusSq;

            private void GetCellsInRadiusRecursion(OctreeNode node, Bounds parentBounds, int depth, uint currentCode)
            {
                if(depth == this.maxDepth)
                {
                    if (ShapeOverlap.CuboidSphereOverlap(parentBounds, this.center, this.radiusSq)
                        //Only add buckets that contain some data
                        && this.dataBuckets.ContainsKey(currentCode))
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
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInRadiusRecursion(this.octreeNodes[nextNode], subBounds, depth + 1, nextCode);
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
            public NativeArray<float> radii;

            [NoAlias, ReadOnly]
            public NativeArray<float3> centers;

            [NoAlias, ReadOnly]
            public NativeList<OctreeNode> octreeNodes;

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;

            private float radiusSq;

            private void GetCellsInRadiusRecursion(int index, OctreeNode node, Bounds parentBounds, int depth, uint currentCode)
            {
                if (depth == this.maxDepth)
                {
                    if (ShapeOverlap.CuboidSphereOverlap(parentBounds, this.centers[index], this.radiusSq)
                        //Only add buckets that contain some data
                        && this.dataBuckets.ContainsKey(currentCode))
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
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInRadiusRecursion(index, this.octreeNodes[nextNode], subBounds, depth + 1, nextCode);
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

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeList<uint> result;


            private void GetCellsInBoundsRecursion(OctreeNode node, float3 min, float3 max, int depth, uint currentCode)
            {
                if(depth == this.maxDepth)
                {
                    if (this.dataBuckets.ContainsKey(currentCode))
                    {
                        this.result.Add(currentCode);
                    }
                } else
                {
                    var parentBounds = new Bounds((min + max) * 0.5f, max - min);

                    var boundsBuffer = new NativeList<Bounds>(8, Allocator.Temp);
                    parentBounds.Subdivide(ref boundsBuffer);

                    for(int i = 0; i < boundsBuffer.Length; i++)
                    {
                        int inverseDepth = this.maxDepth - 1 - depth;
                        var subBounds = boundsBuffer[i];
                        if(subBounds.Intersects(this.queryBounds))
                        {
                            int nextNode = node.children[i];
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInBoundsRecursion(this.octreeNodes[nextNode], subBounds.min, subBounds.max, depth + 1, nextCode);
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

            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, T> dataBuckets;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<uint>.ParallelWriter result;


            private void GetCellsInBoundsRecursion(int index, OctreeNode node, float3 min, float3 max, int depth, uint currentCode)
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
                            uint nextCode = (currentCode | (uint)(i << (inverseDepth * D)));
                            this.GetCellsInBoundsRecursion(index, this.octreeNodes[nextNode], subBounds.min, subBounds.max, depth + 1, nextCode);
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
                dataBuckets = this.dataBuckets,
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
            if(centers.Length != radii.Length)
            {
                Debug.LogError("Centers and Radii arrays do not match in size!");
            }

            result.Clear();

            var job = new GetCellsInRadiiJob()
            {
                bottomLeftDown = this.bottomLeftDown,
                centers = centers,
                dataBuckets = this.dataBuckets,
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
                dataBuckets = this.dataBuckets,
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
            JobHandle dependsOn = default, int innerLoopBatchCount = 1)
        {
            result.Clear();

            var job = new GetCellsInMultipleBoundsJob()
            {
                bottomLeftDown = this.bottomLeftDown,
                dataBuckets = this.dataBuckets,
                maxDepth = this.maxDepth,
                octreeNodes = this.octreeNodes,
                queryBounds = bounds,
                result = result.AsParallelWriter(),
                scale = this.scale
            };

            return job.Schedule(bounds.Length, innerLoopBatchCount, dependsOn);
        }


        public bool IsCreated => this.dataBuckets.IsCreated || this.octreeNodes.IsCreated || this.count.IsCreated;

        public void Dispose()
        {
            if(this.dataBuckets.IsCreated)
            {
                this.dataBuckets.Dispose();
            }
            this.octreeNodes.DisposeIfCreated();
            this.count.DisposeIfCreated();
        }

        public void DisposeIfCreated()
        {
            if (this.IsCreated) this.Dispose();
        }
    }
}
