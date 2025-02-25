using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Implementation of a Red-Black Tree to be used in Unity's Job System.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U">A comparison struct used to compare the inserted elements with each other</typeparam>
    public unsafe struct NativeRBTree<T, U> : IBalancedSearchTree<T>, IDisposable where T : unmanaged where U : struct, IComparer<T> 
    {
        public struct TreeNode : IBalancedSearchTreeNode<T>
        {
            public int Parent { get; set; }
            public int Left { get; set; }
            public int Right { get; set; }

            public T Value { get; set; }

            public byte Black { get; set; }
        }

        #region Public Variables

        public U comparer;

        #endregion

        #region Private Variables

        [NoAlias]
        private NativeList<TreeNode> elements;

        [NoAlias]
        private NativeList<int> free;

        private int root;

        #endregion

        public int Length => this.elements.Length;

        public TreeNode* Root => this.root >= 0 ? (TreeNode*)this.elements.GetUnsafePtr() + this.root : null;

        public int RootIdx => this.root;

        public NativeList<TreeNode> Elements => this.elements;

        public NativeRBTree(U comparer, Allocator allocator)
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

        private int Search(T value)
        {
            int cmp;

            var basePtr = (TreeNode*)this.elements.GetUnsafePtr<TreeNode>();
            var nodePtr = basePtr + this.root;
            int node = this.root;

            while(node >= 0 && (cmp = this.comparer.Compare(value, nodePtr->Value)) != 0)
            {
                node = cmp < 0 ? nodePtr->Left : nodePtr->Right;
                nodePtr = basePtr + node;
            }
            return node;
        }

        public bool Contains(T value)
        {
            return this.Search(value) >= 0;
        }

        public int GetElementIdx(T value) => this.Search(value);

        private void RotateRight(TreeNode* basePtr, TreeNode* parent, TreeNode* grandParent,
            int parentIdx, int grandParentIdx)
        {
            int parentRight = parent->Right;
            int grandGrandParentIdx = grandParent->Parent;
            byte parentColor = parent->Black;
            byte grandParentColor = grandParent->Black;

            grandParent->Parent = parentIdx;
            grandParent->Left = parentRight;
            grandParent->Black = parentColor;

            parent->Right = grandParentIdx;
            parent->Black = grandParentColor;
            parent->Parent = grandGrandParentIdx;

            if (parentRight >= 0)
            {
                var parentRightNode = basePtr + parentRight;
                parentRightNode->Parent = grandParentIdx;
            }

            if (grandGrandParentIdx >= 0)
            {
                var grandGrandParent = basePtr + grandGrandParentIdx;
                if (grandGrandParent->Left == grandParentIdx) grandGrandParent->Left = parentIdx;
                else grandGrandParent->Right = parentIdx;
            }

            if (grandParentIdx == this.root) this.root = parentIdx;
        }

        private void RotateLeft(TreeNode* basePtr, TreeNode* parent, TreeNode* grandParent,
            int parentIdx, int grandParentIdx)
        {
            int parentLeft = parent->Left;
            int grandGrandParentIdx = grandParent->Parent;
            byte parentColor = parent->Black;
            byte grandParentColor = grandParent->Black;

            grandParent->Parent = parentIdx;
            grandParent->Right = parentLeft;
            grandParent->Black = parentColor;

            parent->Left = grandParentIdx;
            parent->Black = grandParentColor;
            parent->Parent = grandGrandParentIdx;

            if (parentLeft >= 0)
            {
                var parentLeftNode = basePtr + parentLeft;
                parentLeftNode->Parent = grandParentIdx;
            }

            if (grandGrandParentIdx >= 0)
            {
                var grandGrandParent = basePtr + grandGrandParentIdx;
                if (grandGrandParent->Left == grandParentIdx) grandGrandParent->Left = parentIdx;
                else grandGrandParent->Right = parentIdx;
            }

            if (grandParentIdx == this.root) this.root = parentIdx;
            
        }

        private void RotateLeftRight(TreeNode* basePtr, TreeNode* current, TreeNode* parent, TreeNode* grandParent,
            int currentIdx, int parentIdx, int grandParentIdx)
        {
            int currentRight = current->Right;
            int currentLeft = current->Left;
            int grandGrandParentIdx = grandParent->Parent;
            byte currentColor = current->Black;
            byte grandParentColor = grandParent->Black;

            current->Right = grandParentIdx;
            current->Left = parentIdx;
            current->Parent = grandGrandParentIdx;
            current->Black = grandParentColor;

            parent->Parent = currentIdx;
            parent->Right = currentLeft;

            grandParent->Parent = currentIdx;
            grandParent->Left = currentRight;
            grandParent->Black = currentColor;

            if(currentLeft >= 0)
            {
                var currentLeftNode = basePtr + currentLeft;
                currentLeftNode->Parent = parentIdx;
            }

            if(currentRight >= 0)
            {
                var currentRightNode = basePtr + currentRight;
                currentRightNode->Parent = grandParentIdx;
            }

            if (grandGrandParentIdx >= 0)
            {
                var grandGrandParent = basePtr + grandGrandParentIdx;
                if (grandGrandParent->Left == grandParentIdx) grandGrandParent->Left = currentIdx;
                else grandGrandParent->Right = currentIdx;
            }

            if (grandParentIdx == this.root) this.root = currentIdx;
        }

        private void RotateRightLeft(TreeNode* basePtr, TreeNode* current, TreeNode* parent, TreeNode* grandParent,
            int currentIdx, int parentIdx, int grandParentIdx)
        {

            int currentRight = current->Right;
            int currentLeft = current->Left;
            int grandGrandParentIdx = grandParent->Parent;
            byte currentColor = current->Black;
            byte grandParentColor = grandParent->Black;

            current->Right = parentIdx;
            current->Left = grandParentIdx;
            current->Parent = grandGrandParentIdx;
            current->Black = grandParentColor;

            parent->Parent = currentIdx;
            parent->Left = currentRight;

            grandParent->Parent = currentIdx;
            grandParent->Right = currentLeft;
            grandParent->Black = currentColor;

            if (currentRight >= 0)
            {
                var currentRightNode = basePtr + currentRight;
                currentRightNode->Parent = parentIdx;
            }

            if (currentLeft >= 0)
            {
                var currentLeftNode = basePtr + currentLeft;
                currentLeftNode->Parent = grandParentIdx;
            }

            if (grandGrandParentIdx >= 0)
            {
                var grandGrandParent = basePtr + grandGrandParentIdx;
                if (grandGrandParent->Left == grandParentIdx) grandGrandParent->Left = currentIdx;
                else grandGrandParent->Right = currentIdx;
            }

            if (grandParentIdx == this.root) this.root = currentIdx;
        }


        public void Insert(T value)
        {
            int cmp = 0;

            var basePtr = (TreeNode*)this.elements.GetUnsafePtr<TreeNode>();
            int nodeIdx = this.root, parentIdx = -1;

            while (nodeIdx >= 0)
            {
                var nodePtr = basePtr + nodeIdx;
                cmp = this.comparer.Compare(value, nodePtr->Value);
                parentIdx = nodeIdx;
                nodeIdx = cmp < 0 ? nodePtr->Left : nodePtr->Right;
            }


            TreeNode newNode = new TreeNode()
            {
                Parent = parentIdx,
                Left = -1,
                Right = -1,
                Value = value,
                Black = 0,
            };

            int currentIdx = -1;
            if(this.free.Length > 0)
            {
                int freeIdx = this.free[this.free.Length - 1];
                this.free.Length--;

                this.elements[freeIdx] = newNode;
                currentIdx = freeIdx;
            } else
            {
                currentIdx = this.elements.Length;
                this.elements.Add(newNode);
                //Array might have been resized - fetch the base ptr again
                basePtr = (TreeNode*)this.elements.GetUnsafePtr<TreeNode>();
            }
            var current = basePtr + currentIdx;

            if(parentIdx < 0)
            {
                current->Black = 1;
                this.root = currentIdx;
            } else
            {

                var parent = basePtr + parentIdx;
                if (cmp <= 0) parent->Left = currentIdx;
                else parent->Right = currentIdx;

                while(parent->Parent >= 0 && parent->Black == 0)
                {
                    int grandParentIdx = parent->Parent;
                    var grandParent = basePtr + grandParentIdx;
                    int uncleIdx;
                    bool pLeft = grandParent->Left == parentIdx;
                    bool cLeft = parent->Left == currentIdx;

                    if(pLeft) uncleIdx = grandParent->Right;
                    else uncleIdx = grandParent->Left;

                    if(uncleIdx >= 0)
                    {
                        var uncle = basePtr + uncleIdx;
                        if(uncle->Black == 0)
                        {
                            uncle->Black = 1;
                            parent->Black = 1;
                            if(grandParentIdx != this.root) grandParent->Black = 0;
                            current = grandParent;
                            currentIdx = grandParentIdx;
                            parentIdx = grandParent->Parent;
                            if (parentIdx < 0) break;
                            parent = basePtr + parentIdx;
                            continue;
                        }
                    }

                    if(pLeft)
                    {
                        if(cLeft)
                        {
                            this.RotateRight(basePtr, parent, grandParent, parentIdx, grandParentIdx);
                        } else
                        {
                            this.RotateLeftRight(basePtr, current, parent, grandParent, currentIdx, parentIdx, grandParentIdx);

                        }

                    } else
                    {
                        if(cLeft)
                        {
                            this.RotateRightLeft(basePtr, current, parent, grandParent, currentIdx, parentIdx, grandParentIdx);

                        } else
                        {
                            this.RotateLeft(basePtr, parent, grandParent, parentIdx, grandParentIdx);

                        }
                    }
                    return;
  
                }
            }


        }

        private void ShiftNodes(TreeNode* ptr, int parent, int child)
        {
            var parentElem = ptr + parent;
            if(parentElem->Parent < 0)
            {
                this.root = child;
            }
            else
            {
                var grandParentElem = ptr + parentElem->Parent;
                if(grandParentElem->Left == parent) grandParentElem->Left = child;
                else grandParentElem->Right = child;
            }

            var childElem = ptr + child;
            childElem->Parent = parentElem->Parent;
        }

        private int GetSiblingInternal(TreeNode* parent, int nodeIdx, out bool nodeIsLeft)
        {
            if (parent->Left == nodeIdx)
            {
                nodeIsLeft = true;
                return parent->Right;
            }
            nodeIsLeft = false;
            return parent->Left;
        }

        private void Delete3(TreeNode* ptr, TreeNode* parent, TreeNode* sibling, int parentIdx, int siblingIdx,
            int closeIdx, int distantIdx, bool nodeIsLeft)
        {
            //Case 3:
            if (nodeIsLeft)
            {
                this.RotateLeft(ptr, sibling, parent, siblingIdx, parentIdx);
            }
            else
            {
                this.RotateRight(ptr, sibling, parent, siblingIdx, parentIdx);
            }

            parent->Black = 0;
            sibling->Black = 1;

            siblingIdx = closeIdx;
            sibling = (ptr + siblingIdx);

            distantIdx = nodeIsLeft ? sibling->Right : sibling->Left;
            if (distantIdx >= 0 && (ptr + distantIdx)->Black == 0)
            {
                this.Delete6(ptr, parent, sibling, parentIdx, siblingIdx, closeIdx, distantIdx, nodeIsLeft);
                return;
            }

            closeIdx = nodeIsLeft ? sibling->Left : sibling->Right;
            if (closeIdx >= 0 && (ptr + closeIdx)->Black == 0)
            {
                this.Delete5(ptr, parent, sibling, parentIdx, siblingIdx, closeIdx, distantIdx, nodeIsLeft);
                return;
            }

            this.Delete4(parent, sibling);
        }

        private void Delete5(TreeNode* ptr, TreeNode* parent, TreeNode* sibling, int parentIdx, int siblingIdx, 
            int closeIdx, int distantIdx, bool nodeIsLeft)
        {
            //Case 5:
            if (nodeIsLeft)
            {
                var siblingLeft = ptr + sibling->Left;
                RotateRight(ptr, siblingLeft, sibling, sibling->Left, siblingIdx);
            }
            else
            {
                var siblingRight = ptr + sibling->Right;
                RotateLeft(ptr, siblingRight, sibling, sibling->Right, siblingIdx);
            }

            sibling->Black = 0;
            (ptr + closeIdx)->Black = 1;

            distantIdx = siblingIdx;
            siblingIdx = closeIdx;

            sibling = (ptr + siblingIdx);

            this.Delete6(ptr, parent, sibling, parentIdx, siblingIdx, closeIdx, distantIdx, nodeIsLeft);
        }

        private void Delete6(TreeNode* ptr, TreeNode* parent, TreeNode* sibling, int parentIdx,int siblingIdx, 
            int closeIdx, int distantIdx, bool nodeIsLeft)
        {
            //Case 6:
            byte parentColor;
            if (nodeIsLeft)
            {
                parentColor = parent->Black;
                this.RotateLeft(ptr, sibling, parent, siblingIdx, parentIdx);
            }
            else
            {
                parentColor = parent->Black;
                this.RotateRight(ptr, sibling, parent, siblingIdx, parentIdx);
            }

            sibling->Black = parentColor;
            parent->Black = 1;
            (ptr + distantIdx)->Black = 1;
        }

        private void Delete4(TreeNode* parent, TreeNode* sibling)
        {
            //Case 4:
            sibling->Black = 0;
            parent->Black = 1;
        }

        //Actual implementation from the Wiki - its really detailled:
        //https://en.wikipedia.org/wiki/Red%E2%80%93black_tree#con5
        private void HandleDoubleBlack(TreeNode* ptr, int nodeIdx)
        {
            var node = ptr + nodeIdx;
            int parentIdx = node->Parent;

            if (parentIdx < 0) return;

            TreeNode* parent = ptr + parentIdx;

            int siblingIdx = GetSiblingInternal(parent, nodeIdx, out bool nodeIsLeft);
            var sibling = ptr + siblingIdx;

            if (nodeIsLeft) parent->Left = -1;
            else parent->Right = -1;

            int iteration = 0;
            do
            {
                parent = ptr + parentIdx;
                if (iteration > 0)
                {
                    siblingIdx = GetSiblingInternal(parent, nodeIdx, out nodeIsLeft);
                    sibling = ptr + siblingIdx;
                }

                int distantIdx = nodeIsLeft ? sibling->Right : sibling->Left;
                int closeIdx = nodeIsLeft ? sibling->Left : sibling->Right;

                if (sibling->Black == 0)
                {
                    this.Delete3(ptr, parent, sibling, parentIdx, siblingIdx, closeIdx, distantIdx, nodeIsLeft);
                    break;
                }

                if(distantIdx >= 0 && (ptr + distantIdx)->Black == 0)
                {
                    this.Delete6(ptr, parent, sibling, parentIdx, siblingIdx, closeIdx, distantIdx, nodeIsLeft);
                    break;
                }

                if(closeIdx >= 0 && (ptr + closeIdx)->Black == 0)
                {
                    this.Delete5(ptr, parent, sibling, parentIdx, siblingIdx, closeIdx, distantIdx, nodeIsLeft);
                    break;
                }

                if(parent->Black == 0)
                {
                    this.Delete4(parent, sibling);

                    break;
                }

                sibling->Black = 0;
                nodeIdx = parentIdx;

                node = ptr + nodeIdx;

                parentIdx = node->Parent;

                iteration++;

            } while (parentIdx >= 0);
        }

        private void RemoveInternal(int node)
        {
            

            var ptr = (TreeNode*)this.elements.GetUnsafePtr();

            var nodeElem = ptr + node;
            if (nodeElem->Left >= 0 && nodeElem->Right >= 0)
            {
                node = this.GetSuccessor(node);

                //Swap - Free
                var successorElem = ptr + node;
                nodeElem->Value = successorElem->Value;
                nodeElem = successorElem;
            }
            int nodeBlack = nodeElem->Black;

            this.free.Add(node);

            if (node == this.root && nodeElem->Left < 0 && nodeElem->Right < 0)
            {
                this.root = -1;
                return;
            }


            //Simple Cases
            var nodeParentIdx = nodeElem->Parent;
            var nodeParent = ptr + nodeParentIdx;

            //Only one child
            if (nodeElem->Left >= 0 != nodeElem->Right >= 0)
            {
                if(nodeElem->Left >= 0)
                {
                    var nodeLeftIdx = nodeElem->Left;
                    var nodeLeft = ptr + nodeLeftIdx;

                    if (nodeParentIdx >= 0)
                    {
                        if (nodeParent->Left == node) nodeParent->Left = nodeLeftIdx;
                        else nodeParent->Right = nodeLeftIdx;
                    } else
                    {
                        this.root = nodeLeftIdx;
                    }

                    nodeLeft->Black = 1;
                    nodeLeft->Parent = nodeParentIdx;
                }
                else
                {
                    var nodeRightIdx = nodeElem->Right;
                    var nodeRight = ptr + nodeRightIdx;

                    if (nodeParentIdx >= 0)
                    {
                        if (nodeParent->Right == node) nodeParent->Right = nodeRightIdx;
                        else nodeParent->Left = nodeRightIdx;
                    } else
                    {
                        this.root = nodeRightIdx;
                    }

                    nodeRight->Black = 1;
                    nodeRight->Parent = nodeParentIdx;
                }
            //No children
            } else if(nodeElem->Left < 0 && nodeElem->Right < 0)
            {
                if(nodeElem->Black == 0)
                {
                    if (nodeParent->Left == node) nodeParent->Left = -1;
                    else nodeParent->Right = -1;
                }
                else
                {
                    //Double black
                    this.HandleDoubleBlack(ptr, node);
                }
            }

        }

        public bool RemoveNode(int node)
        {
            if(node >= 0 && node < this.elements.Length)
            {
                this.RemoveInternal(node);
                return true;
            }
            return false;
        }

        public bool Remove(T value)
        {

            var node = this.Search(value);
            if(node >= 0)
            {
                this.RemoveInternal(node);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The index of the leftmost node of the tree. If the tree is empty, -1 is returned</returns>
        public int GetLeftmostNode()
        {
            var node = this.RootIdx;
            var ptr = (TreeNode*)this.elements.GetUnsafePtr();
            if (node >= 0)
            {
                var elem = ptr + node;
                while (elem->Left >= 0)
                {
                    node = elem->Left;
                    elem = ptr + node;
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
            var ptr = (TreeNode*)this.elements.GetUnsafePtr();
            if (node >= 0)
            {
                var elem = ptr + node;
                while (elem->Right >= 0)
                {
                    node = elem->Right;
                    elem = ptr + node;
                }
            }
            return node;
        }

        private void GetHeightRecursive(TreeNode* basePtr, int idx, int currentHeight, ref int maxHeight)
        {
            currentHeight++;
            if(currentHeight > maxHeight) maxHeight = currentHeight;

            var element = basePtr + idx;
            if (element->Left >= 0) this.GetHeightRecursive(basePtr, element->Left, currentHeight, ref maxHeight);
            if (element->Right >= 0) this.GetHeightRecursive(basePtr, element->Right, currentHeight, ref maxHeight);
        }

        public int GetHeight()
        {
            if(this.root >= 0)
            {
                int height = 0;
                var basePtr = (TreeNode*)this.elements.GetUnsafePtr();
                this.GetHeightRecursive(basePtr, this.root, height, ref height);
                return height;
            }
            return 0;
        }

        public IBalancedSearchTreeNode<T> GetNodeAt(int index) { return this.elements[index]; }

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

        private void GetAllTreeElementsRecursion(ref NativeList<int> nodeIndices, TreeTraversal traversal, int currentNode)
        {
            var elem = this.Elements[currentNode];
            switch (traversal)
            {
                case TreeTraversal.PREORDER:

                    nodeIndices.Add(currentNode);
                    if (elem.Left >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Left);
                    }
                    if (elem.Right >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Right);
                    }
                    break;
                case TreeTraversal.POSTORDER:
                    if (elem.Left >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Left);
                    }
                    if (elem.Right >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Right);
                    }
                    nodeIndices.Add(currentNode);
                    break;
                case TreeTraversal.INORDER:
                    if (elem.Left >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Left);
                    }
                    nodeIndices.Add(currentNode);
                    if (elem.Right >= 0)
                    {
                        this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, elem.Right);
                    }
                    break;
            }
        }

        public void GetAllTreeElements(ref NativeList<int> nodeIndices, TreeTraversal traversal)
        {
            if (this.root >= 0)
            {
                this.GetAllTreeElementsRecursion(ref nodeIndices, traversal, this.root);
            }
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
                parentIdx = (basePtr + parentIdx)->Parent;
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
                parentIdx = (basePtr + parentIdx)->Parent;
            }
            return parentIdx;
        }

        private bool AreNodesValidRecursion(TreeNode* ptr, int nodeIdx)
        {
            var node = ptr + nodeIdx;

            bool valid = true;

            bool hasLeft = node->Left >= 0;
            bool hasRight = node->Right >= 0;

            if (node->Black == 0)
            {
                valid &= (!hasLeft || (ptr + node->Left)->Black == 1);
                valid &= (!hasRight || (ptr + node->Right)->Black == 1);
            }

            if(hasLeft != hasRight)
            {
                if(hasLeft)
                {
                    valid &= (ptr + node->Left)->Black == 0;
                } else
                {
                    valid &= (ptr + node->Right)->Black == 0;
                }
            }

            if(hasLeft)
            {
                valid &= this.AreNodesValidRecursion(ptr, node->Left);
            }

            if(hasRight)
            {
                valid &= this.AreNodesValidRecursion(ptr, node->Right);
            }

            return valid;
        }

        public bool AreNodesValid()
        {
            var root = this.root;
            if (root < 0) return true;
            var ptr = (TreeNode*)this.elements.GetUnsafePtr();
            return this.AreNodesValidRecursion(ptr, root);

        }
    }


}
