using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{

    public unsafe partial struct Native3DBallStarTree<T> : IDisposable
        where T : unmanaged, IBoundingSphere, IIdentifiable, IEquatable<T>
    {

        [BurstCompile]
        public struct GetSpheresInBoundsJob : IJob
        {

            public Bounds searchBounds;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;


            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetOverlappingSpheresInBoundsJob : IJob
        {

            public Bounds searchBounds;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;


            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetSpheresInMultipleBoundsJob : IJobParallelFor
        {
            [NoAlias, ReadOnly]
            public NativeArray<Bounds> searchBoundaries;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Bounds searchBounds;

            private void AddSubtree(BallStarNode3D node)
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


            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }



        [BurstCompile]
        public struct GetOverlappingSpheresInMultipleBoundsJob : IJobParallelFor
        {
            [NoAlias, ReadOnly]
            public NativeArray<Bounds> searchBoundaries;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Bounds searchBounds;

            private void AddSubtree(BallStarNode3D node)
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


            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CuboidContainsSphere(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.CuboidSphereOverlap(this.searchBounds, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }


        [BurstCompile]
        public struct GetSpheresInRadiusJob : IJob
        {

            public float radius;
            public float3 center;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private float radiusSq;

            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetSpheresInRadiiJob : IJobParallelFor
        {
            [ReadOnly, NoAlias]
            public NativeArray<float> radii;

            [ReadOnly, NoAlias]
            public NativeArray<float3> centers;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;


            private float radiusSq;
            private float3 center;


            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetOverlappingSpheresInRadiusJob : IJob
        {

            public float radius;
            public float3 center;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private float radiusSq;

            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }



        [BurstCompile]
        public struct GetOverlappingSpheresInRadiiJob : IJobParallelFor
        {
            [ReadOnly, NoAlias]
            public NativeArray<float> radii;

            [ReadOnly, NoAlias]
            public NativeArray<float3> centers;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;


            private float radiusSq;
            private float3 center;


            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.SphereContainsSphere(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (ShapeOverlap.SphereSphereOverlap(this.center, this.radiusSq, child.Center, child.RadiusSq))
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct SphereCastJob : IJob
        {

            public int root;

            public Capsule capsule;

            [NoAlias]
            public NativeList<T> result;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;


            private void SphereCastRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CapsuleSphereOverlap(this.capsule, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SphereCastRecursion(leftNode);
                    }

                    if (ShapeOverlap.CapsuleSphereOverlap(this.capsule, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SphereCastRecursion(rightNode);
                    }

                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if(ShapeOverlap.CapsuleSphereOverlap(this.capsule, child.Center, child.RadiusSq)) {

                            this.result.Add(child);
                        }
                    }
                }
            }

            public void Execute()
            {
                this.result.Clear();

                var rootNode = this.nodes[this.root];

                this.SphereCastRecursion(rootNode);
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
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            private LineSegment3D lineSegment;

            private void IntersectRecursively(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.LineSegmentSphereOverlap(this.lineSegment, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.IntersectRecursively(leftNode);
                    }

                    if (ShapeOverlap.LineSegmentSphereOverlap(this.lineSegment, rightNode.Center, rightNode.RadiusSq))
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

                        int intersections = ShapeIntersection.LineSegmentSphereIntersections(this.lineSegment, child.Center, child.RadiusSq,
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

        [BurstCompile]
        public struct GetNearestNeighborsJob : IJob
        {
            public int root;

            public NativeArray<float3> queryPoints;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeArray<T> result;

            public int GetClosest(int index, float3 searchPos, ref float best)
            {
                var node = this.nodes[index];

                if(node.children >= 0)
                {
                    float closestDistance = float.PositiveInfinity;
                    int closestChild = -1;

                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIndex = childrenList[i];
                        T child = this.data[childIndex];

                        float distsq = math.distancesq(searchPos, child.Center);
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

                    float leftRadius = math.sqrt(left.RadiusSq);
                    float rightRadius = math.sqrt(right.RadiusSq);

                    float leftDist = math.distance(searchPos, left.Center);
                    float rightDist = math.distance(searchPos, right.Center);

                    float maxLeftDist = leftDist + leftRadius;
                    float maxRightDist = rightDist + rightRadius;

                    float minLeftDist = math.clamp(leftDist - leftRadius, 0, float.MaxValue);
                    float minRightDist = math.clamp(rightDist - rightRadius, 0, float.MaxValue);

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
            public NativeList<BallStarNode3D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            [ReadOnly, NoAlias, DeallocateOnJobCompletion]
            public NativeArray<Plane> frustumPlanes;

            private void SphereOverlapsFrustum(float3 center, float radiusSq, out bool overlap, out bool contained)
            {
                float radius = math.sqrt(radiusSq);

                overlap = true;
                contained = true;

                for(int i = 0; i < this.frustumPlanes.Length; i++) { 

                    var plane = this.frustumPlanes[i];
                    float dist = plane.GetDistanceToPoint(center);
                    if(dist < radius)
                    {
                        contained = false;
                        if (dist < -radius)
                        {
                            overlap = false;
                        }
                    }

                }
            }

            private void AddSubtree(BallStarNode3D node)
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

            private void SearchBallTreeRecursion(BallStarNode3D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    this.SphereOverlapsFrustum(leftNode.Center, leftNode.RadiusSq, out bool leftOverlap, out bool leftContained);
                    this.SphereOverlapsFrustum(rightNode.Center, rightNode.RadiusSq, out bool rightOverlap, out bool rightContained);

                    if(leftContained)
                    {
                        this.AddSubtree(leftNode);
                    } else if(leftOverlap)
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if(rightContained)
                    {
                        this.AddSubtree(rightNode);
                    } else if(rightOverlap)
                    {
                        this.SearchBallTreeRecursion(rightNode);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        this.SphereOverlapsFrustum(child.Center, child.RadiusSq, out bool overlap, out _);

                        if (overlap)
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
                this.SearchBallTreeRecursion(rootNode);
            }
        }
    }
}
