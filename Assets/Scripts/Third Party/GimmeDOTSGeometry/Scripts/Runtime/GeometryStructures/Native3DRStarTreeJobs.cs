using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe partial struct Native3DRStarTree<T> : IDisposable
        where T : unmanaged, IBoundingBox, IIdentifiable, IEquatable<T>
    {

        [BurstCompile]
        public struct GetBoundsInBoundsJob : IJob
        {

            public Bounds searchBounds;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;


            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (this.searchBounds.Contains(leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (this.searchBounds.Overlaps(leftNode.Bounds))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (this.searchBounds.Contains(rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (this.searchBounds.Overlaps(rightNode.Bounds))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (this.searchBounds.Contains(child.Bounds))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }


        [BurstCompile]
        public struct GetOverlappingBoundsInBoundsJob : IJob
        {

            public Bounds searchBounds;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;


            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (this.searchBounds.Contains(leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (this.searchBounds.Overlaps(leftNode.Bounds))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (this.searchBounds.Contains(rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (this.searchBounds.Overlaps(rightNode.Bounds))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (this.searchBounds.Overlaps(child.Bounds))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetBoundsInMultipleBoundsJob : IJobParallelFor
        {
            [NoAlias, ReadOnly]
            public NativeArray<Bounds> searchBoundaries;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Bounds searchBounds;

            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }


            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (this.searchBounds.Contains(leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (this.searchBounds.Overlaps(leftNode.Bounds))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (this.searchBounds.Contains(rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (this.searchBounds.Overlaps(rightNode.Bounds))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (this.searchBounds.Contains(child.Bounds))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                this.searchBounds = this.searchBoundaries[index];
                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }



        [BurstCompile]
        public struct GetOverlappingBoundsInMultipleBoundsJob : IJobParallelFor
        {
            [NoAlias, ReadOnly]
            public NativeArray<Bounds> searchBoundaries;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Bounds searchBounds;

            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }


            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (this.searchBounds.Contains(leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (this.searchBounds.Overlaps(leftNode.Bounds))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (this.searchBounds.Contains(rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (this.searchBounds.Overlaps(rightNode.Bounds))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (this.searchBounds.Overlaps(child.Bounds))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                this.searchBounds = this.searchBoundaries[index];
                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }


        [BurstCompile]
        public struct GetBoundsInRadiusJob : IJob
        {

            public float radius;
            public float3 center;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private float radiusSq;

            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(leftNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(rightNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, child.Bounds))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.radiusSq = this.radius * this.radius;

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetBoundsInRadiiJob : IJobParallelFor
        {
            [ReadOnly, NoAlias]
            public NativeArray<float> radii;

            [ReadOnly, NoAlias]
            public NativeArray<float3> centers;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;


            private float radiusSq;
            private float3 center;


            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(leftNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(rightNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, child.Bounds))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                float radius = this.radii[index];

                this.radiusSq = radius * radius;
                this.center = this.centers[index];

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetOverlappingBoundsInRadiusJob : IJob
        {

            public float radius;
            public float3 center;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private float radiusSq;

            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(leftNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(rightNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.CuboidSphereOverlap(child.Bounds, this.center, this.radiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.radiusSq = this.radius * this.radius;

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }



        [BurstCompile]
        public struct GetOverlappingBoundsInRadiiJob : IJobParallelFor
        {
            [ReadOnly, NoAlias]
            public NativeArray<float> radii;

            [ReadOnly, NoAlias]
            public NativeArray<float3> centers;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;


            private float radiusSq;
            private float3 center;


            private void AddSubtree(RStarNode3D node)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, leftNode.Bounds))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(leftNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsCuboid(this.center, this.radiusSq, rightNode.Bounds))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(rightNode.Bounds, this.center, this.radiusSq))
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.CuboidSphereOverlap(child.Bounds, this.center, this.radiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                float radius = this.radii[index];

                this.radiusSq = radius * radius;
                this.center = this.centers[index];

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetNearestNeighborsJob : IJob
        {
            public int root;

            [ReadOnly, NoAlias]
            public NativeArray<float3> queryPoints;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeArray<T> result;

            public int GetClosest(int index, float3 searchPos, ref float best)
            {

                var node = this.nodes[index];

                if (node.children >= 0)
                {
                    float closestDistance = float.PositiveInfinity;
                    int closestChild = -1;

                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIndex = childrenList[i];
                        T child = this.data[childIndex];

                        float distsq = math.distancesq(searchPos, child.Bounds.center);
                        if (distsq < closestDistance)
                        {
                            closestDistance = distsq;
                            closestChild = childIndex;
                        }
                    }
                    best = math.min(best, closestDistance);

                    return closestChild;

                }
                else
                {

                    var left = this.nodes[node.left];
                    var right = this.nodes[node.right];

                    var leftMin = (float3)left.Bounds.min;
                    var leftMax = (float3)left.Bounds.max;

                    var rightMin = (float3)right.Bounds.min;
                    var rightMax = (float3)right.Bounds.max;

                    float3 leftDiffMin = math.abs(searchPos - leftMin);
                    float3 leftDiffMax = math.abs(searchPos - leftMax);

                    float3 rightDiffMin = math.abs(searchPos - rightMin);
                    float3 rightDiffMax = math.abs(searchPos - rightMax);

                    float3 leftMaxDistances = math.max(leftDiffMin, leftDiffMax);
                    float3 rightMaxDistances = math.max(rightDiffMin, rightDiffMax);

                    float3 leftMinDistances = searchPos - (float3)left.Bounds.center - (float3)left.Bounds.extents;
                    float3 rightMinDistances = searchPos - (float3)right.Bounds.center - (float3)right.Bounds.extents;

                    leftMinDistances = math.clamp(leftMinDistances, 0, float.MaxValue);
                    rightMinDistances = math.clamp(rightMinDistances, 0, float.MaxValue);

                    float maxLeftDist = math.length(leftMaxDistances);
                    float maxRightDist = math.length(rightMaxDistances);

                    float minLeftDist = math.cmin(leftMinDistances);
                    float minRightDist = math.cmin(rightMinDistances);

                    if (maxLeftDist < maxRightDist)
                    {
                        int closest = this.GetClosest(node.left, searchPos, ref best);
                        if (best > minRightDist * minRightDist)
                        {
                            float bestLeft = best;
                            int closestRight = this.GetClosest(node.right, searchPos, ref best);
                            if (best < bestLeft) return closestRight;
                        }
                        return closest;
                    }
                    else
                    {
                        int closest = this.GetClosest(node.right, searchPos, ref best);
                        if (best > minLeftDist * minLeftDist)
                        {
                            float bestRight = best;
                            int closestLeft = this.GetClosest(node.left, searchPos, ref best);
                            if (best < bestRight) return closestLeft;
                        }
                        return closest;
                    }

                }
            }

            public void Execute()
            {
                for (int i = 0; i < this.queryPoints.Length; i++)
                {

                    float closestDistance = float.PositiveInfinity;
                    int closest = this.GetClosest(this.root, this.queryPoints[i], ref closestDistance);
                    if (closest >= 0) this.result[i] = this.data[closest];
                }
            }
        }

        [BurstCompile]
        public struct FrustumJob : IJob
        {

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias] 
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias] 
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            [ReadOnly, NoAlias, DeallocateOnJobCompletion]
            public NativeArray<Plane> frustumPlanes;

            private void AddSubtree(RStarNode3D node)
            {
                if(node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for(int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        this.result.Add(this.data[childIdx]);
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode);
                this.AddSubtree(rightNode);
            }

            private void BoxOverlapsFrustum(Bounds bounds, out bool overlap, out bool contained)
            {
                contained = true;
                overlap = true;
                var corners = bounds.GetCornerPoints();
                for (int i = 0; i < this.frustumPlanes.Length; i++) {

                    var plane = this.frustumPlanes[i];
                    bool allBehind = true;

                    for(int j = 0; j < 8; j++)
                    {
                        var corner = corners[j];
                        float dist = plane.GetDistanceToPoint(corner);
                        contained &= dist >= 0.0f;
                        allBehind &= dist < 0.0f;
                    }

                    if(allBehind)
                    {
                        overlap = false;
                        contained = false;
                        return;
                    }
                }
            }

            private void SearchRTreeRecursion(RStarNode3D node)
            {
                if(node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodexIdx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodexIdx];

                    this.BoxOverlapsFrustum(leftNode.Bounds, out bool leftOverlap, out bool leftContained);
                    this.BoxOverlapsFrustum(rightNode.Bounds, out bool rightOverlap, out bool rightContained);

                    if(leftContained)
                    {
                        this.AddSubtree(leftNode);
                    } else if(leftOverlap)
                    {
                        this.SearchRTreeRecursion(leftNode);
                    }

                    if(rightContained)
                    {
                        this.AddSubtree(rightNode);
                    } else if(rightOverlap)
                    {
                        this.SearchRTreeRecursion(rightNode);
                    }

                } else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        this.BoxOverlapsFrustum(child.Bounds, out bool overlap, out _);

                        if(overlap)
                        {
                            this.result.Add(child);
                        }
                    }
                }
            }

            public void Execute()
            {
                this.result.Clear();

                var rootNode = this.nodes[this.root];
                this.SearchRTreeRecursion(rootNode);
            }
        }


        [BurstCompile]
        public struct RaycastJob : IJob
        {
            public float distance;

            public int root;

            public Ray ray;

            //You can define your own comparers to sort the points in reverse order for example
            //However, the majority of people will want to have them sorted by increasing distance
            public IntersectionHit3D<T>.RayComparer comparer;

            [NoAlias]
            public NativeList<IntersectionHit3D<T>> result;

            [ReadOnly, NoAlias]
            public NativeList<RStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            private LineSegment3D lineSegment;

            private void IntersectRecursively(RStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.LineSegmentCuboidOverlap(this.lineSegment, leftNode.Bounds))
                    {
                        this.IntersectRecursively(leftNode);
                    }

                    if (ShapeOverlap.LineSegmentCuboidOverlap(this.lineSegment, rightNode.Bounds))
                    {
                        this.IntersectRecursively(rightNode);
                    }

                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        int intersections = ShapeIntersection.LineSegmentCuboidIntersections(this.lineSegment, child.Bounds,
                            out float3 intersection0, out float3 intersection1, out _);

                        if (intersections > 0)
                        {
                            var hitPoints = new FixedList32Bytes<float3>
                            {
                                intersection0
                            };
                            if (intersections > 1) hitPoints.Add(intersection1);

                            var intersectionHit = new IntersectionHit3D<T>()
                            {
                                boundingVolume = child,
                                hitPoints = hitPoints
                            };

                            this.result.Add(intersectionHit);
                        }
                    }
                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.lineSegment = new LineSegment3D()
                {
                    a = this.ray.origin,
                    b = this.ray.origin + this.ray.direction.normalized * this.distance
                };

                var rootNode = this.nodes[this.root];

                this.IntersectRecursively(rootNode);
                this.result.Sort(this.comparer);
            }
        }

    
    }
}
