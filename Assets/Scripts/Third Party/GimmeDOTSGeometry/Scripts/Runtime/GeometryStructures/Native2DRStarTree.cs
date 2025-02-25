using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public unsafe partial struct Native2DRStarTree<T> : IDisposable
        where T : unmanaged, IBoundingRect, IIdentifiable, IEquatable<T>
    {
        #region Private Variables

        private bool isCreated;

        private int maxChildren;
        private int root;
        private NativeReference<int> freeNodes;

        private NativeList<RStarNode2D> nodes;
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
        /// <param name="maxChildren">At most 30 children are possible for each partition / rectangle.
        /// Default value is a good balance. Consider changing the FixedListBytes-size in
        /// the RStarNode struct for your use-case and distributions.</param>
        public Native2DRStarTree(int capacity, Allocator allocator, int maxChildren = 16)
        {
            this.nodes = new NativeList<RStarNode2D>(capacity, allocator);
            this.data = new NativeParallelHashMap<int, T>(capacity, allocator);
            this.childrenBuffer = new NativeList<FixedList128Bytes<int>>(1, allocator);

            this.freeChildrenIndices = new NativeList<int>(allocator);
            this.leaves = new NativeParallelHashSet<int>(1, allocator);

            this.leafToNodeMap = new NativeParallelHashMap<int, int>(capacity, allocator);

            this.maxChildren = maxChildren;
            this.root = 0;
            this.freeNodes = new NativeReference<int>(allocator);

            this.nodes.Add(new RStarNode2D()
            {
                Bounds = new Rect(0, 0, 0, 0),
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

        //https://infolab.usc.edu/csci599/Fall2001/paper/rstar-tree.pdf
        //I chose the Guttman Heuristic as it seems to be simple enough.
        //Not mentioned in this specific paper (but maybe in others): If a rectanlge encloses
        //all other rectangles, the seeds are the largest (the enclosing) and the smallest rectangle

        //Generally speaking it is not too important as the dynamic heurstic
        //will clean up later anyway, it is just a way to have it work ~ok~ in the
        //first few frames after insertion
        private void GuttmanSplit(int childrenCount, FixedList128Bytes<int> childrenList,
            ref FixedList128Bytes<int> left, ref FixedList128Bytes<int> right,
            out Rect combinedRectLeft, out Rect combinedRectRight)
        {

            //PickSeeds
            int seed0 = -1, seed1 = -1;
            float largestAreaIncrease = float.NegativeInfinity;
            for (int i = 0; i < childrenCount; i++)
            {
                int childIndexA = childrenList[i];
                var childA = this.data[childIndexA];

                float areaA = childA.Bounds.Area();

                for (int j = i + 1; j < childrenCount; j++)
                {
                    int childIndexB = childrenList[j];
                    var childB = this.data[childIndexB];

                    var combinedRect = childA.Bounds.CombineWith(childB.Bounds);
                    float areaB = childB.Bounds.Area();

                    float combinedArea = combinedRect.Area();
                    float overlapArea = childA.Bounds.OverlapArea(childB.Bounds);
                    float areaIncrease = combinedArea - areaA - areaB + overlapArea;
                    
                    if (areaIncrease > largestAreaIncrease)
                    {
                        seed0 = i;
                        seed1 = j;
                        largestAreaIncrease = areaIncrease;
                    }
                }
            }

            int seedAIndex = childrenList[seed0];
            int seedBIndex = childrenList[seed1];
            T seedA = this.data[seedAIndex];
            T seedB = this.data[seedBIndex];

            Rect groupA = seedA.Bounds;
            Rect groupB = seedB.Bounds;

            NativeBitArray marked = new NativeBitArray(childrenCount, Allocator.TempJob);
            marked.Set(seed0, true);
            marked.Set(seed1, true);

            left.Add(seedAIndex);
            right.Add(seedBIndex);

            for (int i = 0; i < childrenCount - 2; i++)
            {
                float areaA = groupA.Area();
                float areaB = groupB.Area();

                //Pick Next
                float maxIncreaseDiff = float.NegativeInfinity;
                int nextEntry = -1;
                for (int j = 0; j < childrenCount; j++)
                {
                    if (!marked.IsSet(j))
                    {
                        int childIndex = childrenList[j];
                        var child = this.data[childIndex];

                        Rect combinedA = groupA.CombineWith(child.Bounds);
                        Rect combinedB = groupB.CombineWith(child.Bounds);

                        float overlapAreaA = groupA.OverlapArea(child.Bounds);
                        float overlapAreaB = groupB.OverlapArea(child.Bounds);

                        float increaseA = combinedA.Area() - areaA - child.Bounds.Area() + overlapAreaA;
                        float increaseB = combinedB.Area() - areaB - child.Bounds.Area() + overlapAreaB;

                        float diff = math.abs(increaseA - increaseB);

                        if (diff > maxIncreaseDiff)
                        {
                            nextEntry = j;
                            maxIncreaseDiff = diff;
                        }
                    }
                }

                var nextEntryIdx = childrenList[nextEntry];
                var nextChild = this.data[nextEntryIdx];

                Rect nextCombinedA = groupA.CombineWith(nextChild.Bounds);
                Rect nextCombinedB = groupB.CombineWith(nextChild.Bounds);

                float nextOverlapAreaA = groupA.OverlapArea(nextChild.Bounds);
                float nextOverlapAreaB = groupB.OverlapArea(nextChild.Bounds);

                float nextIncreaseA = nextCombinedA.Area() - areaA - nextChild.Bounds.Area() + nextOverlapAreaA;
                float nextIncreaseB = nextCombinedB.Area() - areaB - nextChild.Bounds.Area() + nextOverlapAreaB;

                if (nextIncreaseA < nextIncreaseB)
                {
                    groupA = nextCombinedA;
                    left.Add(nextEntryIdx);
                } else
                {
                    groupB = nextCombinedB;
                    right.Add(nextEntryIdx);
                }
                marked.Set(nextEntry, true);
            }

            combinedRectLeft = groupA;
            combinedRectRight = groupB;

            marked.Dispose();
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

        private RStarNode2D CreateSplitNode(int parent, int childrenIndex, Rect combinedBounds)
        {
            return new RStarNode2D()
            {
                Bounds = combinedBounds,
                children = childrenIndex,
                left = -1,
                right = -1,
                parent = parent
            };
        }

        private int InsertIntoNodesList(RStarNode2D node)
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

        private void InsertIntoNode(RStarNode2D node, int nodeIdx, T value)
        {
            var childrenList = this.childrenBuffer[node.children];
            int childrenCount = childrenList.Length;

            childrenList.Add(value.ID);
            if(childrenCount < this.maxChildren)
            {
                childrenCount++;

                if(childrenCount == 1)
                {
                    node.Bounds = value.Bounds;
                }
                else
                {
                    float xMin = value.Bounds.xMin;
                    float xMax = value.Bounds.xMax;

                    float yMin = value.Bounds.yMin;
                    float yMax = value.Bounds.yMax; 

                    for(int i = 0; i < childrenCount; i++)
                    {
                        int childIndex = childrenList[i];
                        T child = this.data[childIndex];

                        xMin = math.min(child.Bounds.xMin, xMin);
                        xMax = math.max(child.Bounds.xMax, xMax);

                        yMin = math.min(child.Bounds.yMin, yMin);
                        yMax = math.max(child.Bounds.yMax, yMax);
                    }

                    node.Bounds = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                }

                this.leafToNodeMap.Add(value.ID, nodeIdx);
                this.nodes[nodeIdx] = node;
                this.childrenBuffer[node.children] = childrenList;
            } else
            {
                childrenCount++;

                //Split
                FixedList128Bytes<int> left = new FixedList128Bytes<int>();
                FixedList128Bytes<int> right = new FixedList128Bytes<int>();
                this.GuttmanSplit(childrenCount, childrenList, 
                    ref left, ref right, 
                    out Rect combinedLeft, out Rect combinedRight);

                this.freeChildrenIndices.Add(node.children);

                int leftChildrenIdx = this.InsertIntoChildrenList(ref left);
                int rightChildrenIdx = this.InsertIntoChildrenList(ref right);

                var leftNode = this.CreateSplitNode(nodeIdx, leftChildrenIdx, combinedLeft);
                var rightNode = this.CreateSplitNode(nodeIdx, rightChildrenIdx, combinedRight);

                int leftNodeIdx = this.InsertIntoNodesList(leftNode);
                int rightNodeIdx = this.InsertIntoNodesList(rightNode);

                for(int i = 0; i < left.Length; i++)
                {
                    int childIndex = left[i];
                    var child = this.data[childIndex];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, leftNodeIdx);
                }

                for(int i = 0; i < right.Length; i++)
                {
                    int childIndex = right[i];
                    var child = this.data[childIndex];
                    this.leafToNodeMap.Remove(child.ID);
                    this.leafToNodeMap.Add(child.ID, rightNodeIdx);
                }

                this.leaves.Remove(nodeIdx);
                this.leaves.Add(leftNodeIdx);
                this.leaves.Add(rightNodeIdx);

                var allChildrenRect = combinedLeft.CombineWith(combinedRight);

                node.Bounds = allChildrenRect;
                node.left = leftNodeIdx;
                node.right = rightNodeIdx;
                node.children = -1;

                this.nodes[nodeIdx] = node;
            }
        }

        private void InsertIntoDataList(T value)
        {
            if(!this.data.ContainsKey(value.ID))
            {
                this.data.Add(value.ID, value);
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
                int currentHeight = 0;

                float2 center = value.Bounds.center;
                while(node.children < 0)
                {
                    int leftNodeIdx = node.left;
                    int rightNodeIdx = node.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIdx];


                    float distLeft = math.distancesq(leftNode.Bounds.center, center);
                    float distRight = math.distancesq(rightNode.Bounds.center, center);

                    if(distLeft < distRight)
                    {
                        node = leftNode;
                        nodeIdx = leftNodeIdx;
                    } else
                    {
                        node = rightNode;
                        nodeIdx = rightNodeIdx;
                    }
                    currentHeight++;
                }
                this.InsertIntoNode(node, nodeIdx, value);
            }
        }

        private static void GetSibling(NativeList<RStarNode2D> nodes, RStarNode2D parentNode, int nodeIdx, out RStarNode2D sibling, out int siblingIdx)
        {
            if (parentNode.left == nodeIdx)
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

        public RStarNode2D GetNode(int index)
        {
            return this.nodes[index];
        }

        public RStarNode2D* GetRoot()
        {
            if(this.nodes.IsCreated)
            {
                return (RStarNode2D*)this.nodes.GetUnsafePtr();
            } else
            {
                return null;
            }
        }

        public void GetLeafChildren(RStarNode2D leaf, ref NativeList<T> result)
        {
            result.Clear();
            if (leaf.left < 0)
            {
                var children = this.childrenBuffer[leaf.children];

                for (int i = 0; i < children.Length; i++)
                {
                    int childIndex = children[i];
                    var child = this.data[childIndex];
                    result.Add(child);
                }

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

        private void CombineSiblings(RStarNode2D parentNode, int parentNodeIdx, RStarNode2D node, int nodeIdx, RStarNode2D sibling, int siblingIdx)
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
            public NativeList<T> values;

            [NoAlias]
            public Native2DRStarTree<T> tree;

            public void Execute()
            {
                for(int i = 0; i < this.values.Length; i++)
                {
                    this.tree.Remove(this.values[i]);
                }
            }
        }

        public JobHandle RemoveAll(NativeList<T> values, JobHandle dependsOn = default)
        {
            var removeAllJob = new RemoveJob()
            {
                tree = this,
                values = values,
            };
            return removeAllJob.Schedule(dependsOn);
        }

        public void Remove(T value)
        {
            if(this.data.ContainsKey(value.ID))
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
        
        private static void UpdateValue(T value, int nodeIdx,
            NativeList<RStarNode2D> nodes, NativeParallelHashMap<int, T> data)
        {
            var node = nodes[nodeIdx];
            var combinedRect = node.Bounds.CombineWith(value.Bounds);

            float prevArea = node.Bounds.Area();
            node.Bounds = combinedRect;
            nodes[nodeIdx] = node;

            bool rectWasIncreased = combinedRect.Area() > prevArea;

            data[value.ID] = value;

            if(rectWasIncreased)
            {
                var parentNodeIdx = node.parent;
                while(parentNodeIdx >= 0)
                {
                    var parentNode = nodes[parentNodeIdx];

                    int leftNodeIdx = parentNode.left;
                    int rightNodeIdx = parentNode.right;

                    var leftNode = nodes[leftNodeIdx];
                    var rightNode = nodes[rightNodeIdx];

                    combinedRect = leftNode.Bounds.CombineWith(rightNode.Bounds);

                    parentNode.Bounds = combinedRect;
                    nodes[parentNodeIdx] = parentNode;

                    parentNodeIdx = parentNode.parent;
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

        [BurstCompile]
        public struct UpdateJob : IJob
        {
            [ReadOnly, NoAlias]
            public NativeList<T> values;

            [ReadOnly, NoAlias]
            public NativeParallelHashMap<int, int> leafToNodeMap;

            [NoAlias]
            public NativeList<RStarNode2D> nodes;

            [WriteOnly, NoAlias]
            public NativeParallelHashMap<int, T> data;

            [ReadOnly, NoAlias]
            public NativeParallelHashSet<int> leaves;

            private void UpdateValue(T value, RStarNode2D* nodePtr, int nodeIdx)
            {
                var node = nodePtr + nodeIdx;
                node->Bounds = node->Bounds.CombineWith(value.Bounds);
            }

            public void Execute()
            {
                RStarNode2D* nodePtr = (RStarNode2D*)this.nodes.GetUnsafePtr();

                for(int i = 0; i < this.values.Length; i++)
                {
                    var value = this.values[i];
                    int nodeIdx = this.leafToNodeMap[value.ID];
                    this.UpdateValue(value, nodePtr, nodeIdx);
                    this.data[value.ID] = value;
                }

                foreach(var leafIdx in this.leaves)
                {
                    var leafNode = nodePtr + leafIdx;

                    var parentNodeIdx = leafNode->parent;
                    while(parentNodeIdx >= 0)
                    {
                        var parentNode = nodePtr + parentNodeIdx;

                        var leftNode = nodePtr + parentNode->left;
                        var rightNode = nodePtr + parentNode->right;

                        var combinedRect = leftNode->Bounds.CombineWith(rightNode->Bounds);

                        if(parentNode->Bounds == combinedRect)
                        {
                            //Early Quit
                            break;
                        }

                        parentNode->Bounds = combinedRect;
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
                nodes = this.nodes,
                values = values,
                leaves = this.leaves,
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
            public NativeList<RStarNode2D> nodes;

            [NoAlias]
            public NativeList<FixedList128Bytes<int>> childrenBuffer;

            [NoAlias]
            public NativeParallelHashSet<int> leaves;

            //We want to continue the state of the random values instead of always using the same ones, therefore we use a reference
            [NoAlias]
            public NativeReference<Unity.Mathematics.Random> rndRef;

            [NoAlias]
            public NativeParallelHashMap<int, int> leafToNodeMap;


            private void UpdateNodeBounds(ref RStarNode2D node)
            {
                var childrenList = this.childrenBuffer[node.children];
                int count = childrenList.Length;

                var firstChildIndex = childrenList[0];
                var firstChild = this.data[firstChildIndex];
                var combinedBounds = firstChild.Bounds;
                for (int i = 1; i < count; i++)
                {
                    int childIndex = childrenList[i];
                    var child = this.data[childIndex];
                    combinedBounds = combinedBounds.CombineWith(child.Bounds);
                }
                node.Bounds = combinedBounds;
            }
            

            private void StealChildren(int nodeIdxA, RStarNode2D nodeA, RStarNode2D nodeB)
            {
                var childrenA = this.childrenBuffer[nodeA.children];
                var childrenB = this.childrenBuffer[nodeB.children];

                int nrOfChildrenA = childrenA.Length;
                int nrOfChildrenB = childrenB.Length;

                for(int i = 0; nrOfChildrenA < this.maxChildren && nrOfChildrenB > 2 && i < nrOfChildrenB; i++)
                {
                    var childIdx = childrenB[i];
                    var child = this.data[childIdx];

                    var combinedRectA = nodeA.Bounds.CombineWith(child.Bounds);

                    float distToA = math.distancesq(nodeA.Bounds.center, child.Bounds.center);
                    float distToB = math.distancesq(nodeB.Bounds.center, child.Bounds.center);

                    if(nodeA.Bounds == combinedRectA && distToA < distToB)
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

            private float MaxDistance(float2 point, Rect bounds)
            {
                float2 diff = math.max(math.abs(point - (float2)bounds.max), math.abs(point - (float2)bounds.min));
                return math.length(diff);
            }

            private void PrepareSortedDistances(RStarNode2D nodeA, RStarNode2D nodeB,
                ref NativeList<DistanceSortingIndex> listA, ref NativeList<DistanceSortingIndex> listB)
            {
                var childrenListA = this.childrenBuffer[nodeA.children];
                var childrenListB = this.childrenBuffer[nodeB.children];

                for(int i = 0; i < childrenListA.Length; i++)
                {
                    int childIndex = childrenListA[i];
                    var child = this.data[childIndex];

                    float dist = MaxDistance(nodeA.Bounds.center, child.Bounds);
                    listA.Add(new DistanceSortingIndex() { distance = dist, index = i });
                }

                for (int i = 0; i < childrenListB.Length; i++)
                {
                    int childIndex = childrenListB[i];
                    var child = this.data[childIndex];

                    float dist = MaxDistance(nodeB.Bounds.center, child.Bounds);
                    listB.Add(new DistanceSortingIndex() { distance = dist, index = i });
                }
            }

            private void UpdateNodeParents(RStarNode2D node)
            {
                var parentNodeIdx = node.parent;
                while(parentNodeIdx >= 0)
                {
                    var parentNode = this.nodes[parentNodeIdx];

                    var leftNodeIdx = parentNode.left;
                    var rightNodeIdx = parentNode.right;

                    var leftNode = this.nodes[leftNodeIdx];
                    var rightNode = this.nodes[rightNodeIdx];

                    var combinedRect = leftNode.Bounds.CombineWith(rightNode.Bounds);
                    if (parentNode.Bounds == combinedRect)
                    {
                        //Early Quit
                        break;
                    }

                    parentNode.Bounds = combinedRect;
                    this.nodes[parentNodeIdx] = parentNode;

                    parentNodeIdx = parentNode.parent;

                }
            }

            private void LeafSwap(RStarNode2D nodeA, RStarNode2D nodeB, int nodeIdxA, int nodeIdxB,
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

                            float swapDistA = MaxDistance(nodeA.Bounds.center, maxChildB.Bounds);
                            float swapDistB = MaxDistance(nodeB.Bounds.center, maxChildA.Bounds);

                            if(swapDistA + swapDistB < listA[i].distance + listB[j].distance)
                            {
                                childrenA[idxA] = maxChildIdxB;
                                childrenB[idxB] = maxChildIdxA;

                                this.leafToNodeMap.Remove(maxChildIdxA);
                                this.leafToNodeMap.Remove(maxChildIdxB);

                                this.leafToNodeMap.Add(maxChildIdxA, nodeIdxB);
                                this.leafToNodeMap.Add(maxChildIdxB, nodeIdxA);

                                float oldDistA = listA[i].distance;
                                float oldDistB = listB[j].distance;

                                listA[i] = new DistanceSortingIndex() { distance = swapDistA, index = idxA };
                                listB[j] = new DistanceSortingIndex() { distance = swapDistB, index = idxB };

                                if(swapDistA > oldDistA)
                                {
                                    for (int k = i; k < listA.Length - 1; k++)
                                    {
                                        if (listA[k].distance < listA[k + 1].distance) break;

                                        var tmp = listA[k];
                                        listA[k] = listA[k + 1];
                                        listA[k + 1] = tmp;
                                    }
                                } else
                                {

                                    for (int k = i - 1; k >= 0; k--)
                                    {
                                        if (listA[k + 1].distance > listA[k].distance) break;

                                        var tmp = listA[k];
                                        listA[k] = listA[k + 1];
                                        listA[k + 1] = tmp;
                                    }
                                }

                                if (swapDistB > oldDistB)
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

            private void DoGrandChildTrick(RStarNode2D node, int nodeIdx)
            {
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

                            var thisRect = node.Bounds.CombineWith(sibling1.Bounds);
                            var otherRect = sibling0.Bounds.CombineWith(sibling2.Bounds);

                            bool swappedNodes = false;
                            if (thisRect.Area() + otherRect.Area() < parent0Node.Bounds.Area() + parent1Node.Bounds.Area())
                            {
                                parent0Node.Bounds = thisRect;
                                parent0Node.left = nodeIdx;
                                parent0Node.right = sibling1Idx;

                                parent1Node.Bounds = otherRect;
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
                                thisRect = node.Bounds.CombineWith(sibling2.Bounds);
                                otherRect = sibling0.Bounds.CombineWith(sibling1.Bounds);

                                if (thisRect.Area() + otherRect.Area() < parent0Node.Bounds.Area() + parent1Node.Bounds.Area())
                                {
                                    parent0Node.Bounds = thisRect;
                                    parent0Node.left = nodeIdx;
                                    parent0Node.right = sibling2Idx;

                                    parent1Node.Bounds = otherRect;
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
                            var newRect = node.Bounds.CombineWith(parent1Node.Bounds);

                            if (sibling0.Bounds.Area() + newRect.Area() < parent0Node.Bounds.Area() + parent1Node.Bounds.Area())
                            {
                                parent0Node.Bounds = newRect;
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

            public void Execute()
            {
                var rnd = this.rndRef.Value;

                if(this.nodes.Length > 1)
                {
                    var leavesArray = this.leaves.ToNativeArray(Allocator.Temp);

                    if(leavesArray.Length > 1)
                    {
                        for(int i = 0; i < this.leafSwaps; i++)
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

                            if(leaf0.Bounds.Overlaps(leaf1.Bounds))
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

                    if(this.nodes.Length >= 7)
                    {
                        for(int i = 0; i < this.grandchildTricks; i++)
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

        public JobHandle Optimize(int leafSwaps = 32, int grandchildTricks = 16, JobHandle dependsOn = default)
        {
            var optimizeJob = new OptimizeJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                freeNodes = this.freeNodes,
                grandchildTricks = grandchildTricks,
                leafSwaps = leafSwaps,
                leafToNodeMap = this.leafToNodeMap,
                leaves = this.leaves,
                maxChildren = this.maxChildren,
                nodes = this.nodes,
                rndRef = this.rnd,
            };

            var optimizeHandle = optimizeJob.Schedule(dependsOn);
            return optimizeHandle;
        }



        public JobHandle GetRectanglesInRectangle(Rect rect, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var rectanglesInRectangleJob = new GetRectanglesInRectangleJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result,
                root = this.root,
                searchRect = rect,
            };
            return rectanglesInRectangleJob.Schedule(dependsOn);
        }

        public JobHandle GetRectanglesInRectangles(NativeArray<Rect> rectangles, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var rectanglesInRectanglesJob = new GetRectanglesInRectanglesJob()
            {
                data = this.data,
                nodes = this.nodes,
                childrenBuffer = this.childrenBuffer,
                root = this.root,
                result = result.AsParallelWriter(),
                searchRectangles = rectangles,
            };

            return rectanglesInRectanglesJob.Schedule(rectangles.Length, innerBatchLoopCount, dependsOn);
        }


        public JobHandle GetOverlappingRectanglesInRectangle(Rect rect, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var rectanglesInRectangleJob = new GetOverlappingRectanglesInRectangleJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                result = result,
                root = this.root,
                searchRect = rect,
            };
            return rectanglesInRectangleJob.Schedule(dependsOn);
        }


        public JobHandle GetOverlappingRectanglesInRectangles(NativeArray<Rect> rectangles, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var rectanglesInRectanglesJob = new GetOverlappingRectanglesInRectanglesJob()
            {
                data = this.data,
                nodes = this.nodes,
                childrenBuffer = this.childrenBuffer,
                root = this.root,
                result = result.AsParallelWriter(),
                searchRectangles = rectangles,
            };

            return rectanglesInRectanglesJob.Schedule(rectangles.Length, innerBatchLoopCount, dependsOn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="result"></param>
        /// <param name="dependsOn"></param>
        /// <returns>All circles that are entirely within the radius (overlapping does not count)</returns>
        public JobHandle GetRectanglesInRadius(float2 center, float radius, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var rectanglesInRadiusJob = new GetRectanglesInRadiusJob()
            {
                radius = radius,
                center = center,
                data = this.data,
                nodes = this.nodes,
                childrenBuffer = this.childrenBuffer,
                root = this.root,
                result = result
            };

            return rectanglesInRadiusJob.Schedule(dependsOn);
        }


        public JobHandle GetRectanglesInRadii(NativeArray<float2> centers, NativeArray<float> radii, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var rectanglesInRadiiJob = new GetRectanglesInRadiiJob()
            {
                radii = radii,
                centers = centers,
                data = this.data,
                nodes = this.nodes,
                childrenBuffer = this.childrenBuffer,
                root = this.root,
                result = result.AsParallelWriter()
            };

            return rectanglesInRadiiJob.Schedule(radii.Length, innerBatchLoopCount, dependsOn);
        }


        public JobHandle GetOverlappingRectanglesInRadius(float2 center, float radius, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var rectanglesInRadiusJob = new GetOverlappingRectanglesInRadiusJob()
            {
                radius = radius,
                center = center,
                data = this.data,
                nodes = this.nodes,
                childrenBuffer = this.childrenBuffer,
                root = this.root,
                result = result
            };

            return rectanglesInRadiusJob.Schedule(dependsOn);
        }

        public JobHandle GetOverlappingRectanglesInRadii(NativeArray<float2> centers, NativeArray<float> radii, ref NativeParallelHashSet<T> result,
            JobHandle dependsOn = default, int innerBatchLoopCount = 1)
        {
            result.Clear();

            var rectanglesInRadiiJob = new GetOverlappingRectanglesInRadiiJob()
            {
                radii = radii,
                centers = centers,
                data = this.data,
                nodes = this.nodes,
                childrenBuffer = this.childrenBuffer,
                root = this.root,
                result = result.AsParallelWriter()
            };

            return rectanglesInRadiiJob.Schedule(radii.Length, innerBatchLoopCount, dependsOn);
        }

        /// <summary>
        /// Returns all circles hit by the ray and their intersections within the result list sorted by the smallest hit distances
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <param name="result"></param>
        /// <param name="epsilon">Defines how accurately the hit distances are sorted</param>
        /// <param name="dependsOn"></param>
        /// <returns></returns>
        public JobHandle Raycast(Ray2D ray, float distance, ref NativeList<IntersectionHit2D<T>> result, float epsilon = 10e-5f, JobHandle dependsOn = default)
        {
            var comparer = new IntersectionHit2D<T>.RayComparer()
            {
                rayOrigin = ray.origin,
                epsilon = epsilon,
            };

            var raycastJob = new RaycastJob()
            {
                comparer = comparer,
                distance = distance,
                ray = ray,
                nodes = this.nodes,
                result = result,
                root = this.root,
                childrenBuffer = this.childrenBuffer,
                data = this.data
            };

            return raycastJob.Schedule(dependsOn);
        }

        public JobHandle GetNearestNeighbors(NativeArray<float3> queryPoints, ref NativeArray<T> nearestNeighbors, CardinalPlane plane = CardinalPlane.XZ, JobHandle dependsOn = default)
        {
            var nearestNeighborJob = new GetNearestNeighborsJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                queryPoints = queryPoints,
                result = nearestNeighbors,
                root = this.root,
                plane = plane
            };

            return nearestNeighborJob.Schedule(dependsOn);
        }

        public JobHandle GetRectanglesInPolygon(NativePolygon2D polygon, Matrix4x4 trs, ref NativeList<T> result, JobHandle dependsOn = default)
        {
            var job = new GetRectanglesInPolygonJob()
            {
                childrenBuffer = this.childrenBuffer,
                data = this.data,
                nodes = this.nodes,
                polygon = polygon,
                result = result,
                root = this.root,
                trs = trs,
            };
            return job.Schedule(dependsOn);
        }


        public void Clear()
        {
            this.data.Clear();
            this.nodes.Clear();
            this.childrenBuffer.Clear();

            this.freeChildrenIndices.Clear();

            this.leafToNodeMap.Clear();
            this.leaves.Clear();

            this.root = 0;

            this.nodes.Add(new RStarNode2D()
            {
                Bounds = new Rect(0, 0, 0, 0),
                children = 0,
                left = -1,
                right = -1,
            });
            this.leaves.Add(this.root);
            this.childrenBuffer.Add(new FixedList128Bytes<int>());
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
