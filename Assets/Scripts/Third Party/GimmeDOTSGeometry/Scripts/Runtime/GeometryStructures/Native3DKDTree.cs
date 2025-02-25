using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Note: Default sorting order is X, then Y, then Z.
    /// </summary>
    public unsafe struct Native3DKDTree<T> : IDisposable where T : unmanaged, IPosition3D, IEquatable<T>
    {

        #region Private Variables

        private NativeArray<T> nodes;

        private Bounds bounds;

        #endregion

        //Root is at the end of the nodes upon construction
        public T* GetRoot()
        {
            if (this.nodes.IsCreated)
            {
                return (T*)this.nodes.GetUnsafePtr();
            }
            return null;
        }

        public int Count => this.nodes.Length;


        public Bounds GetBounds() => this.bounds;

        public NativeArray<T> GetNodes() => this.nodes;

        [BurstCompile]
        private struct KDTreeConstructionJob : IJob
        {
            public NativeList<T> nodeCopy;

            public NativeArray<T> nodes;

            private void ConstructionRecursion(int index, int left, int right, int depth)
            {
                int length = right - left;
                if (length <= 0) return;

                if (length == 1)
                {
                    this.nodes[index] = this.nodeCopy[left];
                }
                else
                {
                    int treeHeight = Mathf.CeilToInt(Mathf.Log(length + 1, 2.0f));
                    int max = 1 << treeHeight;
                    int min = 1 << (treeHeight - 1);
                    int half = (max + min) / 2;

                    int medianIdx;
                    if (length < half)
                    {
                        int diff = length - min;
                        medianIdx = (min / 2) + diff;
                    }
                    else
                    {
                        medianIdx = (max / 2) - 1;
                    }
                    medianIdx += left;

                    Composite3DComparer comparer = default;

                    switch (depth % 3)
                    {
                        case 0:
                            comparer = new Composite3DComparer() { axis0 = 0, axis1 = 1, axis2 = 2 };
                            break;
                        case 1:
                            comparer = new Composite3DComparer() { axis0 = 1, axis1 = 2, axis2 = 0 };
                            break;
                        case 2:
                            comparer = new Composite3DComparer() { axis0 = 2, axis1 = 0, axis2 = 1 };
                            break;
                    }

                    NativeSelection.QuickSelect(ref this.nodeCopy, comparer, medianIdx - left, left, right);
                    this.nodes[index] = this.nodeCopy[medianIdx];

                    this.ConstructionRecursion(index * 2 + 1, left, medianIdx, depth + 1);
                    this.ConstructionRecursion(index * 2 + 2, medianIdx + 1, right, depth + 1);
                }
            }

            public void Execute()
            {
                this.ConstructionRecursion(0, 0, this.nodeCopy.Length, 0);
            }
        }

        private struct Composite3DComparer : IComparer<T>
        {

            public int axis0;
            public int axis1;
            public int axis2;

            public int Compare(T a, T b)
            {
                int comp = a.Position[this.axis0].CompareTo(b.Position[this.axis0]);
                if (comp != 0) return comp;
                comp = a.Position[this.axis1].CompareTo(b.Position[this.axis1]);
                if (comp != 0) return comp;
                return a.Position[this.axis2].CompareTo(b.Position[this.axis2]);
            }
        }

        private void ConstructKDTree(NativeArray<T> positions)
        {
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;

            for(int i = 0; i < positions.Length; i++)
            {
                var pos = positions[i].Position;

                min = Vector3.Min(pos, min);
                max = Vector3.Max(pos, max);
            }

            this.bounds.SetMinMax(min, max);

            var listCopy = new NativeList<T>(positions.Length, Allocator.TempJob);

            listCopy.CopyFrom(positions);
            var constructKDTreeJob = new KDTreeConstructionJob()
            {
                nodeCopy = listCopy,
                nodes = this.nodes
            };

            constructKDTreeJob.Schedule().Complete();

            listCopy.Dispose();
        }

        private void Construct(NativeArray<T> positions, Allocator allocator)
        {
            if (positions == null || positions.Length == 0)
            {
                Debug.LogError("Tried to construct native 3D KD tree with an empty array!");
            }

            this.nodes = new NativeArray<T>(positions.Length, allocator);

            this.bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);

            this.ConstructKDTree(positions);
        }

        private Native3DKDTree(Allocator allocator)
        {
            this.nodes = default;
            this.bounds = new Bounds();
        }

        public Native3DKDTree(T[] positions, Allocator allocator) : this(allocator)
        {
            var nativePositions = new NativeArray<T>(positions.Length, Allocator.TempJob);
            nativePositions.CopyFrom(positions);

            this.Construct(nativePositions, allocator);

            nativePositions.Dispose();
        }

        public Native3DKDTree(List<T> positions, Allocator allocator) : this(positions.ToArray(), allocator) { }

        public Native3DKDTree(NativeArray<T> positions, Allocator allocator) : this(allocator)
        {
            this.Construct(positions, allocator);
        }

        [BurstCompile]
        public struct GetPointsInBoundsJob : IJob
        {
            public Bounds searchBounds;
            public Bounds kdTreeBounds;

            [NoAlias]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeList<T> result;


            private void AddSubtree(int nodeIdx)
            {
                this.result.Add(this.nodes[nodeIdx]);

                int left = nodeIdx * 2 + 1;
                int right = math.clamp(nodeIdx * 2 + 2, 0, this.nodes.Length - 1);

                var ptr = (T*)this.nodes.GetUnsafePtr();

                while (left < this.nodes.Length)
                {
                    this.result.AddRange(ptr + left, right - left + 1);

                    left = left * 2 + 1;
                    right = math.clamp(right * 2 + 2, 0, this.nodes.Length - 1);
                }
            }

            private void SearchKDTreeRecursion(int currentNodeIdx, float3 min, float3 max, int depth)
            {
                var position = this.nodes[currentNodeIdx];

                if(this.searchBounds.Contains(position.Position))
                {
                    this.result.Add(position);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    int axis = depth % 3;

                    float splitPlane = position.Position[axis];

                    float3 minLeft = min;
                    float3 maxLeft = max;
                    float3 minRight = min;
                    float3 maxRight = max;

                    maxLeft[axis] = splitPlane;
                    minRight[axis] = splitPlane;

                    var boundsLeft = new Bounds();
                    var boundsRight = new Bounds();

                    boundsLeft.SetMinMax(minLeft, maxLeft);
                    boundsRight.SetMinMax(minRight, maxRight);

                    if(this.searchBounds.Contains(boundsLeft))
                    {
                        this.AddSubtree(left);
                    }
                    else if(this.searchBounds.Intersects(boundsLeft))
                    {
                        this.SearchKDTreeRecursion(left, minLeft, maxLeft, depth + 1);
                    }

                    if(right < this.nodes.Length)
                    {
                        if(this.searchBounds.Contains(boundsRight))
                        {
                            this.AddSubtree(right);
                        }
                        else if(this.searchBounds.Intersects(boundsRight))
                        {
                            this.SearchKDTreeRecursion(right, minRight, maxRight, depth + 1);
                        }
                    }
                }
            }

            public void Execute()
            {
                float3 min = this.kdTreeBounds.min;
                float3 max = this.kdTreeBounds.max;

                this.SearchKDTreeRecursion(0, min, max, 0);
            }
        }

        //TODO: Replace methods with IKDTreePointsJob-Interface default implementation in 2-3 years
        [BurstCompile]
        public struct GetPointsInMultipleBoundsJob : IJobParallelFor
        {
            public Bounds kdTreeBounds;

            [NoAlias, ReadOnly]
            public NativeArray<Bounds> searchBounds;

            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Bounds bounds;

            private void AddSubtree(int nodeIdx)
            {
                this.result.Add(this.nodes[nodeIdx]);

                int left = nodeIdx * 2 + 1;
                int right = math.clamp(nodeIdx * 2 + 2, 0, this.nodes.Length - 1);

                while (left < this.nodes.Length)
                {
                    for (int i = left; i <= right; i++)
                    {
                        this.result.Add(this.nodes[i]);
                    }

                    left = left * 2 + 1;
                    right = math.clamp(right * 2 + 2, 0, this.nodes.Length - 1);
                }
            }

            private void SearchKDTreeRecursion(int currentNodeIdx, float3 min, float3 max, int depth)
            {
                var position = this.nodes[currentNodeIdx];

                if (this.bounds.Contains(position.Position))
                {
                    this.result.Add(position);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    int axis = depth % 3;

                    float splitPlane = position.Position[axis];

                    float3 minLeft = min;
                    float3 maxLeft = max;
                    float3 minRight = min;
                    float3 maxRight = max;

                    maxLeft[axis] = splitPlane;
                    minRight[axis] = splitPlane;

                    var boundsLeft = new Bounds();
                    var boundsRight = new Bounds();

                    boundsLeft.SetMinMax(minLeft, maxLeft);
                    boundsRight.SetMinMax(minRight, maxRight);

                    if (this.searchBounds.Contains(boundsLeft))
                    {
                        this.AddSubtree(left);
                    }
                    else if (this.bounds.Intersects(boundsLeft))
                    {
                        this.SearchKDTreeRecursion(left, minLeft, maxLeft, depth + 1);
                    }

                    if (right < this.nodes.Length)
                    {
                        if (this.searchBounds.Contains(boundsRight))
                        {
                            this.AddSubtree(right);
                        }
                        else if (this.bounds.Intersects(boundsRight))
                        {
                            this.SearchKDTreeRecursion(right, minRight, maxRight, depth + 1);
                        }
                    }
                }
            }

            public void Execute(int index)
            {
                float3 min = this.kdTreeBounds.min;
                float3 max = this.kdTreeBounds.max;

                this.bounds = this.searchBounds[index];

                this.SearchKDTreeRecursion(0, min, max, 0);
            }
        }

        [BurstCompile]
        public struct GetPointsInRadiusJob : IJob
        {
            public float radius;

            public float3 position;

            public Bounds kdTreeBounds;

            [NoAlias]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeList<T> result;


            private float radiusSquared;

            private void AddSubtree(int nodeIdx)
            {
                this.result.Add(this.nodes[nodeIdx]);

                int left = nodeIdx * 2 + 1;
                int right = math.clamp(nodeIdx * 2 + 2, 0, this.nodes.Length - 1);

                var ptr = (T*)this.nodes.GetUnsafePtr();

                while (left < this.nodes.Length)
                {
                    this.result.AddRange(ptr + left, right - left + 1);

                    left = left * 2 + 1;
                    right = math.clamp(right * 2 + 2, 0, this.nodes.Length - 1);
                }
            }

            private void SearchKDTreeRecursion(int currentNodeIdx, float3 min, float3 max, int depth)
            {
                var position = this.nodes[currentNodeIdx];

                if (math.distancesq(this.position, position.Position) <= this.radiusSquared)
                {
                    this.result.Add(position);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    int axis = depth % 3;

                    float splitPlane = position.Position[axis];

                    float3 maxLeft = max;
                    float3 minRight = min;

                    maxLeft[axis] = splitPlane;
                    minRight[axis] = splitPlane;

                    if (ShapeOverlap.SphereContainsCuboid(this.position, this.radiusSquared, min, maxLeft))
                    {
                        this.AddSubtree(left);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(min, maxLeft, this.position, this.radiusSquared))
                    {
                        this.SearchKDTreeRecursion(left, min, maxLeft, depth + 1);
                    }

                    if (right < this.nodes.Length)
                    {
                        if (ShapeOverlap.SphereContainsCuboid(this.position, this.radiusSquared, minRight, max))
                        {
                            this.AddSubtree(right);
                        }
                        else if (ShapeOverlap.CuboidSphereOverlap(minRight, max, this.position, this.radiusSquared))
                        {
                            this.SearchKDTreeRecursion(right, minRight, max, depth + 1);
                        }
                    }
                }
            }

            public void Execute()
            {
                this.radiusSquared = this.radius * this.radius;
                

                float3 min = this.kdTreeBounds.min;
                float3 max = this.kdTreeBounds.max;

                this.SearchKDTreeRecursion(0, min, max, 0);
            }
        }

        //TODO: Replace methods with IKDTreePointsJob-Interface default implementation in 2-3 years
        [BurstCompile]
        public struct GetPointsInRadiiJob : IJobParallelFor
        {

            public Bounds kdTreeBounds;

            [NoAlias, ReadOnly]
            public NativeArray<float> radii;

            [NoAlias, ReadOnly]
            public NativeArray<float3> positions;


            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private float radiusSquared;

            private float3 circlePos;

            private void AddSubtree(int nodeIdx)
            {
                this.result.Add(this.nodes[nodeIdx]);

                int left = nodeIdx * 2 + 1;
                int right = math.clamp(nodeIdx * 2 + 2, 0, this.nodes.Length - 1);

                while (left < this.nodes.Length)
                {
                    for (int i = left; i <= right; i++)
                    {
                        this.result.Add(this.nodes[i]);
                    }

                    left = left * 2 + 1;
                    right = math.clamp(right * 2 + 2, 0, this.nodes.Length - 1);
                }
            }

            private void SearchKDTreeRecursion(int currentNodeIdx, float3 min, float3 max, int depth)
            {
                var position = this.nodes[currentNodeIdx];

                if (math.distancesq(this.circlePos, position.Position) <= this.radiusSquared)
                {
                    this.result.Add(position);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    int axis = depth % 3;

                    float splitPlane = position.Position[axis];

                    float3 maxLeft = max;
                    float3 minRight = min;

                    maxLeft[axis] = splitPlane;
                    minRight[axis] = splitPlane;

                    if (ShapeOverlap.SphereContainsCuboid(this.circlePos, this.radiusSquared, min, maxLeft))
                    {
                        this.AddSubtree(left);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(min, maxLeft, this.circlePos, this.radiusSquared))
                    {
                        this.SearchKDTreeRecursion(left, min, maxLeft, depth + 1);
                    }
                    
                    if (right < this.nodes.Length)
                    {
                        if (ShapeOverlap.SphereContainsCuboid(this.circlePos, this.radiusSquared, minRight, max))
                        {
                            this.AddSubtree(right);
                        }
                        else if (ShapeOverlap.CuboidSphereOverlap(minRight, max, this.circlePos, this.radiusSquared))
                        {
                            this.SearchKDTreeRecursion(right, minRight, max, depth + 1);
                        }
                    }
                }
            }

            public void Execute(int index)
            {
                this.radiusSquared = this.radii[index] * this.radii[index];

                float3 min = this.kdTreeBounds.min;
                float3 max = this.kdTreeBounds.max;

                this.circlePos = this.positions[index];

                this.SearchKDTreeRecursion(0, min, max, 0);
            }
        }


        [BurstCompile]
        public struct GetNearestNeighborJob : IJob
        {

            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, ReadOnly]
            public NativeArray<float3> queryPoints;

            [NoAlias, WriteOnly]
            public NativeArray<T> result;


            private int GetClosest(int currentNodeIdx, float3 searchPos, int depth)
            {
                var position = this.nodes[currentNodeIdx];

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                bool leftValid = left < this.nodes.Length;
                bool rightValid = right < this.nodes.Length;

                if (leftValid || rightValid)
                {
                    int axis = depth % 3;

                    float splitPlane = position.Position[axis];

                    if (!rightValid || searchPos[axis] < splitPlane)
                    {
                        int leftClosest = this.GetClosest(left, searchPos, depth + 1);
                        float3 leftPos = this.nodes[leftClosest].Position;

                        float dist = math.distance(searchPos, position.Position);
                        float bestDist = math.distance(searchPos, leftPos);

                        if (dist < bestDist)
                        {
                            leftClosest = currentNodeIdx;
                            bestDist = dist;
                        }

                        if (rightValid && searchPos[axis] + bestDist > splitPlane)
                        {
                            int rightClosest = this.GetClosest(right, searchPos, depth + 1);
                            float3 rightPos = this.nodes[rightClosest].Position;

                            float rightDist = math.distance(searchPos, rightPos);
                            if (rightDist < bestDist)
                            {
                                leftClosest = rightClosest;
                            }
                        }

                        return leftClosest;

                    }
                    else
                    {
                        int rightClosest = this.GetClosest(right, searchPos, depth + 1);
                        float3 rightPos = this.nodes[rightClosest].Position;

                        float dist = math.distance(searchPos, position.Position);
                        float bestDist = math.distance(searchPos, rightPos);

                        if (dist < bestDist)
                        {
                            rightClosest = currentNodeIdx;
                            bestDist = dist;
                        }

                        if (leftValid && searchPos[axis] - bestDist < splitPlane)
                        {
                            int leftClosest = this.GetClosest(left, searchPos, depth + 1);
                            float3 leftPos = this.nodes[leftClosest].Position;

                            float leftDist = math.distance(searchPos, leftPos);
                            if (leftDist < bestDist)
                            {
                                rightClosest = leftClosest;
                            }
                        }
                        return rightClosest;
                    }

                }
                else
                {
                    return currentNodeIdx;
                }
            }


            public void Execute()
            {

                for (int i = 0; i < this.queryPoints.Length; i++)
                {
                    var searchPos = this.queryPoints[i];
                    int closest = this.GetClosest(0, searchPos, 0);
                    this.result[i] = this.nodes[closest];
                }
            }

        }

        public JobHandle GetNearestNeighbors(NativeArray<float3> queryPoints, ref NativeArray<T> nearestNeighbors, JobHandle dependsOn = default)
        {
            var nearestNeighborJob = new GetNearestNeighborJob()
            {
                nodes = this.nodes,
                queryPoints = queryPoints,
                result = nearestNeighbors,
            };
            return nearestNeighborJob.Schedule(dependsOn);
        }

        public JobHandle GetPointsInBounds(Bounds bounds, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var pointsInBoundsJob = new GetPointsInBoundsJob()
            {
                kdTreeBounds = this.bounds,
                nodes = this.nodes,
                result = result,
                searchBounds = bounds,
            };

            return pointsInBoundsJob.Schedule(dependsOn);
        }

        public JobHandle GetPointsInBounds(NativeArray<Bounds> bounds, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            var pointsInBoundsJob = new GetPointsInMultipleBoundsJob()
            {
                kdTreeBounds = this.bounds,
                nodes = this.nodes,
                result = result.AsParallelWriter(),
                searchBounds = bounds,
            };

            return pointsInBoundsJob.Schedule(bounds.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetPointsInRadius(float3 center, float radius, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var pointsInRadiusJob = new GetPointsInRadiusJob()
            {
                kdTreeBounds = this.bounds,
                nodes = this.nodes,
                position = center,
                radius = radius,
                result = result,
            };

            return pointsInRadiusJob.Schedule(dependsOn);
        }

        public JobHandle GetPointsInRadii(NativeArray<float3> centers, NativeArray<float> radii, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            if (centers.Length != radii.Length)
            {
                Debug.LogError("Centers and Radii arrays do not match in size!");
            }

            var pointsInRadiiJob = new GetPointsInRadiiJob()
            {
                kdTreeBounds = this.bounds,
                nodes = this.nodes,
                positions = centers,
                radii = radii,
                result = result.AsParallelWriter(),
            };

            return pointsInRadiiJob.Schedule(centers.Length, innerBatchLoopCount, dependsOn);
        }

        public bool IsCreated => this.nodes.IsCreated;

        public void Dispose()
        {
            this.nodes.DisposeIfCreated();
        }

        public void DisposeIfCreated()
        {
            if (this.IsCreated) this.Dispose();
        }

    }
}
