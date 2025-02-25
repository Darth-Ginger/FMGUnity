using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Implementation of an AVL Tree to be used in Unity's Job System.
    /// <para> </para>
    /// <para>-Insertion: O(log(n))</para>
    /// <para>-Removing: O(log(n))</para>
    /// <para>-Search: O(log(n))</para>
    /// <para> </para>
    /// Internally, the nodes are stored in a NativeArray<T>, where each node stores
    /// pointers to its children and its parent. On removing, a NativeList stores
    /// the freed position, which is then filled as soon as a new element is inserted
    /// to save space
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U">A comparison struct used to compare the inserted elements with each other</typeparam>
    public unsafe struct NativeAVLTree<T, U> : IBalancedSearchTree<T>, IDisposable where T : unmanaged where U : struct, IComparer<T>
    {

        public struct TreeNode : IBalancedSearchTreeNode<T>
        {
            public int Parent { get; set; }
            public int Left { get; set; }
            public int Right { get; set; }

            public T Value { get; set; }

            public sbyte Balance { get; set; }
        }

        #region Public Variables

        public U comparer;

        #endregion

        #region Private Variables

        [NoAlias]
        private NativeList<TreeNode> elements;

        //Using a list as a stack is way faster than a NativeQueue, because of the internal allocations the queue makes all the time
        [NoAlias]
        private NativeList<int> free;

        private int root;

        #endregion

        public int Length { get => this.elements.Length; set => this.elements.Length = value; }

        public TreeNode* Root => this.root >= 0 ? (TreeNode*)this.elements.GetUnsafePtr() + this.root : null;

        public int RootIdx => this.root;

        public NativeList<TreeNode> Elements => this.elements;


        public NativeAVLTree(U comparer, Allocator allocator)
        {
            this.comparer = comparer;
            this.elements = new NativeList<TreeNode>(1, allocator);
            this.free = new NativeList<int>(allocator);
            this.root = -1;
        }

        public bool IsCreated => this.elements.IsCreated || this.free.IsCreated;

        public void Dispose()
        {
            this.elements.DisposeIfCreated();
            this.free.DisposeIfCreated();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The index of the leftmost node of the tree. If the tree is empty, -1 is returned</returns>
        public int GetLeftmostNode()
        {
            var node = this.RootIdx;
            if(node >= 0)
            {
                var elem = this.elements[node];
                while(elem.Left >= 0)
                {
                    node = elem.Left;
                    elem = this.elements[node];
                }
            }
            return node;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The index of the rightmost node of the tree. If the tree is empty, -1 is returned</returns>
        public int GetRightmostNode()
        {
            var node = this.RootIdx;
            if (node >= 0)
            {
                var elem = this.elements[node];
                while (elem.Right >= 0)
                {
                    node = elem.Right;
                    elem = this.elements[node];
                }
            }
            return node;
        }

        private void GetAllTreeElementsRecursion(ref NativeList<int> nodeIndices, TreeTraversal traversal, int currentNode)
        {
            var elem = this.Elements[currentNode];
            switch(traversal)
            {
                case TreeTraversal.PREORDER:

                    nodeIndices.Add(currentNode);
                    if(elem.Left >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Left);
                    }
                    if(elem.Right >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Right);
                    }
                    break;
                case TreeTraversal.POSTORDER:
                    if(elem.Left >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Left);
                    }
                    if(elem.Right >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Right);
                    }
                    nodeIndices.Add(currentNode);
                    break;
                case TreeTraversal.INORDER:
                    if(elem.Left >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Left);
                    }
                    nodeIndices.Add(currentNode);
                    if(elem.Right >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Right);
                    }
                    break;
            }
        }

        /// <summary>
        /// Visits all nodes of the tree and stores the indices into a list, sorted depending
        /// on the tree traversal.
        /// </summary>
        /// <param name="nodeIndices">The list to store the indices into</param>
        /// <param name="traversal">The way to recursively visit each node. <see href="https://en.wikipedia.org/wiki/Tree_traversal">Wiki</see>/></param>
        public void GetAllTreeElements(ref NativeList<int> nodeIndices, TreeTraversal traversal)
        {
            if(this.root >= 0)
            {
                this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, this.root);
            }
        }

        private void GetHeightRecursive(int idx, int currentHeight, ref int maxHeight)
        {
            currentHeight++;
            if(currentHeight > maxHeight)
            {
                maxHeight = currentHeight;
            }

            var element = this.Elements[idx];
            if(element.Left >= 0)
            {
                this.GetHeightRecursive(element.Left, currentHeight, ref maxHeight);
            }

            if(element.Right >= 0)
            {
                this.GetHeightRecursive(element.Right, currentHeight, ref maxHeight);
            }
        }

        //Note: This operation is O(n)
        /// <summary>
        /// 
        /// </summary>
        /// <returns>The maximum height of the tree</returns>
        public int GetHeight()
        {
            if(this.root >= 0)
            {
                int height = 0;
                this.GetHeightRecursive(this.root, height, ref height);
                return height;
            }
            return 0;
        }

        /// <summary>
        /// Return the node that is immediately to the left of the given node. In other words,
        /// the first node with a "smaller" value than the given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public int GetPredecessor(int nodeIdx)
        {
            if (nodeIdx < 0) return nodeIdx;

            var basePtr = (TreeNode*)this.elements.GetUnsafePtr();
            var elem = basePtr + nodeIdx;
            if (elem->Left >= 0)
            {
                nodeIdx = elem->Left;
                elem = basePtr + nodeIdx;
                while (elem->Right >= 0)
                {
                    nodeIdx = elem->Right;
                    elem = basePtr + nodeIdx;
                }
                return nodeIdx;
            }
            int parentIdx = elem->Parent;
            while (parentIdx >= 0 && (basePtr + parentIdx)->Left == nodeIdx)
            {
                nodeIdx = parentIdx;
                parentIdx = elem->Parent;
            }
            return parentIdx;
        }

        /// <summary>
        /// Return the node that is immediately to the right of the given node. In other words,
        /// the first node with a "greater" value than the given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public int GetSuccessor(int nodeIdx)
        {
            if (nodeIdx < 0) return nodeIdx;

            var basePtr = (TreeNode*)this.elements.GetUnsafePtr();
            var elem = basePtr + nodeIdx;
            if (elem->Right >= 0)
            {
                nodeIdx = elem->Right;
                elem = basePtr + nodeIdx;
                while (elem->Left >= 0)
                {
                    nodeIdx = elem->Left;
                    elem = basePtr + nodeIdx;
                }
                return nodeIdx;
            }
            int parentIdx = elem->Parent;
            while (parentIdx >= 0 && (basePtr + parentIdx)->Right == nodeIdx)
            {
                nodeIdx = parentIdx;
                parentIdx = elem->Parent;
            }
            return parentIdx;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns>The index of the node where the value is stored</returns>
        public int GetElementIdx(T value) => this.Search(value);

        private int Search(T value)
        {
            int cmp;

            var basePtr = (TreeNode*)this.elements.GetUnsafePtr();
            var nodePtr = basePtr + this.root;
            int node = this.root;

            while(node >= 0 && (cmp = this.comparer.Compare(value, nodePtr->Value)) != 0)
            {
                node = cmp < 0 ? nodePtr->Left : nodePtr->Right;
                nodePtr = basePtr + node;
            }
            return node;
        }


        public bool IsEmpty()
        {
            return this.root < 0;
        }

        public void Clear()
        {
            this.elements.Clear();
            this.free.Clear();
            this.root = -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns>True if the value is contained in the sorted tree (depending on the implementation of the comparer this
        /// might not always be the case)</returns>
        public bool Contains(T value)
        {
            return this.Search(value) >= 0;
        }

        private int RotateLeft(int parent, int child)
        {
            var childElem = this.elements[child];
            var parentElem = this.elements[parent];

            int leftGrandchild = childElem.Left;
            parentElem.Right = leftGrandchild;
            if(leftGrandchild >= 0)
            {
                var leftGrandchildElem = this.elements[leftGrandchild];
                leftGrandchildElem.Parent = parent;
                this.elements[leftGrandchild] = leftGrandchildElem;
            }
            childElem.Left = parent;
            parentElem.Parent = child;

            if(childElem.Balance == 0)
            {
                parentElem.Balance = 1;
                childElem.Balance = -1;
            } else
            {
                parentElem.Balance = 0;
                childElem.Balance = 0;
            }

            this.elements[child] = childElem;
            this.elements[parent] = parentElem;

            return child;
        }

        private int RotateRight(int parent, int child)
        {
            var childElem = this.elements[child];
            var parentElem = this.elements[parent];

            int rightGrandchild = childElem.Right;
            parentElem.Left = rightGrandchild;
            if(rightGrandchild >= 0)
            {
                var rightGrandchildElem = this.elements[rightGrandchild];
                rightGrandchildElem.Parent = parent;
                this.elements[rightGrandchild] = rightGrandchildElem;
            }
            childElem.Right = parent;
            parentElem.Parent = child;

            if(childElem.Balance == 0)
            {
                parentElem.Balance = -1;
                childElem.Balance = 1;
            } else
            {
                parentElem.Balance = 0;
                childElem.Balance = 0;
            }

            this.elements[child] = childElem;
            this.elements[parent] = parentElem;

            return child;
        }

        private int RotateRightLeft(int parent, int child)
        {
            var childElem = this.elements[child];
            var parentElem = this.elements[parent];

            int grandchild = childElem.Left;

            var grandchildElem = this.elements[grandchild];

            int rightGrandgrandchild = grandchildElem.Right;

            childElem.Left = rightGrandgrandchild;
            if(rightGrandgrandchild >= 0)
            {
                var rightGrandgrandchildElem = this.elements[rightGrandgrandchild];
                rightGrandgrandchildElem.Parent = child;
                this.elements[rightGrandgrandchild] = rightGrandgrandchildElem;
            }
            grandchildElem.Right = child;
            childElem.Parent = grandchild;

            int leftGrandgrandchild = grandchildElem.Left;
            parentElem.Right = leftGrandgrandchild;
            if(leftGrandgrandchild >= 0)
            {
                var leftGrandgrandchildElem = this.elements[leftGrandgrandchild];
                leftGrandgrandchildElem.Parent = parent;
                this.elements[leftGrandgrandchild] = leftGrandgrandchildElem;
            }
            grandchildElem.Left = parent;
            parentElem.Parent = grandchild;

            if(grandchildElem.Balance == 0)
            {
                parentElem.Balance = 0;
                childElem.Balance = 0;

            } else
            {
                if(grandchildElem.Balance > 0)
                {
                    parentElem.Balance = -1;
                    childElem.Balance = 0;
                } else
                {
                    parentElem.Balance = 0;
                    childElem.Balance = 1;
                }
            }
            grandchildElem.Balance = 0;

            this.elements[child] = childElem;
            this.elements[parent] = parentElem;
            this.elements[grandchild] = grandchildElem;

            return grandchild;
        }

        private int RotateLeftRight(int parent, int child)
        {
            var childElem = this.elements[child];
            var parentElem = this.elements[parent];

            int grandchild = childElem.Right;

            var grandchildElem = this.elements[grandchild];

            int leftGrandgrandchild = grandchildElem.Left;
            childElem.Right = leftGrandgrandchild;
            if (leftGrandgrandchild >= 0)
            {
                var leftGrandgrandchildElem = this.elements[leftGrandgrandchild];
                leftGrandgrandchildElem.Parent = child;
                this.elements[leftGrandgrandchild] = leftGrandgrandchildElem;
            }
            grandchildElem.Left = child;
            childElem.Parent = grandchild;

            int rightGrandgrandchild = grandchildElem.Right;
            parentElem.Left = rightGrandgrandchild;
            if (rightGrandgrandchild >= 0)
            {
                var rightGrandgrandchildElem = this.elements[rightGrandgrandchild];
                rightGrandgrandchildElem.Parent = parent;
                this.elements[rightGrandgrandchild] = rightGrandgrandchildElem;
            }
            grandchildElem.Right = parent;
            parentElem.Parent = grandchild;

            if (grandchildElem.Balance == 0)
            {
                parentElem.Balance = 0;
                childElem.Balance = 0;

            }
            else
            {
                if (grandchildElem.Balance < 0)
                {
                    parentElem.Balance = 1;
                    childElem.Balance = 0;
                }
                else
                {
                    parentElem.Balance = 0;
                    childElem.Balance = -1;
                }
            }
            grandchildElem.Balance = 0;

            this.elements[child] = childElem;
            this.elements[parent] = parentElem;
            this.elements[grandchild] = grandchildElem;

            return grandchild;
        }

        private void ShiftNodes(int parent, int child)
        {
            var parentElem = this.elements[parent];
            if (parentElem.Parent < 0)
            {
                this.root = child;
            }
            else
            {
                var parentParentElem = this.elements[parentElem.Parent];
                if (parent == parentParentElem.Left)
                {
                    parentParentElem.Left = child;
                }
                else
                {
                    parentParentElem.Right = child;
                }
                this.elements[parentElem.Parent] = parentParentElem;
            }

            if (child >= 0)
            {
                var childElem = this.elements[child];
                childElem.Parent = parentElem.Parent;
                this.elements[child] = childElem;
            }
        }

        private void RemoveInternal(int node)
        {
            var nodeElem = this.elements[node];
            int childIdx = node;

            bool wasLeft = nodeElem.Parent >= 0 && node == this.elements[nodeElem.Parent].Left;
            if (nodeElem.Left < 0)
            {
                this.ShiftNodes(node, nodeElem.Right);
                this.free.Add(node);
            }
            else if (nodeElem.Right < 0)
            {
                this.ShiftNodes(node, nodeElem.Left);
                this.free.Add(node);
            }
            else
            {
                var successor = this.GetSuccessor(node);
                var successorElem = this.elements[successor];
                var rmvTreeNode = new TreeNode()
                {
                    Left = successorElem.Left,
                    Right = successorElem.Right,
                    Parent = successorElem.Parent
                };
                wasLeft = successorElem.Parent >= 0 && this.elements[successorElem.Parent].Left == successor;

                if (successorElem.Parent != node)
                {
                    this.ShiftNodes(successor, successorElem.Right);
                    successorElem = this.elements[successor];
                    successorElem.Right = nodeElem.Right;
                    var successorRight = this.elements[successorElem.Right];
                    successorRight.Parent = successor;
                    this.elements[successorElem.Right] = successorRight;
                    this.elements[successor] = successorElem;
                }
                this.ShiftNodes(node, successor);
                successorElem = this.elements[successor];
                successorElem.Left = nodeElem.Left;
                var successorLeft = this.elements[successorElem.Left];
                successorLeft.Parent = successor;
                this.elements[successorElem.Left] = successorLeft;
                successorElem.Balance = nodeElem.Balance;
                this.elements[successor] = successorElem;

                if (rmvTreeNode.Parent == node) { rmvTreeNode.Parent = successor; }

                this.free.Add(node);
                nodeElem = rmvTreeNode;
                childIdx = int.MinValue;
            }

            int iteration = 0;
            int grandParent, rotatedNode;
            while (nodeElem.Parent >= 0)
            {
                int nodeIdx = nodeElem.Parent;
                nodeElem = this.elements[nodeIdx];
                grandParent = nodeElem.Parent;

                int balance = nodeElem.Balance;
                int siblingBalance = 0;

                if (nodeElem.Left < 0 && nodeElem.Right < 0)
                {
                    nodeElem.Balance = 0;
                    this.elements[nodeIdx] = nodeElem;
                    iteration++;
                    childIdx = nodeIdx;
                    continue;
                }
                else if (childIdx == nodeElem.Left || (iteration == 0 && wasLeft))
                {
                    if (balance > 0)
                    {
                        var sibling = nodeElem.Right;
                        var siblingElem = this.elements[sibling];
                        siblingBalance = siblingElem.Balance;
                        if (siblingBalance < 0)
                        {
                            rotatedNode = this.RotateRightLeft(nodeIdx, sibling);
                        }
                        else
                        {
                            rotatedNode = this.RotateLeft(nodeIdx, sibling);
                        }
                    }
                    else
                    {
                        if (balance == 0)
                        {
                            nodeElem.Balance = 1;
                            this.elements[nodeIdx] = nodeElem;
                            break;
                        }
                        nodeElem.Balance = 0;
                        this.elements[nodeIdx] = nodeElem;
                        iteration++;
                        childIdx = nodeIdx;
                        continue;
                    }
                }
                else
                {
                    if (balance < 0)
                    {
                        var sibling = nodeElem.Left;
                        var siblingElem = this.elements[sibling];
                        siblingBalance = siblingElem.Balance;
                        if (siblingBalance > 0)
                        {
                            rotatedNode = this.RotateLeftRight(nodeIdx, sibling);
                        }
                        else
                        {
                            rotatedNode = this.RotateRight(nodeIdx, sibling);
                        }

                    }
                    else
                    {
                        if (balance == 0)
                        {
                            nodeElem.Balance = -1;
                            this.elements[nodeIdx] = nodeElem;
                            break;
                        }
                        nodeElem.Balance = 0;
                        this.elements[nodeIdx] = nodeElem;
                        iteration++;
                        childIdx = nodeIdx;
                        continue;
                    }
                }

                var rotatedElem = this.elements[rotatedNode];
                rotatedElem.Parent = grandParent;
                this.elements[rotatedNode] = rotatedElem;
                if (grandParent >= 0)
                {
                    var grandParentElem = this.elements[grandParent];
                    if (nodeIdx == grandParentElem.Left)
                    {
                        grandParentElem.Left = rotatedNode;
                    }
                    else
                    {
                        grandParentElem.Right = rotatedNode;
                    }
                    this.elements[grandParent] = grandParentElem;
                }
                else
                {
                    this.root = rotatedNode;
                }
                childIdx = rotatedNode;

                if (siblingBalance == 0) break;

                iteration++;
            }
        }

        /// <summary>
        /// Removes the node with the given array index
        /// </summary>
        /// <param name="node"></param>
        /// <returns>False, if the index is outside the range of the array. True otherwise. Behaviour is undefined when removing a
        /// node that is marked as free.</returns>
        public bool RemoveNode(int node)
        {
            bool rmvSuccess = node >= 0 && node < this.elements.Length;
            if(rmvSuccess)
            {
                this.RemoveInternal(node);
            }
            return rmvSuccess;
        }

        /// <summary>
        /// Calculates a code that represents the position of the node with the value when looking at the tree in a left-right
        /// direction. The lowest code is the leftmost node, and the highest code the rightmost. 0.5 represents the root.
        /// This is useful, if you want to know from a given set of values, which one is the one most to the left in the tree etc.
        /// (which is information you sometimes need for sweepline algorithms)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="idx"></param>
        /// <returns></returns>
        public float GetTreeCode(T value, out int idx)
        {
            idx = this.RootIdx;
            float val = 0.25f;

            int level = 1;

            var basePtr = (TreeNode*)this.elements.GetUnsafePtr();
            var currentElem = basePtr + idx;

            int cmp;
            while ((cmp = this.comparer.Compare(currentElem->Value, value)) != 0)
            {
                val -= 1.0f / (float)(1 << (level + 1)) * cmp;
                idx = cmp >= 0 ? currentElem->Left : currentElem->Right;

                if (idx < 0) break;

                currentElem = basePtr + idx; 
                level++;
            }
            return val * 2;
        }

        /// <summary>
        /// Removes the node which contains the given value from the tree.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>True, if the element was found in the tree and removed. False, if the elements was not found</returns>
        public bool Remove(T value)
        {
            var node = this.Search(value);
            bool rmvSuccess = node >= 0;

            if(rmvSuccess)
            {
                this.RemoveInternal(node);
            }
            return rmvSuccess;
        }

        /// <summary>
        /// Inserts a new node into the tree containing the given value
        /// </summary>
        /// <param name="value"></param>
        public void Insert(T value)
        {
            int node = this.root, previousNode = -1;

            int cmp = 0;
            while(node >= 0)
            {
                previousNode = node;
                var element = this.elements[node];
                cmp = this.comparer.Compare(value, element.Value);
                node = cmp < 0 ? element.Left : element.Right;
            }

            TreeNode newNode = new TreeNode()
            {
                Parent = previousNode,
                Balance = 0,
                Left = -1,
                Right = -1,
                Value = value
            };

            int newNodePtr = -1;
            //Save new element so it is not garbage collected and has a fixed adress xD
            if (this.free.Length > 0)
            {
                int freeIdx = this.free[this.free.Length - 1];
                this.free.Length--;

                this.elements[freeIdx] = newNode;
                newNodePtr = freeIdx;
            }
            else
            {
                newNodePtr = this.elements.Length;
                this.elements.Add(newNode);
            }

            if(previousNode < 0)
            {
                this.root = newNodePtr;
            } else
            {
                var prevElem = this.elements[previousNode];
                if(cmp < 0)
                {
                    prevElem.Left = newNodePtr;
                } else
                {
                    prevElem.Right = newNodePtr;
                }
                this.elements[previousNode] = prevElem;
            }

            int child = -1;
            int parent = newNodePtr;

            while (this.elements[parent].Parent >= 0)
            {
                child = parent;
                parent = this.elements[parent].Parent;

                var childElem = this.elements[child];
                var parentElem = this.elements[parent];
                int grandParent = parentElem.Parent;
                int rotatedNode;



                int balance = parentElem.Balance;

                if (child == parentElem.Right)
                {
                    if (balance > 0)
                    {
                        int childBalance = childElem.Balance;
                        if (childBalance < 0)
                        {
                            rotatedNode = this.RotateRightLeft(parent, child);
                        }
                        else
                        {
                            rotatedNode = this.RotateLeft(parent, child);
                        }
                    }
                    else
                    {
                        if (balance < 0)
                        {
                            parentElem.Balance = 0;
                            this.elements[parent] = parentElem;
                            break;
                        }
                        parentElem.Balance = 1;
                        this.elements[parent] = parentElem;
                        continue;
                    }

                }
                else
                {
                    if (balance < 0)
                    {
                        int childBalance = childElem.Balance;
                        if (childBalance > 0)
                        {
                            rotatedNode = this.RotateLeftRight(parent, child);

                        } else
                        {
                            rotatedNode = this.RotateRight(parent, child);
                        }

                    }
                    else
                    {
                        if (balance > 0)
                        {
                            parentElem.Balance = 0;
                            this.elements[parent] = parentElem;
                            break;
                        }
                        parentElem.Balance = -1;
                        this.elements[parent] = parentElem;
                        continue;
                    }
                }

                var rotatedElem = this.elements[rotatedNode];
                rotatedElem.Parent = grandParent;
                this.elements[rotatedNode] = rotatedElem;

                if(grandParent >= 0)
                {
                    var grandParentElem = this.elements[grandParent];
                    if(parent == grandParentElem.Left)
                    {
                        grandParentElem.Left = rotatedNode;
                    } else
                    {
                        grandParentElem.Right = rotatedNode;
                    }
                    this.elements[grandParent] = grandParentElem;
                } else
                {
                    this.root = rotatedNode;
                }

                break;
            }
        }

        public IBalancedSearchTreeNode<T> GetNodeAt(int index) { return this.elements[index]; }
    }
}
