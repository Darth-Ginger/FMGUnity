using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe partial struct Native2DBallStarTree<T> : IDisposable
        where T : unmanaged, IBoundingCircle, IIdentifiable, IEquatable<T>
    {

        [BurstCompile]
        public struct GetCirclesInRectangleJob : IJob
        {
            public Rect searchRect;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.RectangleContainsCircle(this.searchRect, child.Center, child.RadiusSq))
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
        public struct GetOverlappingCirclesInRectangleJob : IJob
        {
            public Rect searchRect;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, child.Center, child.RadiusSq))
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
        public struct GetCirclesInRectanglesJob : IJobParallelFor
        {
            [NoAlias, ReadOnly]
            public NativeArray<Rect> searchRectangles;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Rect searchRect;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.RectangleContainsCircle(this.searchRect, child.Center, child.RadiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                this.searchRect = this.searchRectangles[index];
                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetOverlappingCirclesInRectanglesJob : IJobParallelFor
        {
            [NoAlias, ReadOnly]
            public NativeArray<Rect> searchRectangles;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Rect searchRect;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.RectangleContainsCircle(this.searchRect, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.RectangleCircleOverlap(this.searchRect, child.Center, child.RadiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                this.searchRect = this.searchRectangles[index];
                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetCirclesInRadiusJob : IJob
        {
            public float radius;
            public float2 center;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private float radiusSquared;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, child.Center, child.RadiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.radiusSquared = this.radius * this.radius;

                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetCirclesInRadiiJob : IJobParallelFor
        {

            public int root;

            [ReadOnly, NoAlias]
            public NativeArray<float> radii;

            [ReadOnly, NoAlias]
            public NativeArray<float2> centers;


            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private float radiusSquared;
            private float2 center;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, child.Center, child.RadiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                float radius = this.radii[index];

                this.radiusSquared = radius * radius;
                this.center = this.centers[index];

                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode);
            }
        }

        [BurstCompile]
        public struct GetOverlappingCirclesInRadiusJob : IJob
        {
            public float radius;
            public float2 center;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            private float radiusSquared;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, child.Center, child.RadiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.radiusSquared = this.radius * this.radius;

                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode);
            }
        }


        [BurstCompile]
        public struct GetOverlappingCirclesInRadiiJob : IJobParallelFor
        {

            public int root;

            [ReadOnly, NoAlias]
            public NativeArray<float> radii;

            [ReadOnly, NoAlias]
            public NativeArray<float2> centers;


            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private float radiusSquared;
            private float2 center;

            private void AddSubtree(BallStarNode2D node)
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


            private void SearchBallTreeRecursion(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode);
                    }

                    if (ShapeOverlap.CircleContainsCircle(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode);
                    }
                    else if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, rightNode.Center, rightNode.RadiusSq))
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

                        if (ShapeOverlap.CircleCircleOverlap(this.center, this.radiusSquared, child.Center, child.RadiusSq))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                float radius = this.radii[index];

                this.radiusSquared = radius * radius;
                this.center = this.centers[index];

                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode);
            }
        }


        [BurstCompile]
        public struct RaycastJob : IJob
        {
            public float distance;

            public int root;

            public Ray2D ray;

            //You can define your own comparers to sort the points in reverse order for example
            //However, the majority of people will want to have them sorted by increasing distance
            public IntersectionHit2D<T>.RayComparer comparer;

            [NoAlias]
            public NativeList<IntersectionHit2D<T>> result;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            private LineSegment2D lineSegment;

            private void IntersectRecursively(BallStarNode2D node)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.LineSegmentCircleOverlap(this.lineSegment, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.IntersectRecursively(leftNode);
                    }

                    if (ShapeOverlap.LineSegmentCircleOverlap(this.lineSegment, rightNode.Center, rightNode.RadiusSq))
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

                        int intersections = ShapeIntersection.LineSegmentCircleIntersections(this.lineSegment, child.Center, child.RadiusSq,
                            out float2 intersection0, out float2 intersection1, out _);

                        if (intersections > 0)
                        {
                            var hitPoints = new FixedList32Bytes<float2>
                            {
                                intersection0
                            };
                            if (intersections > 1) hitPoints.Add(intersection1);

                            var intersectionHit = new IntersectionHit2D<T>()
                            {
                                boundingArea = child,
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

                this.lineSegment = new LineSegment2D()
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
            public CardinalPlane plane;

            public int root;

            public NativeArray<float3> queryPoints;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeArray<T> result;

            public int GetClosest(int index, float2 searchPos, ref float best)
            {
                var node = this.nodes[index];

                if(node.children >= 0)
                {
                    float closestDistance = float.PositiveInfinity;
                    int closestChild = -1;

                    var childrenList = this.childrenBuffer[node.children];
                    for(int i = 0; i < childrenList.Length; i++)
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

                } else
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

                    if(maxLeftDist < maxRightDist)
                    {
                        int closest = this.GetClosest(node.left, searchPos, ref best);
                        if(best > minRightDist * minRightDist)
                        {
                            float bestLeft = best;
                            int closestRight = this.GetClosest(node.right, searchPos, ref best);
                            if (best < bestLeft) return closestRight;
                        }
                        return closest;
                    } else
                    {
                        int closest = this.GetClosest(node.right, searchPos, ref best);
                        if(best > minLeftDist * minLeftDist)
                        {
                            float bestRight = best;
                            int closestLeft = this.GetClosest(node.left, searchPos, ref best);
                            if(best < bestRight) return closestLeft;
                        }
                        return closest;
                    }
                }
            }

            public void Execute()
            {
                var planePos = new float2();
                var indices = this.plane.GetAxisIndices();

                for(int i = 0; i < this.queryPoints.Length; i++)
                {

                    var position = this.queryPoints[i];
                    planePos.x = position[indices.x];
                    planePos.y = position[indices.y];

                    float closestDistance = float.PositiveInfinity;
                    int closest = this.GetClosest(this.root, planePos, ref closestDistance);
                    if (closest >= 0) this.result[i] = this.data[closest];
                }
            }
        }


        [BurstCompile]
        public struct GetCirclesInPolygonJob : IJob
        {
            public Matrix4x4 trs;

            public NativePolygon2D polygon;

            public int root;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeList<BallStarNode2D> nodes;

            [ReadOnly, NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [WriteOnly, NoAlias]
            public NativeList<T> result;

            #region Private Fields

            private NativePolygon2D offsetedPolygon;

            private Rect polygonBounds;

            #endregion

            private void AddSubtree(BallStarNode2D node, NativeArray<int> offsets)
            {
                if (node.children >= 0)
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];
                        if (this.offsetedPolygon.IsPointInsideInternal(child.Center, offsets))
                        {
                            this.result.Add(this.data[childIdx]);
                        }
                    }
                    return;
                }

                var leftNode = this.nodes[node.left];
                var rightNode = this.nodes[node.right];

                this.AddSubtree(leftNode, offsets);
                this.AddSubtree(rightNode, offsets);
            }


            private void SearchBallTreeRecursion(BallStarNode2D node, NativeArray<int> offsets)
            {
                if (node.left >= 0)
                {
                    var leftNodeIdx = node.left;
                    var rightNodeIx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIx];

                    if (ShapeOverlap.RectangleContainsCircle(this.polygonBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.AddSubtree(leftNode, offsets);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.polygonBounds, leftNode.Center, leftNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(leftNode, offsets);
                    }

                    if (ShapeOverlap.RectangleContainsCircle(this.polygonBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.AddSubtree(rightNode, offsets);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(this.polygonBounds, rightNode.Center, rightNode.RadiusSq))
                    {
                        this.SearchBallTreeRecursion(rightNode, offsets);
                    }
                }
                else
                {
                    var childrenList = this.childrenBuffer[node.children];
                    for (int i = 0; i < childrenList.Length; i++)
                    {
                        int childIdx = childrenList[i];
                        var child = this.data[childIdx];

                        if (this.offsetedPolygon.IsPointInsideInternal(child.Center, offsets))
                        {
                            this.result.Add(child);
                        }
                    }

                }
            }

            public void Execute()
            {
                this.result.Clear();

                this.offsetedPolygon = new NativePolygon2D(Allocator.Temp, this.polygon.points, this.polygon.separators);
                for (int i = 0; i < this.polygon.points.Length; i++)
                {
                    Vector3 pos3D = new Vector3();
                    pos3D.x = this.offsetedPolygon.points[i].x;
                    pos3D.y = this.offsetedPolygon.points[i].y;
                    var transformedPos = this.trs.MultiplyPoint(pos3D);

                    float2 point = new float2();
                    point.x = transformedPos.x;
                    point.y = transformedPos.y;

                    this.offsetedPolygon.points[i] = point;
                }

                this.polygonBounds = this.offsetedPolygon.GetBoundingRect();
                var offsets = this.offsetedPolygon.PrepareOffsets(Allocator.Temp);

                var rootNode = this.nodes[this.root];
                this.SearchBallTreeRecursion(rootNode, offsets);
            }

        }
    }
}
