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
    public unsafe struct Native2DKDTree<T> : IDisposable where T : unmanaged, IPosition3D, IEquatable<T>
    {

        #region Private Variables

        private int axis0;
        private int axis1;

        private NativeArray<T> nodes;

        private Rect bounds;

        #endregion

        public T* GetRoot()
        {
            if (this.nodes.IsCreated)
            {
                return (T*)this.nodes.GetUnsafePtr();
            }
            return null;
        }

        public NativeArray<T> GetNodes() => this.nodes;

        public int GetAxis0() => this.axis0;
        public int GetAxis1() => this.axis1;

        public int Count => this.nodes.Length;

        public Rect GetBounds() => this.bounds;


        [BurstCompile]
        private struct KDTreeConstructionJob : IJob
        {

            public NativeList<T> nodeCopy;

            public NativeArray<T> nodes;

            public int axis0;
            public int axis1;

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

                    Composite2DComparer comparer = default;
                    switch(depth % 2)
                    {
                        case 0:
                            comparer = new Composite2DComparer() { axis0 = this.axis0, axis1 = this.axis1 };
                            break;
                        case 1:
                            comparer = new Composite2DComparer() { axis0 = this.axis1, axis1 = this.axis0 };
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



        private struct Composite2DComparer : IComparer<T>
        {
            public int axis0;
            public int axis1;

            public int Compare(T a, T b)
            {
                int comp = a.Position[this.axis0].CompareTo(b.Position[this.axis0]);
                if (comp != 0) return comp;
                return a.Position[this.axis1].CompareTo(b.Position[this.axis1]);
            }
        }

        private void ConstructKDTree(NativeArray<T> positions)
        {
            float xMin = float.PositiveInfinity;
            float xMax = float.NegativeInfinity;
            float yMin = float.PositiveInfinity;
            float yMax = float.NegativeInfinity;

            for(int i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];
                xMin = Mathf.Min(pos.Position[this.axis0], xMin);
                xMax = Mathf.Max(pos.Position[this.axis0], xMax);
                yMin = Mathf.Min(pos.Position[this.axis1], yMin);
                yMax = Mathf.Max(pos.Position[this.axis1], yMax);
            }

            this.bounds.Set(xMin, yMin, xMax - xMin, yMax - yMin);

            NativeList<T> listCopy = new NativeList<T>(positions.Length, Allocator.TempJob);

            listCopy.CopyFrom(positions);

            var constructKDTreeJob = new KDTreeConstructionJob()
            {
                nodeCopy = listCopy,
                axis0 = this.axis0,
                axis1 = this.axis1,
                nodes = this.nodes
            };

            constructKDTreeJob.Schedule().Complete();

            listCopy.Dispose();
        }
        
        private void Construct(NativeArray<T> positions, Allocator allocator)
        {
            if (positions == null || positions.Length == 0)
            {
                Debug.LogError("Tried to construct native 2D KD tree with an empty array!");
            }

            this.nodes = new NativeArray<T>(positions.Length, allocator);
            this.bounds = new Rect(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);

            this.ConstructKDTree(positions);
        }

        private Native2DKDTree(CardinalPlane sortMode)
        {
            this.nodes = default;

            var axisIndices = sortMode.GetAxisIndices();
            this.axis0 = axisIndices.x;
            this.axis1 = axisIndices.y;

            this.bounds = default;
        }

        public Native2DKDTree(T[] positions, CardinalPlane sortMode, Allocator allocator) : this(sortMode)
        {
            var nativePositions = new NativeArray<T>(positions.Length, Allocator.TempJob);
            nativePositions.CopyFrom(positions);

            this.Construct(nativePositions, allocator);

            nativePositions.Dispose();
        }

        public Native2DKDTree(List<T> positions, CardinalPlane sortMode, Allocator allocator) : this(positions.ToArray(), sortMode, allocator) { }

        public Native2DKDTree(NativeArray<T> positions, CardinalPlane sortMode, Allocator allocator) : this(sortMode) {

            this.Construct(positions, allocator);
        }



        [BurstCompile]
        public struct GetPointsInRadiusJob : IJob
        {
            public float radius;

            public int axis0;
            public int axis1;

            public Vector3 position;

            public Rect kdTreeBounds;


            [NoAlias]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeList<T> result;

            private float radiusSquared;

            private float2 planePos;

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

            private void SearchKDTreeRecursion(int currentNodeIdx, float xMin, float xMax, float yMin, float yMax, int axis)
            {

                var pos = new float2();
                var currentNode = this.nodes[currentNodeIdx];
                pos.x = currentNode.Position[this.axis0];
                pos.y = currentNode.Position[this.axis1];

                if (math.distancesq(this.planePos, pos) <= this.radiusSquared)
                {
                    this.result.Add(currentNode);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    float splitPlane = currentNode.Position[axis];

                    float xMax0 = xMax;
                    float yMax0 = yMax;

                    float xMin1 = xMin;
                    float yMin1 = yMin;

                    int nextAxis;
                    if (axis == this.axis0)
                    {
                        xMax0 = splitPlane;
                        xMin1 = splitPlane;
                        nextAxis = this.axis1;
                    }
                    else
                    {
                        yMax0 = splitPlane;
                        yMin1 = splitPlane;
                        nextAxis = this.axis0;
                    }

                    var bounds0 = Rect.MinMaxRect(xMin, yMin, xMax0, yMax0);
                    var bounds1 = Rect.MinMaxRect(xMin1, yMin1, xMax, yMax);

                    //Min and max inside the circle -> rectangle is completely contained inside the circle
                    if (ShapeOverlap.CircleContainsRectangle(this.planePos, this.radiusSquared, bounds0))
                    {
                        this.AddSubtree(left);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(bounds0, this.planePos, this.radiusSquared))
                    {
                        this.SearchKDTreeRecursion(left, xMin, xMax0, yMin, yMax0, nextAxis);
                    }

                    if (right < this.nodes.Length)
                    {
                        //Min and max inside the circle -> rectangle is completely contained inside the circle
                        if (ShapeOverlap.CircleContainsRectangle(this.planePos, this.radiusSquared, bounds1))
                        {
                            this.AddSubtree(right);
                        }
                        else if (ShapeOverlap.RectangleCircleOverlap(bounds1, this.planePos, this.radiusSquared))
                        {
                            this.SearchKDTreeRecursion(right, xMin1, xMax, yMin1, yMax, nextAxis);
                        }
                    }

                }
            }

            public void Execute()
            {
                float xMin, xMax, yMin, yMax;

                xMin = this.kdTreeBounds.xMin;
                xMax = this.kdTreeBounds.xMax;
                yMin = this.kdTreeBounds.yMin;
                yMax = this.kdTreeBounds.yMax;

                this.radiusSquared = this.radius * this.radius;

                this.planePos = new float2();
                this.planePos.x = this.position[this.axis0];
                this.planePos.y = this.position[this.axis1];

                this.SearchKDTreeRecursion(0, xMin, xMax, yMin, yMax, this.axis0);
            }
        }

        [BurstCompile]
        public struct GetPointsInRectangleJob : IJob
        {

            public int axis0;
            public int axis1;

            public Rect searchRect;
            public Rect kdTreeBounds;

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

            private void SearchKDTreeRecursion(int currentNodeIdx, float xMin, float xMax, float yMin, float yMax, int axis)
            {

                var position = new float3();
                var currentNode = this.nodes[currentNodeIdx];
                position.x = currentNode.Position[this.axis0];
                position.y = currentNode.Position[this.axis1];

                if (this.searchRect.Contains(position))
                {
                    this.result.Add(currentNode);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    float splitPlane = currentNode.Position[axis];

                    float xMax0 = xMax;
                    float yMax0 = yMax;

                    float xMin1 = xMin;
                    float yMin1 = yMin;

                    int nextAxis;
                    if (axis == this.axis0)
                    {
                        xMax0 = splitPlane;
                        xMin1 = splitPlane;
                        nextAxis = this.axis1;
                    }
                    else
                    {
                        yMax0 = splitPlane;
                        yMin1 = splitPlane;
                        nextAxis = this.axis0;
                    }

                    var bounds0 = Rect.MinMaxRect(xMin, yMin, xMax0, yMax0);
                    var bounds1 = Rect.MinMaxRect(xMin1, yMin1, xMax, yMax);

                    if (this.searchRect.Contains(bounds0))
                    {
                        this.AddSubtree(left);
                    }
                    else if (this.searchRect.Overlaps(bounds0))
                    {
                        this.SearchKDTreeRecursion(left, xMin, xMax0, yMin, yMax0, nextAxis);
                    }
                    
                    if (right < this.nodes.Length)
                    {
                        if (this.searchRect.Contains(bounds1))
                        {
                            this.AddSubtree(right);
                        }
                        else if (this.searchRect.Overlaps(bounds1))
                        {
                            this.SearchKDTreeRecursion(right, xMin1, xMax, yMin1, yMax, nextAxis);
                        }
                    }
                    
                }
            }


            public void Execute()
            {
                float xMin, xMax, yMin, yMax;

                xMin = this.kdTreeBounds.xMin;
                xMax = this.kdTreeBounds.xMax;
                yMin = this.kdTreeBounds.yMin;
                yMax = this.kdTreeBounds.yMax;

                this.SearchKDTreeRecursion(0, xMin, xMax, yMin, yMax, this.axis0);
            }
        }

        //This is the point where you'd wish default implementations were a thing in earlier versions uf Unity as well...
        //TODO: Replace methods with IKDTreePointsJob-Interface default implementation in 2-3 years
        [BurstCompile]
        public struct GetPointsInRadiiJob : IJobParallelFor
        {
            public int axis0;
            public int axis1;

            public Rect kdTreeBounds;

            [NoAlias, ReadOnly]
            public NativeArray<float> radii;

            [NoAlias, ReadOnly]
            public NativeArray<float3> positions;

            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private float radiusSquared;

            private float3 planePos;

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

            private void SearchKDTreeRecursion(int currentNodeIdx, float xMin, float xMax, float yMin, float yMax, int axis)
            {

                var position = new float3();
                var currentNode = this.nodes[currentNodeIdx];
                position.x = currentNode.Position[this.axis0];
                position.y = currentNode.Position[this.axis1];

                if (math.distancesq(this.planePos, position) <= this.radiusSquared)
                {
                    this.result.Add(currentNode);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    float splitPlane = currentNode.Position[axis];

                    float xMax0 = xMax;
                    float yMax0 = yMax;

                    float xMin1 = xMin;
                    float yMin1 = yMin;

                    int nextAxis;
                    if (axis == this.axis0)
                    {
                        xMax0 = splitPlane;
                        xMin1 = splitPlane;
                        nextAxis = this.axis1;
                    }
                    else
                    {
                        yMax0 = splitPlane;
                        yMin1 = splitPlane;
                        nextAxis = this.axis0;
                    }

                    var bounds0 = Rect.MinMaxRect(xMin, yMin, xMax0, yMax0);
                    var bounds1 = Rect.MinMaxRect(xMin1, yMin1, xMax, yMax);

                    float2 circleCenter = this.planePos.xy;

                    //Min and max inside the circle -> rectangle is completely contained inside the circle
                    if (ShapeOverlap.CircleContainsRectangle(circleCenter, this.radiusSquared, bounds0))
                    {
                        this.AddSubtree(left);
                    }
                    else if (ShapeOverlap.RectangleCircleOverlap(bounds0, circleCenter, this.radiusSquared))
                    {
                        this.SearchKDTreeRecursion(left, xMin, xMax0, yMin, yMax0, nextAxis);
                    }

                    if (right < this.nodes.Length)
                    {
                        //Min and max inside the circle -> rectangle is completely contained inside the circle
                        if (ShapeOverlap.CircleContainsRectangle(circleCenter, this.radiusSquared, bounds1))
                        {
                            this.AddSubtree(right);
                        }
                        else if (ShapeOverlap.RectangleCircleOverlap(bounds1, circleCenter, this.radiusSquared))
                        {
                            this.SearchKDTreeRecursion(right, xMin1, xMax, yMin1, yMax, nextAxis);
                        }
                    }

                }
            }

            public void Execute(int index)
            {
                float xMin, xMax, yMin, yMax;

                xMin = this.kdTreeBounds.xMin;
                xMax = this.kdTreeBounds.xMax;
                yMin = this.kdTreeBounds.yMin;
                yMax = this.kdTreeBounds.yMax;

                this.radiusSquared = this.radii[index] * this.radii[index];

                this.planePos = new Vector3();
                this.planePos.x = this.positions[index][this.axis0];
                this.planePos.y = this.positions[index][this.axis1];

                this.SearchKDTreeRecursion(0, xMin, xMax, yMin, yMax, this.axis0);
            }
        }


        //TODO: Replace methods with IKDTreePointsJob-Interface default implementation in 2-3 years
        [BurstCompile]
        public struct GetPointsInRectanglesJob : IJobParallelFor
        {

            public int axis0;
            public int axis1;

            public Rect kdTreeBounds;

            [NoAlias, ReadOnly]
            public NativeArray<Rect> searchRectangles;

            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeParallelHashSet<T>.ParallelWriter result;

            private Rect searchRect;

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

            private void SearchKDTreeRecursion(int currentNodeIdx, float xMin, float xMax, float yMin, float yMax, int axis)
            {

                var position = new float3();
                var currentNode = this.nodes[currentNodeIdx];
                position.x = currentNode.Position[this.axis0];
                position.y = currentNode.Position[this.axis1];

                if (this.searchRect.Contains(position))
                {
                    this.result.Add(currentNode);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                if (left < this.nodes.Length)
                {
                    float splitPlane = currentNode.Position[axis];

                    float xMax0 = xMax;
                    float yMax0 = yMax;

                    float xMin1 = xMin;
                    float yMin1 = yMin;

                    int nextAxis;
                    if (axis == this.axis0)
                    {
                        xMax0 = splitPlane;
                        xMin1 = splitPlane;
                        nextAxis = this.axis1;
                    }
                    else
                    {
                        yMax0 = splitPlane;
                        yMin1 = splitPlane;
                        nextAxis = this.axis0;
                    }

                    var bounds0 = Rect.MinMaxRect(xMin, yMin, xMax0, yMax0);
                    var bounds1 = Rect.MinMaxRect(xMin1, yMin1, xMax, yMax);

                    if (this.searchRect.Contains(bounds0))
                    {
                        this.AddSubtree(left);
                    }
                    else if (this.searchRect.Overlaps(bounds0))
                    {
                        this.SearchKDTreeRecursion(left, xMin, xMax0, yMin, yMax0, nextAxis);
                    }

                    if (right < this.nodes.Length)
                    {
                        if (this.searchRect.Contains(bounds1))
                        {
                            this.AddSubtree(right);
                        }
                        else if (this.searchRect.Overlaps(bounds1))
                        {
                            this.SearchKDTreeRecursion(right, xMin1, xMax, yMin1, yMax, nextAxis);
                        }
                    }

                }
            }


            public void Execute(int index)
            {
                float xMin, xMax, yMin, yMax;

                xMin = this.kdTreeBounds.xMin;
                xMax = this.kdTreeBounds.xMax;
                yMin = this.kdTreeBounds.yMin;
                yMax = this.kdTreeBounds.yMax;

                this.searchRect = this.searchRectangles[index];

                this.SearchKDTreeRecursion(0, xMin, xMax, yMin, yMax, this.axis0);
            }
        }


        [BurstCompile]
        public struct GetPointsInPolygonJob : IJob
        {

            public Matrix4x4 trs;

            public int axis0;
            public int axis1;

            public Rect kdTreeBounds;

            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, WriteOnly]
            public NativeList<T> result;

            [NoAlias, ReadOnly]
            public NativePolygon2D polygon;

            private NativePolygon2D offsetedPolygon;

            private Rect searchRect;

            private void AddSubtree(int node, NativeArray<int> offsets)
            {
                var pos = new float2();
                var nodePos = this.nodes[node];
                pos.x = nodePos.Position[this.axis0];
                pos.y = nodePos.Position[this.axis1];

                if(this.offsetedPolygon.IsPointInsideInternal(pos, offsets))
                {
                    this.result.Add(nodePos);
                }

                int left = node * 2 + 1;
                int right = node * 2 + 2;
                if (left < this.nodes.Length)
                {
                    this.AddSubtree(left, offsets);
                }

                if (right < this.nodes.Length)
                {
                    this.AddSubtree(right, offsets);
                }
            }

            private void SearchKDTreeRecursion(int currentNodeIdx, float xMin, float xMax, float yMin, float yMax, int axis, NativeArray<int> offsets)
            {

                var position = new float3();
                var currentNode = this.nodes[currentNodeIdx];
                position.x = currentNode.Position[this.axis0];
                position.y = currentNode.Position[this.axis1];

                if (this.searchRect.Contains(position)
                    && this.offsetedPolygon.IsPointInsideInternal(position.xy, offsets))
                {
                    this.result.Add(currentNode);
                }

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                bool leftValid = left < this.nodes.Length;
                bool rightValid = right < this.nodes.Length;

                if (leftValid || rightValid)
                {
                    float splitPlane = currentNode.Position[axis];

                    float xMax0 = xMax;
                    float yMax0 = yMax;

                    float xMin1 = xMin;
                    float yMin1 = yMin;

                    int nextAxis;
                    if (axis == this.axis0)
                    {
                        xMax0 = splitPlane;
                        xMin1 = splitPlane;
                        nextAxis = this.axis1;
                    }
                    else
                    {
                        yMax0 = splitPlane;
                        yMin1 = splitPlane;
                        nextAxis = this.axis0;
                    }

                    var bounds0 = Rect.MinMaxRect(xMin, yMin, xMax0, yMax0);
                    var bounds1 = Rect.MinMaxRect(xMin1, yMin1, xMax, yMax);

                    if (leftValid)
                    {
                        if (this.searchRect.Contains(bounds0))
                        {
                            this.AddSubtree(left, offsets);
                        }
                        else if (this.searchRect.Overlaps(bounds0))
                        {
                            this.SearchKDTreeRecursion(left, xMin, xMax0, yMin, yMax0, nextAxis, offsets);
                        }
                    }

                    if (rightValid)
                    {
                        if (this.searchRect.Contains(bounds1))
                        {
                            this.AddSubtree(right, offsets);
                        }
                        else if (this.searchRect.Overlaps(bounds1))
                        {
                            this.SearchKDTreeRecursion(right, xMin1, xMax, yMin1, yMax, nextAxis, offsets);
                        }
                    }

                }
            }


            public void Execute()
            {
                this.offsetedPolygon = new NativePolygon2D(Allocator.Temp, this.polygon.points, this.polygon.separators);
                for(int i = 0; i < this.polygon.points.Length; i++)
                {
                    Vector3 pos3D = new Vector3();
                    pos3D[this.axis0] = this.offsetedPolygon.points[i].x;
                    pos3D[this.axis1] = this.offsetedPolygon.points[i].y;
                    var transformedPos = this.trs.MultiplyPoint(pos3D);

                    float2 point = new float2();
                    point.x = transformedPos[this.axis0];
                    point.y = transformedPos[this.axis1];

                    this.offsetedPolygon.points[i] = point;
                }

                this.searchRect = this.offsetedPolygon.GetBoundingRect();
                var offsets = this.offsetedPolygon.PrepareOffsets(Allocator.Temp);

                float xMin, xMax, yMin, yMax;

                xMin = this.kdTreeBounds.xMin;
                xMax = this.kdTreeBounds.xMax;
                yMin = this.kdTreeBounds.yMin;
                yMax = this.kdTreeBounds.yMax;

                this.SearchKDTreeRecursion(0, xMin, xMax, yMin, yMax, this.axis0, offsets);
            }
        }

        [BurstCompile]
        public struct GetNearestNeighborJob : IJob
        {

            public int axis0;
            public int axis1;


            [NoAlias, ReadOnly]
            public NativeArray<T> nodes;

            [NoAlias, ReadOnly]
            public NativeArray<float3> queryPoints;

            [NoAlias, WriteOnly]
            public NativeArray<T> result;


            private int GetClosest(int currentNodeIdx, float2 planePos, int axis)
            {
                var pos = new float2();
                var currentNode = this.nodes[currentNodeIdx];
                pos.x = currentNode.Position[this.axis0];
                pos.y = currentNode.Position[this.axis1];

                int left = currentNodeIdx * 2 + 1;
                int right = currentNodeIdx * 2 + 2;

                bool leftValid = left < this.nodes.Length;
                bool rightValid = right < this.nodes.Length;

                if (leftValid || rightValid)
                {
                    float splitPlane = currentNode.Position[axis];

                    int nextAxis;
                    int cmpAxis;
                    if (axis == this.axis0)
                    {
                        nextAxis = this.axis1;
                        cmpAxis = 0;
                    }
                    else
                    {
                        nextAxis = this.axis0;
                        cmpAxis = 1;
                    }

                    if (leftValid && planePos[cmpAxis] < splitPlane)
                    {
                        int leftClosest = this.GetClosest(left, planePos, nextAxis);
                        float3 leftPos = this.nodes[leftClosest].Position;
                        float2 leftPlanePos = new float2();
                        leftPlanePos.x = leftPos[this.axis0];
                        leftPlanePos.y = leftPos[this.axis1];

                        float dist = math.distance(planePos, pos);
                        float bestDist = math.distance(planePos, leftPlanePos);

                        if (dist < bestDist)
                        {
                            leftClosest = currentNodeIdx;
                            bestDist = dist;
                        }

                        if (rightValid && planePos[cmpAxis] + bestDist > splitPlane)
                        {
                            int rightClosest = this.GetClosest(right, planePos, nextAxis);
                            float3 rightPos = this.nodes[rightClosest].Position;
                            float2 rightPlanePos = new float2();
                            rightPlanePos.x = rightPos[this.axis0];
                            rightPlanePos.y = rightPos[this.axis1];

                            float rightDist = math.distance(planePos, rightPlanePos);
                            if (rightDist < bestDist)
                            {
                                leftClosest = rightClosest;
                            }
                        }

                        return leftClosest;

                    }
                    else if(rightValid)
                    {
                        int rightClosest = this.GetClosest(right, planePos, nextAxis);
                        float3 rightPos = this.nodes[rightClosest].Position;
                        float2 rightPlanePos = new float2();
                        rightPlanePos.x = rightPos[this.axis0];
                        rightPlanePos.y = rightPos[this.axis1];

                        float dist = math.distance(planePos, pos);
                        float bestDist = math.distance(planePos, rightPlanePos);
 
                        if (dist < bestDist)
                        {
                            rightClosest = currentNodeIdx;
                            bestDist = dist;
                        }

                        if (leftValid && planePos[cmpAxis] - bestDist < splitPlane)
                        {
                            int leftClosest = this.GetClosest(left, planePos, nextAxis);
                            float3 leftPos = this.nodes[leftClosest].Position;
                            float2 leftPlanePos = new float2();
                            leftPlanePos.x = leftPos[this.axis0];
                            leftPlanePos.y = leftPos[this.axis1];

                            float leftDist = math.distance(planePos, leftPlanePos);
                            if (leftDist < bestDist)
                            {
                                rightClosest = leftClosest;
                            }
                        }
                        return rightClosest;
                    }

                }

                return currentNodeIdx;
                
            }


            public void Execute()
            {

                var planePos = new float2();

                for (int i = 0; i < this.queryPoints.Length; i++)
                {
                    var position = this.queryPoints[i];
                    planePos.x = position[this.axis0];
                    planePos.y = position[this.axis1];

                    int closest = this.GetClosest(0, planePos, this.axis0);
                    this.result[i] = this.nodes[closest];
                }
            }

        }


        public JobHandle GetNearestNeighbors(NativeArray<float3> queryPoints, ref NativeArray<T> nearestNeighbors, JobHandle dependsOn = default)
        {
            var nearestNeighborJob = new GetNearestNeighborJob()
            {
                axis0 = this.axis0,
                axis1 = this.axis1,
                nodes = this.nodes,
                queryPoints = queryPoints,
                result = nearestNeighbors,
            };

            return nearestNeighborJob.Schedule(dependsOn);
        }


        public JobHandle GetPointsInRectangle(Rect rect, ref NativeList<T> result, JobHandle dependsOn = default)
        {

            var pointsInRectangleJob = new GetPointsInRectangleJob()
            {
                nodes = this.nodes,
                searchRect = rect,
                result = result,
                axis0 = this.axis0,
                axis1 = this.axis1,
                kdTreeBounds = this.bounds
            };

            return pointsInRectangleJob.Schedule(dependsOn);
        }

        public JobHandle GetPointsInRectangles(NativeArray<Rect> rectangles, ref NativeParallelHashSet<T> result, 
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            var pointsInRectanglesJob = new GetPointsInRectanglesJob()
            {
                nodes = this.nodes,
                searchRectangles = rectangles,
                result = result.AsParallelWriter(),
                axis0 = this.axis0,
                axis1 = this.axis1,
                kdTreeBounds = this.bounds
            };

            return pointsInRectanglesJob.Schedule(rectangles.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetPointsInRadius(float3 center, float radius, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var pointsInRadiusJob = new GetPointsInRadiusJob()
            {
                nodes = this.nodes,
                axis0 = this.axis0,
                axis1 = this.axis1,
                position = center,
                radius = radius,
                result = result,
                kdTreeBounds = this.bounds
            };

            return pointsInRadiusJob.Schedule(dependsOn);
        }

        public JobHandle GetPointsInRadii(NativeArray<float3> centers, NativeArray<float> radii, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {

            if(centers.Length != radii.Length)
            {
                Debug.LogError("Centers and Radii arrays do not match in size!");
            }

            var pointsInRadiiJob = new GetPointsInRadiiJob()
            {
                axis0 = this.axis0,
                axis1 = this.axis1,
                kdTreeBounds = this.bounds,
                nodes = this.nodes,
                positions = centers,
                radii = radii,
                result = result.AsParallelWriter()
            };

            return pointsInRadiiJob.Schedule(centers.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetPointsInPolygon(NativePolygon2D polygon, Matrix4x4 trs, ref NativeList<T> result,
            JobHandle dependsOn = default)
        {
            var pointsInPolygonJob = new GetPointsInPolygonJob()
            {
                axis0 = this.axis0,
                axis1 = this.axis1,
                kdTreeBounds = this.bounds,
                nodes = this.nodes,
                result = result,
                polygon = polygon,
                trs = trs,
            };
            return pointsInPolygonJob.Schedule(dependsOn);
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
