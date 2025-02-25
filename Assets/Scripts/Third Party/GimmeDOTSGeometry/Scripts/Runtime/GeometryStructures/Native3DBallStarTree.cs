using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    //Ideas for creating an online-tree are from this paper from 1989: https://www1.icsi.berkeley.edu/ftp/pub/techreports/1989/tr-89-063.pdf
    //  - Modified it though to minimize radius (= overlap-chance) on insertion rather than volume
    //Splitting algorithm ideas are from this paper: https://arxiv.org/pdf/1511.00628.pdf

    //I used a trick I remember from a R*-Tree paper (a better studied structure)... or was it unrolled linked lists?
    //Anyway, m data-points are stored in each leaf, to get better cache-efficiency and flatter hierarchies. 

    //Added additional constraint, such that each node has < m leaf node children OR two internal node children OR zero internal node children
    //In other words: There is no internal node that has only one child

    //In general, the tree structure is modified and optimized for the case when a lot of objects are moving and inserted and removed again
    //Put in other words: I tried to make inserting, removing, updating and the queries balanced to each other 
    public unsafe partial struct Native3DBallStarTree<T> : IDisposable 
        where T : unmanaged, IBoundingSphere, IIdentifiable, IEquatable<T>
    {

        #region Private Variables

        private bool isCreated;

        private int maxChildren;
        private int root;
        private NativeReference<int> freeNodes;

        private NativeList<BallStarNode3D> nodes;
        private NativeList<FixedList128Bytes<int>> childrenBuffer;

        private NativeList<int> freeChildrenIndices;
        private NativeParallelHashSet<int> leaves;

        private NativeParallelHashMap<int, T> data;
        private NativeParallelHashMap<int, int> leafToNodeMap;

        private NativeReference<Unity.Mathematics.Random> rnd;

        #endregion

        public bool IsCreated => this.isCreated;

        public int Count => this.data.Count();


        public int MaxChildren() => this.maxChildren;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="allocator"></param>
        /// <param name="maxChildren"> At most 30 children are possible for each partition / sphere. 
        /// Higher values can result in better cache efficiency, flatter hierarchies, but slower traversal and query reasoning.
        /// Default value is a good balance. Consider changing the FixedListBytes-size in
        /// the BallStarNode struct for your use-case and distributions.</param>
        public Native3DBallStarTree(int capacity, Allocator allocator, int maxChildren = 16)
        {
            this.nodes = new NativeList<BallStarNode3D>(capacity, allocator);
            this.data = new NativeParallelHashMap<int, T>(capacity, allocator);
            this.childrenBuffer = new NativeList<FixedList128Bytes<int>>(1, allocator);

            this.freeChildrenIndices = new NativeList<int>(allocator);
            this.leaves = new NativeParallelHashSet<int>(1, allocator);

            this.leafToNodeMap = new NativeParallelHashMap<int, int>(capacity, allocator);

            this.maxChildren = maxChildren;
            this.root = 0;
            this.freeNodes = new NativeReference<int>(allocator);

            this.nodes.Add(new BallStarNode3D()
            {
                Center = float3.zero,
                RadiusSq = 0.0f,
                children = 0,
                left = -1,
                right = -1,
                parent = -1,
            });
            this.childrenBuffer.Add(new FixedList128Bytes<int>());
            this.leaves.Add(this.root);

            this.rnd = new NativeReference<Unity.Mathematics.Random>(allocator);
            var random = new Unity.Mathematics.Random();
            random.InitState();
            this.rnd.Value = random;

            this.isCreated = true;
        }

        public BallStarNode3D GetNode(int index)
        {
            return this.nodes[index];
        }

        public BallStarNode3D* GetRoot()
        {
            if(this.nodes.IsCreated)
            {
                return (BallStarNode3D*)this.nodes.GetUnsafePtr();

            } else
            {
                return null;
            }
        }


        private static void CalculateMinSphere2(float3 leftCenter, float leftRadiusSq, float3 rightCenter, float rightRadiusSq, out float3 center, out float radiusSq)
        {
            var dir = leftCenter - rightCenter;
            float dirSq = math.dot(dir, dir);

            float3 lengths = math.sqrt(new float3(leftRadiusSq, rightRadiusSq, dirSq));

            if(lengths.x + lengths.z < lengths.y)
            {
                radiusSq = rightRadiusSq;
                center = rightCenter;
            } else if(lengths.y + lengths.z < lengths.x)
            {
                radiusSq = leftRadiusSq;
                center = leftCenter;
            } else
            {
                float radius = math.csum(lengths) * 0.5f;
                float3 normalizedDir = math.rcp(lengths.z) * dir;
                center = math.mad(normalizedDir, lengths.x - radius, leftCenter);
                radiusSq = radius * radius;
            }
        }

        private void InsertIntoDataList(T value)
        {
            if(!this.data.ContainsKey(value.ID))
            {
                this.data.Add(value.ID, value);
            }
        }

        private int InsertIntoChildrenList(ref FixedList128Bytes<int> list)
        {
            if(this.freeChildrenIndices.Length > 0)
            {
                int idx = this.freeChildrenIndices[this.freeChildrenIndices.Length - 1];
                this.freeChildrenIndices.Length--;

                this.childrenBuffer[idx] = list;
                return idx;
            } else
            {
                this.childrenBuffer.Add(list);
                return this.childrenBuffer.Length - 1;
            }
        }

        private int InsertIntoNodesList(BallStarNode3D node)
        {
            if(this.freeNodes.Value > 0)
            {
                int idx = this.nodes.Length - this.freeNodes.Value;
                this.freeNodes.Value--;

                this.nodes[idx] = node;
                return idx;
            } else
            {
                this.nodes.Add(node);
                return this.nodes.Length - 1;
            }
        }

        private BallStarNode3D CreateSplitNode(int parent, ref FixedList128Bytes<int> children, float3 center, int childrenIndex)
        {
            float maxRadiusSq = 0.0f;

            for (int i = 0; i < children.Length; i++)
            {
                int childIndex = children[i];
                var child = this.data[childIndex];
                maxRadiusSq = math.max(maxRadiusSq, math.distancesq(center, child.Center));
            }

            return new BallStarNode3D()
            {
                Center = center,
                RadiusSq = maxRadiusSq,
                children = childrenIndex,
                left = -1,
                right = -1,
                parent = parent
            };
        }

        public void InsertIntoNode(BallStarNode3D node, int nodeIdx, T value)
        {
            var childrenList = this.childrenBuffer[node.children];
            int childrenCount = childrenList.Length;

            childrenList.Add(value.ID);
            if (childrenCount < this.maxChildren)
            {
                childrenCount++;

                if (childrenCount == 1)
                {
                    node.RadiusSq = value.RadiusSq;
                    node.Center = value.Center;
                }
                else
                {
                    int prevChildrenCount = childrenCount - 1;

                    float3 avg = math.mad(node.Center, prevChildrenCount, value.Center);
                    avg /= (float)childrenCount;

                    float maxRadiusSq = 0.0f;
                    for (int i = 0; i < childrenCount; i++)
                    {
                        int childIndex = childrenList[i];
                        T child = this.data[childIndex];
                        float dist = math.distancesq(avg, child.Center);
                        dist += 2.0f * math.sqrt(dist * child.RadiusSq) + child.RadiusSq;
                        maxRadiusSq = math.max(maxRadiusSq, dist);
                    }

                    node.Center = avg;
                    node.RadiusSq = maxRadiusSq;
                }

                this.leafToNodeMap.Add(value.ID, nodeIdx);
                this.nodes[nodeIdx] = node;
                this.childrenBuffer[node.children] = childrenList;
            }
            else
            {
                childrenCount++;

                //Split
                //The regression line is in 2D here, because it does not really matter too much as long
                //as the optimization heuristic is called, which fixes the errors quickly
                NativeArray<float2> centerValues = new NativeArray<float2>(childrenCount, Allocator.TempJob);
                for (int i = 0; i < childrenCount; i++)
                {
                    var childIndex = childrenList[i];
                    var child = this.data[childIndex];

                    centerValues[i] = child.Center.xz;
                }

                Line2D regressionLine = StatisticsUtil.EstimateRegressionLine2D(node.Center.xz, centerValues);
                NativeArray<DistanceSortingIndex> relativeDistances = new NativeArray<DistanceSortingIndex>(childrenCount, Allocator.TempJob);
                for (int i = 0; i < centerValues.Length; i++)
                {
                    float2 dir = centerValues[i] - regressionLine.point;
                    float relDist = VectorUtil.ScalarProjection(dir, regressionLine.direction);
                    relativeDistances[i] = new DistanceSortingIndex()
                    {
                        distance = relDist,
                        index = i,
                    };
                }

                //In-place sorting, because memory allocations are expensive
                NativeSorting.InsertionSort(ref relativeDistances, DistanceSortingIndexComparer.Default);

                int half = relativeDistances.Length / 2;
                FixedList128Bytes<int> left = new FixedList128Bytes<int>();
                FixedList128Bytes<int> right = new FixedList128Bytes<int>();

                float3 leftAvg = float3.zero;
                float3 rightAvg = float3.zero;

                for (int i = 0; i < relativeDistances.Length; i++)
                {
                    DistanceSortingIndex sortingIndex = relativeDistances[i];
                    int childIndex = childrenList[sortingIndex.index];
                    var child = this.data[childIndex];
                    if (i < half)
                    {
                        left.Add(childIndex);
                        leftAvg += child.Center;
                    }
                    else
                    {
                        right.Add(childIndex);
                        rightAvg += child.Center;
                    }
                }
                leftAvg /= left.Length;
                rightAvg /= right.Length;

                this.freeChildrenIndices.Add(node.children);


                int leftChildrenIdx = this.InsertIntoChildrenList(ref left);
                int rightChildrenIdx = this.InsertIntoChildrenList(ref right);

                var leftNode = this.CreateSplitNode(nodeIdx, ref left, leftAvg, leftChildrenIdx);
                var rightNode = this.CreateSplitNode(nodeIdx, ref right, rightAvg, rightChildrenIdx);

                int leftNodeIdx = this.InsertIntoNodesList(leftNode);
                int rightNodeIdx = this.InsertIntoNodesList(rightNode);

                for (int i = 0; i < left.Length; i++)
                {
                    int childIndex = left[i];
                    var child = this.data[childIndex];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, leftNodeIdx);
                }

                for (int i = 0; i < right.Length; i++)
                {
                    int childIndex = right[i];
                    var child = this.data[childIndex];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, rightNodeIdx);
                }

                this.leaves.Remove(nodeIdx);
                this.leaves.Add(leftNodeIdx);
                this.leaves.Add(rightNodeIdx);

                CalculateMinSphere2(leftNode.Center, leftNode.RadiusSq, rightNode.Center, rightNode.RadiusSq, out float3 center, out float radiusSq);

                node.Center = center;
                node.RadiusSq = radiusSq;
                node.left = leftNodeIdx;
                node.right = rightNodeIdx;
                node.children = -1;

                this.nodes[nodeIdx] = node;

                centerValues.Dispose();
                relativeDistances.Dispose();
            }
        }

        public void Insert(T value)
        {
            this.InsertIntoDataList(value);

            var rootNode = this.nodes[this.root];

            if(rootNode.children >= 0)
            {
                this.InsertIntoNode(rootNode, this.root, value);
            } else
            {
                var node = rootNode;
                int nodeIdx = this.root;
                while(node.children < 0)
                {
                    int leftNodeIdx = node.left;
                    int rightNodeIdx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIdx];

                    float distLeft = math.distancesq(leftNode.Center, value.Center);
                    float distRight = math.distancesq(rightNode.Center, value.Center);

                    if (distLeft < distRight)
                    {
                        node = leftNode;
                        nodeIdx = leftNodeIdx;
                    }
                    else
                    {
                        node = rightNode;
                        nodeIdx = rightNodeIdx;
                    }
                }

                this.InsertIntoNode(node, nodeIdx, value);
            }
        }


        private static void GetSibling(NativeList<BallStarNode3D> nodes, BallStarNode3D parentNode, int nodeIdx, out BallStarNode3D sibling, out int siblingIdx)
        {
            if(parentNode.left == nodeIdx)
            {
                siblingIdx = parentNode.right;
                sibling = nodes[parentNode.right];
            }
            else
            {
                siblingIdx = parentNode.left;
                sibling = nodes[parentNode.left];
            }
        }

        private void RemoveFromNodesList(int nodeIdx)
        {

            int lastIdx = this.nodes.Length - 1 - this.freeNodes.Value;
            if (nodeIdx == lastIdx)
            {
                this.freeNodes.Value++;
                return;
            }

            var lastNode = this.nodes[lastIdx];
            this.nodes[nodeIdx] = lastNode;

            var parentIdx = lastNode.parent;

            if (parentIdx >= 0)
            {
                var parent = this.nodes[parentIdx];
                if (parent.left == lastIdx)
                {
                    parent.left = nodeIdx;
                }
                else if (parent.right == lastIdx)
                {
                    parent.right = nodeIdx;
                }
                this.nodes[parentIdx] = parent;
            }

            if (lastNode.left >= 0)
            {
                int leftNodeIdx = lastNode.left;
                int rightNodeIdx = lastNode.right;

                var leftNode = this.nodes[leftNodeIdx];
                var rightNode = this.nodes[rightNodeIdx];

                leftNode.parent = nodeIdx;
                rightNode.parent = nodeIdx;

                this.nodes[leftNodeIdx] = leftNode;
                this.nodes[rightNodeIdx] = rightNode;
            }
            else if (this.leaves.Contains(lastIdx))
            {
                this.leaves.Remove(lastIdx);
                this.leaves.Add(nodeIdx);
                var children = this.childrenBuffer[lastNode.children];
                for (int i = 0; i < children.Length; i++)
                {
                    int childIdx = children[i];
                    var child = this.data[childIdx];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, nodeIdx);
                }
            }

            this.freeNodes.Value++;
        }

        private void CombineSiblings(BallStarNode3D parentNode, int parentNodeIdx, BallStarNode3D node, int nodeIdx, BallStarNode3D sibling, int siblingIdx)
        {
            var childrenList = this.childrenBuffer[node.children];
            int childrenCount = childrenList.Length;

            var siblingChildrenList = this.childrenBuffer[sibling.children];
            int siblingChildrenCount = siblingChildrenList.Length;

            //Also combine when one node has 0 children and the other maxChildren!
            if (childrenCount + siblingChildrenCount <= this.maxChildren)
            {
                FixedList128Bytes<int> combinedChildren = new FixedList128Bytes<int>();

                int idx = 0;
                for (; idx < childrenCount; idx++)
                {
                    int childIdx = childrenList[idx];
                    var child = this.data[childIdx];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, parentNodeIdx);
                    combinedChildren.Add(childIdx);
                }

                for (; idx < childrenCount + siblingChildrenCount; idx++)
                {
                    int childIdx = siblingChildrenList[idx - childrenCount];
                    var child = this.data[childIdx];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, parentNodeIdx);
                    combinedChildren.Add(childIdx);
                }

                this.freeChildrenIndices.Add(node.children);
                this.freeChildrenIndices.Add(sibling.children);

                this.leaves.Remove(nodeIdx);
                this.leaves.Remove(siblingIdx);
                this.leaves.Add(parentNodeIdx);

                int childrenIdx = this.InsertIntoChildrenList(ref combinedChildren);


                parentNode.left = -1;
                parentNode.right = -1;
                parentNode.children = childrenIdx;

                //We do not even need to calculate new radii or centers
                this.nodes[node.parent] = parentNode;
                if (nodeIdx > siblingIdx)
                {
                    this.RemoveFromNodesList(nodeIdx);
                    this.RemoveFromNodesList(siblingIdx);
                }
                else
                {
                    this.RemoveFromNodesList(siblingIdx);
                    this.RemoveFromNodesList(nodeIdx);
                }

            }
        }

        [BurstCompile]
        public struct RemoveJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeList<T> valuesToRemove;

            [NoAlias]
            public Native3DBallStarTree<T> tree;

            public void Execute()
            {
                for (int i = 0; i < this.valuesToRemove.Length; i++)
                {
                    this.tree.Remove(this.valuesToRemove[i]);
                }
            }
        }

        public JobHandle RemoveAll(NativeList<T> values, JobHandle dependsOn = default)
        {
            var removeAllJob = new RemoveJob()
            {
                tree = this,
                valuesToRemove = values,
            };
            return removeAllJob.Schedule(dependsOn);
        }

        public void Remove(T value)
        {
            if (this.data.ContainsKey(value.ID))
            {
                int nodeIdx = this.leafToNodeMap[value.ID];
                var node = this.nodes[nodeIdx];

                var childrenList = this.childrenBuffer[node.children];
                int childrenCount = childrenList.Length;

                int listIndex = 0;
                for (int i = 0; i < childrenCount; i++)
                {
                    int childIndex = childrenList[i];
                    T child = this.data[childIndex];
                    if (child.ID == value.ID)
                    {
                        listIndex = i;
                        break;
                    }
                }

                this.data.Remove(value.ID);
                this.leafToNodeMap.Remove(value.ID);
                childrenList.RemoveAtSwapBack(listIndex);
                this.childrenBuffer[node.children] = childrenList;
                childrenCount--;

                if (node.parent >= 0)
                {
                    var parentNode = this.nodes[node.parent];
                    GetSibling(this.nodes, parentNode, nodeIdx, out var sibling, out int siblingIdx);
                    if (sibling.children >= 0)
                    {
                        //Combine left- and right children
                        this.CombineSiblings(parentNode, node.parent, node, nodeIdx, sibling, siblingIdx);
                    }
                    else if (childrenCount == 0)
                    {
                        //If sibling is an internal node -> remove parent and set sibling as parent
                        this.leaves.Remove(nodeIdx);

                        this.freeChildrenIndices.Add(node.children);

                        var grandParentIdx = parentNode.parent;
                        sibling.parent = grandParentIdx;

                        int siblingLeftIdx = sibling.left;
                        int siblingRightIdx = sibling.right;

                        if (siblingLeftIdx >= 0)
                        {
                            var siblingLeftNode = this.nodes[siblingLeftIdx];
                            siblingLeftNode.parent = node.parent;
                            this.nodes[siblingLeftIdx] = siblingLeftNode;
                        }

                        if (siblingRightIdx >= 0)
                        {
                            var siblingRightNode = this.nodes[siblingRightIdx];
                            siblingRightNode.parent = node.parent;
                            this.nodes[siblingRightIdx] = siblingRightNode;
                        }

                        this.nodes[node.parent] = sibling;
                        if (nodeIdx > siblingIdx)
                        {
                            this.RemoveFromNodesList(nodeIdx);
                            this.RemoveFromNodesList(siblingIdx);
                        }
                        else
                        {
                            this.RemoveFromNodesList(siblingIdx);
                            this.RemoveFromNodesList(nodeIdx);
                        }

                    }

                }
            }
        }

        public void Update(T value)
        {
            if(this.data.ContainsKey(value.ID))
            {
                int nodeIdx = this.leafToNodeMap[value.ID];
                UpdateValue(value, nodeIdx, this.nodes, this.data);
            }
        }

        private static void UpdateValue(T value, int nodeIdx,
            NativeList<BallStarNode3D> nodes, NativeParallelHashMap<int, T> data)
        {
            var node = nodes[nodeIdx];

            //We only need to ensure, at a minimum, that we find the point again in the tree in a query,
            //which is why we only update the radius, and not the center (which is done in Optimize())
            float distanceSq = math.distancesq(node.Center, value.Center);
            distanceSq += 2.0f * math.sqrt(distanceSq * value.RadiusSq) + value.RadiusSq;
            bool radiusWasIncreased = false;
            if (distanceSq > node.RadiusSq)
            {
                node.RadiusSq = distanceSq;
                nodes[nodeIdx] = node;
                radiusWasIncreased = true;
            }
            data[value.ID] = value;

            if (radiusWasIncreased)
            {
                var parentNodeIdx = node.parent;
                while (parentNodeIdx >= 0)
                {
                    var parentNode = nodes[parentNodeIdx];

                    var leftNodeIdx = parentNode.left;
                    var rightNodeIdx = parentNode.right;

                    var leftNode = nodes[leftNodeIdx];
                    var rightNode = nodes[rightNodeIdx];

                    CalculateMinSphere2(leftNode.Center, leftNode.RadiusSq, rightNode.Center, rightNode.RadiusSq, out float3 center, out float radiusSq);

                    parentNode.RadiusSq = radiusSq;
                    parentNode.Center = center;
                    nodes[parentNodeIdx] = parentNode;

                    parentNodeIdx = parentNode.parent;
                }
            }
        }

        [BurstCompile]
        public struct UpdateJob : IJob
        {

            [ReadOnly, NoAlias]
            public NativeList<T> values;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, int> leafToNodeMap;

            [NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [WriteOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeParallelHashSet<int> leaves;


            private void UpdateValue(T value, BallStarNode3D* nodePtr, int nodeIdx)
            {
                var node = nodePtr + nodeIdx;
                float distanceSq = math.distancesq(node->Center, value.Center);
                distanceSq += math.mad(2.0f, math.sqrt(distanceSq * value.RadiusSq), value.RadiusSq);
                node->RadiusSq = math.max(node->RadiusSq, distanceSq);
            }

            public void Execute()
            {
                BallStarNode3D* nodePtr = (BallStarNode3D*)this.nodes.GetUnsafePtr();

                for (int i = 0; i < this.values.Length; i++)
                {
                    var value = this.values[i];
                    //Assume the best, so Burst can vectorize this loop...
                    //if (this.data.ContainsKey(value.ID))
                    //{
                    int nodeIdx = this.leafToNodeMap[value.ID];
                    this.UpdateValue(value, nodePtr, nodeIdx);
                    this.data[value.ID] = value;
                    //} 
                }

                foreach (var leafIdx in this.leaves)
                {
                    var leafNode = nodePtr + leafIdx;
                    var parentNodeIdx = leafNode->parent;
                    while (parentNodeIdx >= 0)
                    {
                        var parentNode = nodePtr + parentNodeIdx;

                        var leftNode = nodePtr + parentNode->left;
                        var rightNode = nodePtr + parentNode->right;

                        CalculateMinSphere2(leftNode->Center, leftNode->RadiusSq, rightNode->Center, rightNode->RadiusSq, 
                            out float3 center, out float radiusSq);

                        if(parentNode->RadiusSq == radiusSq && math.all(parentNode->Center == center))
                        {
                            //Early Quit
                            break;
                        }

                        parentNode->RadiusSq = radiusSq;
                        parentNode->Center = center;

                        parentNodeIdx = parentNode->parent;
                    }
                }
            }
        }

        public JobHandle UpdateAll(NativeList<T> values, JobHandle dependsOn = default)
        {
            var updateAllJob = new UpdateJob()
            {
                data = this.data,
                leafToNodeMap = this.leafToNodeMap,
                leaves = this.leaves,
                nodes = this.nodes,
                values = values
            };

            return updateAllJob.Schedule(dependsOn);
        }

        [BurstCompile]
        public struct OptimizeJob : IJob
        {
            public int leafSwaps;
            public int grandchildTricks;
            public int maxChildren;

            [NoAlias]
            public NativeReference<int> freeNodes;

            [NoAlias, ReadOnly]
            public NativeParallelHashMap<int, T> data;

            [NoAlias]
            public NativeList<BallStarNode3D> nodes;

            [NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [NoAlias]
            public NativeParallelHashSet<int> leaves;

            //We want to continue the state of the random values instead of always using the same ones, therefore we use a reference
            [NoAlias]
            public NativeReference<Unity.Mathematics.Random> rndRef;

            [NoAlias]
            public NativeParallelHashMap<int, int> leafToNodeMap;


            private void StealChildren(int nodeIdxA, BallStarNode3D nodeA, BallStarNode3D nodeB)
            {
                var childrenA = this.childrenBuffer[nodeA.children];
                var childrenB = this.childrenBuffer[nodeB.children];

                int nrOfChildrenA = childrenA.Length;
                int nrOfChildrenB = childrenB.Length;

                float radiusSqB = nodeB.RadiusSq;


                for (int i = 0; nrOfChildrenA < this.maxChildren && nrOfChildrenB > 2 && i < nrOfChildrenB; i++)
                {
                    var childIdx = childrenB[i];
                    T child = this.data[childIdx];
                    float distToA = math.distancesq(nodeA.Center, child.Center);
                    distToA += 2.0f * math.sqrt(distToA * child.RadiusSq) + child.RadiusSq;

                    float distToB = math.distancesq(nodeB.Center, child.Center);
                    distToB += 2.0f * math.sqrt(distToB * child.RadiusSq) + child.RadiusSq;

                    if (distToA < radiusSqB && distToA < distToB)
                    {
                        childrenA.Add(childIdx);
                        childrenB.RemoveAtSwapBack(i);
                        nrOfChildrenA++;
                        nrOfChildrenB--;

                        this.leafToNodeMap.Remove(childIdx);
                        this.leafToNodeMap.Add(childIdx, nodeIdxA);
                    }
                }

                this.childrenBuffer[nodeA.children] = childrenA;
                this.childrenBuffer[nodeB.children] = childrenB;
            }

            private void LeafSwap(BallStarNode3D nodeA, BallStarNode3D nodeB, int nodeIdxA, int nodeIdxB,
                NativeList<DistanceSortingIndex> listA, NativeList<DistanceSortingIndex> listB)
            {
                var childrenA = this.childrenBuffer[nodeA.children];
                var childrenB = this.childrenBuffer[nodeB.children];

                int lastIdxA = listA.Length - 1;
                int lastIdxB = listB.Length - 1;

                if (childrenA.Length > 1 && childrenB.Length > 1)
                {

                    for (int i = lastIdxA; i >= 0; i--)
                    {
                        int idxA = listA[i].index;
                        int maxChildIdxA = childrenA[idxA];
                        var maxChildA = this.data[maxChildIdxA];

                        bool swappedElement = false;
                        for (int j = lastIdxB; j >= 0; j--)
                        {
                            int idxB = listB[j].index;
                            int maxChildIdxB = childrenB[idxB];
                            var maxChildB = this.data[maxChildIdxB];

                            float swapRadiusA = math.distancesq(nodeA.Center, maxChildB.Center);
                            swapRadiusA += 2.0f * Mathf.Sqrt(swapRadiusA * maxChildB.RadiusSq) + maxChildB.RadiusSq;

                            float swapRadiusB = math.distancesq(nodeB.Center, maxChildA.Center);
                            swapRadiusB += 2.0f * Mathf.Sqrt(swapRadiusB * maxChildA.RadiusSq) + maxChildA.RadiusSq;

                            if (swapRadiusA + swapRadiusB < listA[i].distance + listB[j].distance)
                            {
                                childrenA[idxA] = maxChildIdxB;
                                childrenB[idxB] = maxChildIdxA;

                                this.leafToNodeMap.Remove(maxChildIdxA);
                                this.leafToNodeMap.Remove(maxChildIdxB);

                                this.leafToNodeMap.Add(maxChildIdxA, nodeIdxB);
                                this.leafToNodeMap.Add(maxChildIdxB, nodeIdxA);

                                float oldDistA = listA[i].distance;
                                float oldDistB = listB[j].distance;

                                listA[i] = new DistanceSortingIndex() { distance = swapRadiusA, index = idxA };
                                listB[j] = new DistanceSortingIndex() { distance = swapRadiusB, index = idxB };

                                if (swapRadiusA > oldDistA)
                                {
                                    for (int k = i; k < listA.Length - 1; k++)
                                    {
                                        if (listA[k].distance < listA[k + 1].distance) break;

                                        var tmp = listA[k];
                                        listA[k] = listA[k + 1];
                                        listA[k + 1] = tmp;
                                    }
                                }
                                else
                                {

                                    for (int k = i - 1; k >= 0; k--)
                                    {
                                        if (listA[k + 1].distance > listA[k].distance) break;

                                        var tmp = listA[k];
                                        listA[k] = listA[k + 1];
                                        listA[k + 1] = tmp;
                                    }
                                }

                                if (swapRadiusB > oldDistB)
                                {
                                    for (int k = j; k < listB.Length - 1; k++)
                                    {
                                        if (listB[k].distance < listB[k + 1].distance) break;

                                        var tmp = listB[k];
                                        listB[k] = listB[k + 1];
                                        listB[k + 1] = tmp;
                                    }
                                }
                                else
                                {
                                    for (int k = j - 1; k >= 0; k--)
                                    {
                                        if (listB[k + 1].distance > listB[k].distance) break;

                                        var tmp = listB[k];
                                        listB[k] = listB[k + 1];
                                        listB[k + 1] = tmp;
                                    }
                                }

                                swappedElement = true;
                                break;
                            }
                        }

                        if (!swappedElement) break;
                    }

                }

                this.childrenBuffer[nodeA.children] = childrenA;
                this.childrenBuffer[nodeB.children] = childrenB;
            }



            private void DoGrandChildTrick(BallStarNode3D node, int nodeIdx)
            {

                //Swapping is very tame compared to rotations in AVL-Trees - just an exercise in thoroughly covering all cases
                if (node.parent > 0)
                {
                    int parent0Idx = node.parent;
                    var parent0Node = this.nodes[parent0Idx];

                    int grandParentIdx = parent0Node.parent;

                    if (grandParentIdx >= 0)
                    {
                        var grandParentNode = this.nodes[grandParentIdx];

                        GetSibling(this.nodes, parent0Node, nodeIdx, out var sibling0, out int sibling0Idx);
                        GetSibling(this.nodes, grandParentNode, parent0Idx, out var parent1Node, out int parent1Idx);

                        //Ordinary subtree case:
                        //
                        //          o
                        //        /   \
                        //       o     o
                        //     /  \   /  \
                        //    o  ->n o    o
                        if (parent1Node.left >= 0)
                        {

                            int sibling1Idx = parent1Node.left;
                            int sibling2Idx = parent1Node.right;

                            var sibling1 = this.nodes[sibling1Idx];
                            var sibling2 = this.nodes[sibling2Idx];

                            CalculateMinSphere2(node.Center, node.RadiusSq, sibling1.Center, sibling1.RadiusSq, out float3 thisCenter, out float thisRadiusSq);
                            CalculateMinSphere2(sibling0.Center, sibling0.RadiusSq, sibling2.Center, sibling2.RadiusSq, out float3 otherCenter, out float otherRadiusSq);

                            bool swappedNodes = false;
                            if (thisRadiusSq + otherRadiusSq < parent0Node.RadiusSq + parent1Node.RadiusSq)
                            {
                                parent0Node.Center = thisCenter;
                                parent0Node.RadiusSq = thisRadiusSq;
                                parent0Node.left = nodeIdx;
                                parent0Node.right = sibling1Idx;

                                parent1Node.Center = otherCenter;
                                parent1Node.RadiusSq = otherRadiusSq;
                                parent1Node.left = sibling0Idx;
                                parent1Node.right = sibling2Idx;

                                node.parent = parent0Idx;
                                sibling1.parent = parent0Idx;
                                sibling0.parent = parent1Idx;
                                sibling2.parent = parent1Idx;

                                swappedNodes = true;

                            }

                            else
                            {
                                CalculateMinSphere2(node.Center, node.RadiusSq, sibling2.Center, sibling2.RadiusSq, out thisCenter, out thisRadiusSq);
                                CalculateMinSphere2(sibling0.Center, sibling0.RadiusSq, sibling1.Center, sibling1.RadiusSq, out otherCenter, out otherRadiusSq);

                                if (thisRadiusSq + otherRadiusSq < parent0Node.RadiusSq + parent1Node.RadiusSq)
                                {
                                    parent0Node.Center = thisCenter;
                                    parent0Node.RadiusSq = thisRadiusSq;
                                    parent0Node.left = nodeIdx;
                                    parent0Node.right = sibling2Idx;

                                    parent1Node.Center = otherCenter;
                                    parent1Node.RadiusSq = otherRadiusSq;
                                    parent1Node.left = sibling0Idx;
                                    parent1Node.right = sibling1Idx;


                                    node.parent = parent0Idx;
                                    sibling2.parent = parent0Idx;
                                    sibling0.parent = parent1Idx;
                                    sibling1.parent = parent1Idx;

                                    swappedNodes = true;
                                }
                            }

                            if (swappedNodes)
                            {

                                this.nodes[parent0Idx] = parent0Node;
                                this.nodes[parent1Idx] = parent1Node;
                                this.nodes[nodeIdx] = node;
                                this.nodes[sibling0Idx] = sibling0;
                                this.nodes[sibling1Idx] = sibling1;
                                this.nodes[sibling2Idx] = sibling2;

                                this.UpdateNodeParents(parent0Node);
                            }
                        }
                        //Truncated subtree case (parent sibling is leaf):
                        //
                        //          o
                        //        /   \
                        //       o     o
                        //     /  \   ---
                        //    o  ->n(ode)
                        else
                        {
                            CalculateMinSphere2(node.Center, node.RadiusSq, parent1Node.Center, parent1Node.RadiusSq, out float3 newCenter, out float newRadiusSq);

                            if (sibling0.RadiusSq + newRadiusSq < parent0Node.RadiusSq + parent1Node.RadiusSq)
                            {
                                parent0Node.Center = newCenter;
                                parent0Node.RadiusSq = newRadiusSq;
                                parent0Node.left = nodeIdx;
                                parent0Node.right = parent1Idx;
                                parent0Node.parent = grandParentIdx;

                                grandParentNode.left = parent0Idx;
                                grandParentNode.right = sibling0Idx;

                                node.parent = parent0Idx;
                                parent1Node.parent = parent0Idx;
                                sibling0.parent = grandParentIdx;

                                this.nodes[parent0Idx] = parent0Node;
                                this.nodes[parent1Idx] = parent1Node;
                                this.nodes[nodeIdx] = node;
                                this.nodes[sibling0Idx] = sibling0;
                                this.nodes[grandParentIdx] = grandParentNode;

                                this.UpdateNodeParents(parent0Node);
                            }
                        }

                    }
                }
            }

            private void UpdateNodeBounds(ref BallStarNode3D node)
            {
                float3 avg = 0.0f;

                var childrenList = this.childrenBuffer[node.children];
                int count = childrenList.Length;

                for (int i = 0; i < count; i++)
                {
                    int childIndex = childrenList[i];
                    T child = this.data[childIndex];
                    avg += child.Center;
                }
                avg /= count;

                float maxRadiusSq = 0.0f;
                for (int i = 0; i < count; i++)
                {
                    int childIndex = childrenList[i];
                    T child = this.data[childIndex];
                    float dist = math.distancesq(avg, child.Center);
                    dist += 2.0f * math.sqrt(dist * child.RadiusSq) + child.RadiusSq;
                    maxRadiusSq = math.max(maxRadiusSq, dist);
                }

                node.Center = avg;
                node.RadiusSq = maxRadiusSq;

            }

            private void PrepareSortedDistances(BallStarNode3D nodeA, BallStarNode3D nodeB,
                ref NativeList<DistanceSortingIndex> listA, ref NativeList<DistanceSortingIndex> listB)
            {
                var childrenListA = this.childrenBuffer[nodeA.children];
                var childrenListB = this.childrenBuffer[nodeB.children];

                for (int i = 0; i < childrenListA.Length; i++)
                {
                    int childIndex = childrenListA[i];
                    T child = this.data[childIndex];
                    float dist = math.distancesq(nodeA.Center, child.Center);
                    dist += 2.0f * math.sqrt(dist * child.RadiusSq) + child.RadiusSq;
                    listA.Add(new DistanceSortingIndex() { distance = dist, index = i });
                }

                for (int i = 0; i < childrenListB.Length; i++)
                {
                    int childIndex = childrenListB[i];
                    T child = this.data[childIndex];
                    float dist = math.distancesq(nodeB.Center, child.Center);
                    dist += 2.0f * math.sqrt(dist * child.RadiusSq) + child.RadiusSq;
                    listB.Add(new DistanceSortingIndex() { distance = dist, index = i });
                }
            }


            private void UpdateNodeParents(BallStarNode3D node)
            {
                var parentNodeIdx = node.parent;
                while (parentNodeIdx >= 0)
                {
                    var parentNode = this.nodes[parentNodeIdx];

                    var leftNodeIdx = parentNode.left;
                    var rightNodeIdx = parentNode.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIdx];

                    CalculateMinSphere2(leftNode.Center, leftNode.RadiusSq, rightNode.Center, rightNode.RadiusSq, out float3 center, out float radiusSq);

                    parentNode.RadiusSq = radiusSq;
                    parentNode.Center = center;
                    this.nodes[parentNodeIdx] = parentNode;

                    parentNodeIdx = parentNode.parent;
                }
            }

            public void Execute()
            {
                var rnd = this.rndRef.Value;


                if (this.nodes.Length > 1)
                {
                    var leavesArray = this.leaves.ToNativeArray(Allocator.Temp);

                    //Strategy 1: Leaf-Swap
                    if (leavesArray.Length > 1)
                    {
                        for (int i = 0; i < this.leafSwaps; i++)
                        {
                            int leaf0Idx = rnd.NextInt(0, leavesArray.Length);
                            int leaf1Idx = rnd.NextInt(0, leavesArray.Length);

                            if (leaf0Idx == leaf1Idx) continue;

                            int leaf0NodeIdx = leavesArray[leaf0Idx];
                            int leaf1NodeIdx = leavesArray[leaf1Idx];

                            var leaf0 = this.nodes[leaf0NodeIdx];
                            var leaf1 = this.nodes[leaf1NodeIdx];

                            this.UpdateNodeBounds(ref leaf0);
                            this.UpdateNodeBounds(ref leaf1);

                            //If leaves don't overlap -> don't do anything
                            if (ShapeOverlap.SphereSphereOverlap(leaf0.Center, leaf0.RadiusSq, leaf1.Center, leaf1.RadiusSq))
                            {
                                this.StealChildren(leaf0NodeIdx, leaf0, leaf1);

                                this.UpdateNodeBounds(ref leaf0);
                                this.UpdateNodeBounds(ref leaf1);

                                NativeList<DistanceSortingIndex> distancesA = new NativeList<DistanceSortingIndex>(Allocator.Temp);
                                NativeList<DistanceSortingIndex> distancesB = new NativeList<DistanceSortingIndex>(Allocator.Temp);

                                this.PrepareSortedDistances(leaf0, leaf1, ref distancesA, ref distancesB);

                                NativeSorting.InsertionSort(ref distancesA, new DistanceSortingIndexComparer());
                                NativeSorting.InsertionSort(ref distancesB, new DistanceSortingIndexComparer());

                                this.LeafSwap(leaf0, leaf1, leaf0NodeIdx, leaf1NodeIdx, distancesA, distancesB);

                                this.UpdateNodeBounds(ref leaf0);
                                this.UpdateNodeBounds(ref leaf1);
                            }

                            this.nodes[leaf0NodeIdx] = leaf0;
                            this.nodes[leaf1NodeIdx] = leaf1;

                            this.UpdateNodeParents(leaf0);
                            this.UpdateNodeParents(leaf1);
                        }
                    }

                    //Strategy 2: Grandchild-Trick
                    if (this.nodes.Length >= 7)
                    {
                        for (int i = 0; i < this.grandchildTricks; i++)
                        {
                            int nodeIdx = rnd.NextInt(0, this.nodes.Length - this.freeNodes.Value);
                            var node = this.nodes[nodeIdx];

                            this.DoGrandChildTrick(node, nodeIdx);

                        }
                    }

                }

                this.rndRef.Value = rnd;
            }
        }

        /// <summary>
        /// Deploys randomized optimization heuristic. In other words, I am doing some stuff I think that would help,
        /// making the structure faster to query. If you do not optimize often, the performance will drop
        /// significantly. Calling it will not optimize the complete tree, but only a (random) part of it
        /// </summary>
        /// <param name="grandchildTricks"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle Optimize(int leafSwaps = 32, int grandchildTricks = 16, JobHandle dependsOn = default)
        {
            var optimizeJob = new OptimizeJob()
            {
                leafSwaps = leafSwaps,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                freeNodes = this.freeNodes,
                grandchildTricks = grandchildTricks,
                leafToNodeMap = this.leafToNodeMap,
                leaves = this.leaves,
                maxChildren = this.maxChildren,
                nodes = this.nodes,
                rndRef = this.rnd,
            };

            var optimizeHandle = optimizeJob.Schedule(dependsOn);
            return optimizeHandle;
        }



        public JobHandle GetSpheresInBounds(Bounds bounds, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var spheresInBoundsJob = new GetSpheresInBoundsJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result,
                root = this.root,
                searchBounds = bounds,
            };

            return spheresInBoundsJob.Schedule(dependsOn);
        }

        public JobHandle GetOverlappingSpheresInBounds(Bounds bounds, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var spheresInBoundsJob = new GetOverlappingSpheresInBoundsJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result,
                root = this.root,
                searchBounds = bounds,
            };

            return spheresInBoundsJob.Schedule(dependsOn);
        }


        public JobHandle GetSpheresInMultipleBounds(NativeArray<Bounds> bounds, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var spheresInMultipleBoundsJob = new GetSpheresInMultipleBoundsJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result.AsParallelWriter(),
                root = this.root,
                searchBoundaries = bounds
            };

            return spheresInMultipleBoundsJob.Schedule(bounds.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetOverlappingSpheresInMultipleBounds(NativeArray<Bounds> bounds, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var spheresInMultipleBoundsJob = new GetOverlappingSpheresInMultipleBoundsJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result.AsParallelWriter(),
                root = this.root,
                searchBoundaries = bounds
            };

            return spheresInMultipleBoundsJob.Schedule(bounds.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetSpheresInRadius(float3 center, float radius, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var spheresInRadiusJob = new GetSpheresInRadiusJob()
            {
                center = center,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                radius = radius,
                result = result,
                root = this.root
            };
            return spheresInRadiusJob.Schedule(dependsOn);
        }



        public JobHandle GetSpheresInRadii(NativeArray<float3> centers, NativeArray<float> radii, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var spheresInRadiiJob = new GetSpheresInRadiiJob()
            {
                centers = centers,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                radii = radii,
                result = result.AsParallelWriter(),
                root = this.root,
            };

            return spheresInRadiiJob.Schedule(radii.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetOverlappingSpheresInRadius(float3 center, float radius, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var spheresInRadiusJob = new GetOverlappingSpheresInRadiusJob()
            {
                center = center,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                radius = radius,
                result = result,
                root = this.root
            };
            return spheresInRadiusJob.Schedule(dependsOn);
        }


        public JobHandle GetOverlappingSpheresInRadii(NativeArray<float3> centers, NativeArray<float> radii, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var spheresInRadiiJob = new GetOverlappingSpheresInRadiiJob()
            {
                centers = centers,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                radii = radii,
                result = result.AsParallelWriter(),
                root = this.root,
            };

            return spheresInRadiiJob.Schedule(radii.Length, innerBatchLoopCount, dependsOn);
        }

        public JobHandle GetNearestNeighbors(NativeArray<float3> queryPoints, ref NativeArray<T> nearestNeighbors, JobHandle dependsOn = default)
        {
            var nearestNeighborJob = new GetNearestNeighborsJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                queryPoints = queryPoints,
                result = nearestNeighbors,
                root = this.root,
            };
            return nearestNeighborJob.Schedule(dependsOn);
        }

        /// <summary>
        /// Returns all spheres hit by the ray and their intersections within the result list sorted by the smallest hit distances
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <param name="result"></param>
        /// <param name="epsilon">Defines how accurately the hit distances are sorted</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle Raycast(Ray ray, float distance, ref NativeList<IntersectionHit3D<T>> result, float epsilon = 10e-5f, JobHandle dependsOn = default)
        {
            var comparer = new IntersectionHit3D<T>.RayComparer()
            {
                rayOrigin = ray.origin,
                epsilon = epsilon,
            };

            var raycastJob = new RaycastJob()
            {
                comparer = comparer,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                distance = distance,
                nodes = this.nodes,
                ray = ray,
                result = result,
                root = this.root,
            };

            return raycastJob.Schedule(dependsOn);
        }

        /// <summary>
        /// Returns all spheres that overlap with a given capsule in the tree. This is different from the classical / PhysX SphereCast as
        /// it does not return intersections or sorts the result - it simply returns all spheres within the casted spheres corridor (defined by a capsule)
        /// </summary>
        /// <param name="capsule"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle SphereCast(Capsule capsule, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var sphereCastJob = new SphereCastJob()
            {
                capsule = capsule,
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result,
                root = this.root
            };

            return sphereCastJob.Schedule(dependsOn);
        }

        /// <summary>
        /// Returns all entries that are within a frustum (stretched box - camera view)
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="result"></param>
        /// <param name="allocator"></param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle FrustumQuery(Camera camera, ref NativeList<T> result, 
            Allocator allocator = Allocator.TempJob, JobHandle dependsOn = default)
        {
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            var planes = new NativeArray<Plane>(frustumPlanes, allocator);

            var frustumJob = new FrustumJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                frustumPlanes = planes,
                nodes = this.nodes,
                result = result,
                root = this.root,
            };

            return frustumJob.Schedule(dependsOn);
        }

        public void Dispose()
        {
            this.nodes.DisposeIfCreated();
            this.data.DisposeIfCreated();
            this.childrenBuffer.DisposeIfCreated();

            this.freeChildrenIndices.DisposeIfCreated();

            this.leaves.DisposeIfCreated();
            this.leafToNodeMap.DisposeIfCreated();
            this.rnd.DisposeIfCreated();

            this.freeNodes.DisposeIfCreated();

            this.isCreated = false;
        }

        public void DisposeIfCreated()
        {
            if (this.isCreated) this.Dispose();
        }
    }
}
