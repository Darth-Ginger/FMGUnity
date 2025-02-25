using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace GimmeDOTSGeometry
{

    //Implementation is a probabilistic skip list, but I named it SortedList, so the concept
    //is clear to a user.
    //I have to say: A looot simpler than an AVL Tree ^^

    //The original (William Pugh, 1989): https://www.eecs.umich.edu/courses/eecs380/ALG/niemann/s_skip.pdf
    //The cookbook (William Pugh again, 1989): https://citeseerx.ist.psu.edu/document?repid=rep1&type=pdf&doi=b9de4e4235b2dfc34eda7b48db3662e0c80d91aa 


    /// <summary>
    /// A list that always keeps itself sorted based on a provided comparer (skip list)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct NativeSortedList<T, U> : IEnumerable<T>, IDisposable where T : unmanaged where U : struct, IComparer<T>
    {

        public unsafe struct Enumerator : IEnumerator<T>
        {
            private int index;

            private NativeSortedList<T, U> sortedList;

            private int currentPtrIdx;

            public T Current {
                get
                {
                    var ptr = this.sortedList.m_PointerBuffer[this.currentPtrIdx];
                    var node = ptr.node;
                    return this.sortedList.m_NodeBuffer[node].element;
                }
            }

            object IEnumerator.Current => Current;

            public Enumerator(ref NativeSortedList<T, U> sortedList)
            {
                this.index = -1;
                this.sortedList = sortedList;
                this.currentPtrIdx = -1;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (this.sortedList.Length == 0) return false;

                if (this.currentPtrIdx < 0)
                {
                    this.currentPtrIdx = this.sortedList.m_Header;
                    var pointer = this.sortedList.m_PointerBuffer[this.currentPtrIdx];
                    while(pointer.downwards >= 0)
                    {
                        this.currentPtrIdx = pointer.downwards;
                        pointer = this.sortedList.m_PointerBuffer[pointer.downwards];
                    }
                }
                var currentPtr = this.sortedList.m_PointerBuffer[this.currentPtrIdx];
                this.currentPtrIdx = currentPtr.forwards;
                
                this.index++;
                return this.index < this.sortedList.Length;
            }

            public void Reset()
            {
                this.currentPtrIdx = -1;
            }
        }

        internal struct PathNode
        {
            public int currentIdx;
            public int pointer;

            public PathNode(int currentIdx, int pointer)
            {
                this.currentIdx = currentIdx;
                this.pointer = pointer;
            }
        }

        internal struct SkipListPointer
        {
            public int width;

            public int node;

            public int forwards;
            public int downwards;
        }

        internal struct SkipListNode
        {
            public T element;

            public int top;

        }

        #region Internal Fields

        internal Allocator m_AllocatorLabel;

        internal int m_Length;
        internal int m_MaxLevels;
        internal int m_CurrentLevels;

        internal int m_ReservedPointers;
        internal int m_ReservedNodes;
        internal int m_ReservedPointerSlots;
        internal int m_ReservedNodeSlots;

        internal int m_UsedPointers;
        internal int m_UsedNodes;
        internal int m_UsedPointerSlots;
        internal int m_UsedNodeSlots;

        internal Random m_Rnd;

        internal int m_Header;

        [NativeDisableUnsafePtrRestriction]
        internal SkipListPointer* m_PointerBuffer;

        [NativeDisableUnsafePtrRestriction]
        internal SkipListNode* m_NodeBuffer;

        //Swapback does not work, as it would break the pointer links. Fixing those links would be either computationally expensive,
        //or in terms of memory (backwards-pointers). Therefore, we manually keep track of
        //empty spaces in the arrays.
        [NativeDisableUnsafePtrRestriction]
        internal int* m_FreePointerSlots;

        [NativeDisableUnsafePtrRestriction]
        internal int* m_FreeNodeSlots;

        //Small log(n) extra buffer for storing the path, so that we do not have to allocate anything
        //during insertion and removal of elements
        //Buffer Size is always equal to m_MaxLevels
        [NativeDisableUnsafePtrRestriction]
        internal PathNode* m_PathNodeBuffer;

        internal U comparer;

        internal static readonly int s_DefaultCapacity = 16;


        #endregion

#if ENABLE_UNITY_COLLECTIONS_CHECKS

        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel m_DisposeSentinel;

        internal int m_MinIndex;
        internal int m_MaxIndex;

        internal static int s_staticSafetyId;

        [BurstDiscard]
        static void AssignStaticSafetyId(ref AtomicSafetyHandle safetyHandle)
        {
            if(s_staticSafetyId == 0)
            {
                s_staticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<NativeSortedList<T, U>>();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref safetyHandle, s_staticSafetyId);
        }

        [BurstDiscard]
        internal static void IsUnmanagedAndThrow()
        {
            if (!UnsafeUtility.IsValidNativeContainerElementType<T>())
            {
                throw new InvalidOperationException($"{typeof(T)} used in NativeSortedList<{typeof(T)},{typeof(U)}> must be unmanaged (contain no managed types) and cannot itself be a native container type.");
            }
        }

#endif



        public NativeSortedList(U comparer, Allocator allocator, uint seed = 1851936439u)
        {
            this.comparer = comparer;

            this.m_Length = 0;
            this.m_MaxLevels = math.tzcnt(s_DefaultCapacity);
            this.m_CurrentLevels = 0;
            this.m_AllocatorLabel = allocator;

            this.m_Rnd = new Random();
            this.m_Rnd.InitState(seed);

            this.m_Header = -1;

            this.m_ReservedNodes = s_DefaultCapacity;
            this.m_ReservedPointers = s_DefaultCapacity * 2;
            this.m_ReservedNodeSlots = s_DefaultCapacity;
            this.m_ReservedPointerSlots = s_DefaultCapacity;

            this.m_UsedNodes = 0;
            this.m_UsedPointers = 0;
            this.m_UsedNodeSlots = 0;
            this.m_UsedPointerSlots = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS

            if(allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistend", "allocator");
            }

            IsUnmanagedAndThrow();

            this.m_MinIndex = 0;
            this.m_MaxIndex = -1;

            DisposeSentinel.Create(out this.m_Safety, out this.m_DisposeSentinel, 0, allocator);

            AssignStaticSafetyId(ref this.m_Safety);
#endif
            int nodeLength = sizeof(SkipListNode) * this.m_ReservedNodes;
            int nodeSlotsLength = sizeof(int) * this.m_ReservedNodeSlots;
            int pointerLength = sizeof(SkipListPointer) * this.m_ReservedPointers;
            int pointerSlotsLength = sizeof(int) * this.m_ReservedPointerSlots;

            this.m_NodeBuffer = (SkipListNode*)UnsafeUtility.Malloc(nodeLength, UnsafeUtility.AlignOf<SkipListNode>(), this.m_AllocatorLabel);
            this.m_PointerBuffer = (SkipListPointer*)UnsafeUtility.Malloc(pointerLength, UnsafeUtility.AlignOf<SkipListPointer>(), this.m_AllocatorLabel);

            this.m_FreeNodeSlots = (int*)UnsafeUtility.Malloc(nodeSlotsLength, UnsafeUtility.AlignOf<int>(), this.m_AllocatorLabel);
            this.m_FreePointerSlots = (int*)UnsafeUtility.Malloc(pointerSlotsLength, UnsafeUtility.AlignOf<int>(), this.m_AllocatorLabel);

            this.m_PathNodeBuffer = (PathNode*)UnsafeUtility.Malloc(this.m_MaxLevels, UnsafeUtility.AlignOf<PathNode>(), this.m_AllocatorLabel);

        }

        public int Length
        {
            get
            {
                return this.m_Length;
            }
        }



        internal SkipListPointer* GetClosestPointer(T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            var headerIdx = this.m_Header;
            var iterationPtr = this.m_PointerBuffer + headerIdx;

            for (int i = this.m_CurrentLevels; i > 0; i--)
            {
                while (iterationPtr->forwards >= 0)
                {
                    var forwardPtr = this.m_PointerBuffer + iterationPtr->forwards;
                    var node = this.m_NodeBuffer + forwardPtr->node;
                    if (this.comparer.Compare(node->element, value) >= 0) break;

                    iterationPtr = forwardPtr;
                }

                if (iterationPtr->downwards >= 0)
                {
                    iterationPtr = this.m_PointerBuffer + iterationPtr->downwards;
                }
            }

            return iterationPtr;
        }

        internal SkipListPointer GetClosestPointer(T value, out int currentIdx)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            var headerIdx = this.m_Header;
            var iterationPtr = this.m_PointerBuffer[headerIdx];

            currentIdx = 0;
            for (int i = this.m_CurrentLevels; i > 0; i--)
            {
                while (iterationPtr.forwards >= 0)
                {
                    int forwardIdx = iterationPtr.forwards;
                    var forwardPtr = this.m_PointerBuffer[forwardIdx];
                    var node = this.m_NodeBuffer[forwardPtr.node];
                    if (this.comparer.Compare(node.element, value) >= 0) break;

                    currentIdx += iterationPtr.width;
                    iterationPtr = forwardPtr;
                }

                if (iterationPtr.downwards >= 0)
                {
                    iterationPtr = this.m_PointerBuffer[iterationPtr.downwards];
                }
            }

            return iterationPtr;
        }

        private SkipListPointer GetTargetPointer(int idx)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

            if (idx < this.m_MinIndex || idx > this.m_MaxIndex)
            {
                FailOutOfRangeError(idx);
            }
#endif

            var headerIdx = this.m_Header;
            var iterationPtr = this.m_PointerBuffer[headerIdx];

            int currentIdx = 0;
            for (int i = this.m_CurrentLevels - 1; i >= 0; i--)
            {
                while (currentIdx + iterationPtr.width <= idx)
                {
                    int forwardIdx = iterationPtr.forwards;
                    if(forwardIdx < 0)
                    {
                        break;
                    }
                    var forwardPtr = this.m_PointerBuffer[forwardIdx];

                    currentIdx += iterationPtr.width;
                    iterationPtr = forwardPtr;
                }

                if(iterationPtr.downwards >= 0)
                {
                    iterationPtr = this.m_PointerBuffer[iterationPtr.downwards];
                }
            }


            int targetIdx = iterationPtr.forwards;
            return this.m_PointerBuffer[targetIdx];
        }

        public T ElementAt(int index)
        {
            var targetPtr = this.GetTargetPointer(index);

            var node = this.m_NodeBuffer[targetPtr.node];
            return node.element;
        }

        private void SetElementAt(T value, int index)
        {
            var targetPtr = this.GetTargetPointer(index);

            var node = this.m_NodeBuffer[targetPtr.node];
            node.element = value;
            this.m_NodeBuffer[targetPtr.node] = node;
        }

        public T this[int index]
        {
            get
            {
                return this.ElementAt(index);
            }
            internal set
            {
                this.SetElementAt(value, index);
            }
        }

        private int GetRandomLevel()
        {
            int level = 1;
            float p = 0.5f;
            while(this.m_Rnd.NextFloat() < p && level < this.m_MaxLevels)
            {
                level++;
            }
            return level;
        }

        public bool IsEmpty()
        {
            return this.m_Header < 0;
        }

        [BurstCompile]
        private struct SearchRangeJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeSortedList<T, U> list;

            [NoAlias, WriteOnly]
            public NativeList<T> result;

            public T start;
            public T end;

            public void Execute()
            {
                this.result.Clear();

                var prevStartPtr = this.list.GetClosestPointer(this.start, out int startIdx);
                var prevEndPtr = this.list.GetClosestPointer(this.end, out int endIdx);

                var iterPtr = prevStartPtr;
                int currentIdx = startIdx;
                while(currentIdx < endIdx)
                {
                    currentIdx += iterPtr.width;
                    int nextPtrIdx = iterPtr.forwards;
                    iterPtr = this.list.m_PointerBuffer[nextPtrIdx];

                    var node = this.list.m_NodeBuffer[iterPtr.node];
                    this.result.Add(node.element);
                }

            }
        }

        //Skip Lists are very well suited for these types of query (it would be a lot more difficult and recursive in a balanced BST)
        /// <summary>
        /// Returns all values in the sorted list between start (inclusive) and end (exlusive)
        /// </summary>
        /// <param name="result"></param>
        /// <param name="startInclusive"></param>
        /// <param name="endExlusive"></param>
        /// <returns></returns>
        public JobHandle SearchRange(ref NativeList<T> result, T startInclusive, T endExlusive, JobHandle dependsOn = default)
        {
            if(this.comparer.Compare(startInclusive, endExlusive) > 0)
            {
                throw new ArgumentException("[Gimme DOTS Geometry]: Start should be \"lower\" than end");
            }

            var rangeJob = new SearchRangeJob()
            {
                list = this,
                result = result,
                start = startInclusive,
                end = endExlusive,
            };
            var handle = rangeJob.Schedule(dependsOn);
            return handle;
        }



        /// <summary>
        /// Returns the index of the element searched for, or, if not found, the
        /// index were it fould have to be inserted minus one (i.e. -1 means, the element
        /// would have to be inserted at index 0, -2 is 1 etc.)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int Search(T value)
        {
            var prevPtr = this.GetClosestPointer(value, out int currentIdx);

            if (prevPtr.forwards >= 0)
            {
                int targetIdx = prevPtr.forwards;
                var targetPtr = this.m_PointerBuffer[targetIdx];

                var node = this.m_NodeBuffer[targetPtr.node];
                if (this.comparer.Compare(node.element, value) == 0)
                {
                    return currentIdx;
                }
                else
                {
                    return -currentIdx - 1;
                }
            }

            return -currentIdx - 1;
        }

        public bool Contains(T value)
        {
            return Search(value) >= 0;
        }

        private int FetchNewPointerIdx()
        {
            if (this.m_UsedPointerSlots > 0)
            {
                this.m_UsedPointerSlots--;
                return this.m_FreePointerSlots[this.m_UsedPointerSlots];
            }
            else
            {
                if (this.m_UsedPointers + 1 >= this.m_ReservedPointers)
                {
                    this.ResizePointers();
                }
                this.m_UsedPointers++;
                return this.m_UsedPointers - 1;
            }
        }

        private int FetchNewNodeIdx()
        {
            if (this.m_UsedNodeSlots > 0)
            {
                this.m_UsedNodeSlots--;
                return this.m_FreeNodeSlots[this.m_UsedNodeSlots];
            }
            else
            {
                if (this.m_UsedNodes + 1 >= this.m_ReservedNodes)
                {
                    this.ResizeNodes();
                }
                this.m_UsedNodes++;
                return this.m_UsedNodes - 1;
            }
        }

        private unsafe void ResizeBuffer<BufferType>(ref int reserved, ref BufferType* ptr) where BufferType : unmanaged
        {
            int newCapacity = reserved * 2;

            int alignOf = UnsafeUtility.AlignOf<BufferType>();
            int size = UnsafeUtility.SizeOf<BufferType>();
            int oldSize = size * reserved;
            int newSize = size * newCapacity;

            var newPointer = (BufferType*)UnsafeUtility.Malloc(newSize, alignOf, this.m_AllocatorLabel);

            UnsafeUtility.MemCpy(newPointer, ptr, oldSize);

            UnsafeUtility.Free(ptr, this.m_AllocatorLabel);

            ptr = newPointer;
            reserved = newCapacity;
        }

        private void ResizePointerSlots()
        {
            this.ResizeBuffer(ref this.m_ReservedPointerSlots, ref this.m_FreePointerSlots);
        }

        private void ResizeNodeSlots()
        {
            this.ResizeBuffer(ref this.m_ReservedNodeSlots, ref this.m_FreeNodeSlots);
        }

        private void ResizePointers()
        {
            this.ResizeBuffer(ref this.m_ReservedPointers, ref this.m_PointerBuffer);
        }

        private void ResizeNodes()
        {
            this.ResizeBuffer(ref this.m_ReservedNodes, ref this.m_NodeBuffer);
            this.m_MaxLevels = CollectionHelper.Log2Ceil(this.m_ReservedNodes);

            UnsafeUtility.Free(this.m_PathNodeBuffer, this.m_AllocatorLabel);
            this.m_PathNodeBuffer = (PathNode*)UnsafeUtility.Malloc(sizeof(PathNode) * this.m_MaxLevels, UnsafeUtility.AlignOf<PathNode>(), this.m_AllocatorLabel);
        }

        internal int GetNodeLevel(SkipListNode node)
        {
            int level = 1;
            int currentIdx = node.top;
            var currentNode = this.m_PointerBuffer[currentIdx];

            while(currentNode.downwards >= 0)
            {
                currentIdx = currentNode.downwards;
                currentNode = this.m_PointerBuffer[currentIdx];
                level++;
            }

            return level;
        }

        private void IncreaseCurrentLevel(int newPointerIdx, int distance)
        {
            var newHeaderIdx = this.FetchNewPointerIdx();
            var newHeader = this.m_PointerBuffer + newHeaderIdx;
            newHeader->downwards = this.m_Header;
            newHeader->forwards = newPointerIdx;
            newHeader->node = -1;
            newHeader->width = distance;

            this.m_Header = newHeaderIdx;
            this.m_CurrentLevels++;
        }

        private void StackNewLevels(int start, int end, int newNodeIdx, ref int topPtrIdx, int idx)
        {
            for (int i = start; i < end; i++)
            {
                int newIdx = this.FetchNewPointerIdx();

                var newPtr = this.m_PointerBuffer + newIdx;
                newPtr->forwards = -1;
                newPtr->downwards = topPtrIdx;
                newPtr->node = newNodeIdx;
                newPtr->width = 0;

                topPtrIdx = newIdx;

                this.IncreaseCurrentLevel(topPtrIdx, idx);
            }
        }

        internal void Insert(T value, out int newNodeIdx)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
#endif

            newNodeIdx = this.FetchNewNodeIdx();
            var newNode = this.m_NodeBuffer + newNodeIdx;
            newNode->element = value;

            int nodeLevel = this.GetRandomLevel();

            int topPtrIdx = -1;
            if (this.m_Length == 0)
            {
                this.StackNewLevels(0, nodeLevel, newNodeIdx, ref topPtrIdx, 1);
            }
            else
            {
                var iterationIdx = this.m_Header;
                var iterationPtr = this.m_PointerBuffer + iterationIdx;

                int currentIdx = 0;
                int lastComparedNode = -1;

                for (int i = this.m_CurrentLevels - 1; i >= 0; i--)
                {
                    if (i < nodeLevel)
                    {
                        this.m_PathNodeBuffer[i] = new PathNode(currentIdx, iterationIdx);
                    }

                    while (iterationPtr->forwards >= 0)
                    {
                        int forwardIdx = iterationPtr->forwards;
                        var forwardPtr = this.m_PointerBuffer + forwardIdx;
                        var node = this.m_NodeBuffer + forwardPtr->node;

                        if (this.comparer.Compare(node->element, value) >= 0)
                        {
                            lastComparedNode = forwardPtr->node;
                            iterationPtr->width++;
                            this.m_PointerBuffer[iterationIdx] = *iterationPtr;
                            break;
                        }

                        currentIdx += iterationPtr->width;
                        if (i < nodeLevel)
                        {
                            this.m_PathNodeBuffer[i] = new PathNode(currentIdx, forwardIdx);
                        }

                        iterationIdx = forwardIdx;
                        iterationPtr = forwardPtr;
                    }

                    if (iterationPtr->downwards >= 0)
                    {
                        iterationIdx = iterationPtr->downwards;
                        iterationPtr = this.m_PointerBuffer + iterationIdx;
                    }
                }

                currentIdx++;
                int min = math.min(this.m_CurrentLevels, nodeLevel);
                for (int i = 0; i < min; i++)
                {
                    int newIdx = this.FetchNewPointerIdx();

                    var newPtr = this.m_PointerBuffer + newIdx;

                    var pathNode = this.m_PathNodeBuffer[i];
                    var prevPointer = this.m_PointerBuffer[pathNode.pointer];
                    int prevIdx = pathNode.currentIdx;

                    int prevForward = prevPointer.forwards;
                    int prevWidth = currentIdx - prevIdx;
                    int nextWidth = prevPointer.width - prevWidth;

                    prevPointer.width = prevWidth;
                    prevPointer.forwards = newIdx;

                    newPtr->forwards = prevForward;
                    newPtr->downwards = topPtrIdx;
                    newPtr->node = newNodeIdx;
                    //If the previous width was 0 (because it pointed to the end of the list), we would get negative values
                    //To really make sure this never becomes a problem (it was not in the tests), I clamp it to positive here
                    newPtr->width = math.max(nextWidth, 0);

                    this.m_PointerBuffer[pathNode.pointer] = prevPointer;

                    topPtrIdx = newIdx;
                }

                if (nodeLevel > this.m_CurrentLevels)
                {
                    this.StackNewLevels(this.m_CurrentLevels, nodeLevel, newNodeIdx, ref topPtrIdx, currentIdx);
                }

            }

            newNode->top = topPtrIdx;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.m_MaxIndex = this.m_Length;
#endif
            this.m_Length++;
        }


        public void Insert(T value)
        {
            this.Insert(value, out _);
        }

        private void ReturnFreePointerIdx(int idx)
        {
            if(this.m_UsedPointerSlots >= this.m_ReservedPointerSlots)
            {
                this.ResizePointerSlots();
            }

            this.m_FreePointerSlots[this.m_UsedPointerSlots] = idx;
            this.m_UsedPointerSlots++;
        }

        private void ReturnFreeNodeIdx(int idx)
        {
            if(this.m_UsedNodeSlots >= this.m_ReservedNodeSlots)
            {
                this.ResizeNodeSlots();
            }
            this.m_FreeNodeSlots[this.m_UsedNodeSlots] = idx;
            this.m_UsedNodeSlots++;
        }

        private void RemovePointer(int removeBufferIdx, SkipListPointer toRemove, int prevBufferIdx, SkipListPointer previous)
        {
            previous.forwards = toRemove.forwards;
            previous.width += toRemove.width;
            previous.width--;

            this.m_PointerBuffer[prevBufferIdx] = previous;

            this.ReturnFreePointerIdx(removeBufferIdx);
        }

        private void RemoveNode(int nodeBufferIdx)
        {
            this.ReturnFreeNodeIdx(nodeBufferIdx);
        }

        public bool Remove(T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
#endif
            if (this.m_Length == 0) return false;

            var iterationIdx = this.m_Header;
            var iterationPtr = this.m_PointerBuffer + iterationIdx;

            int currentIdx = 0;

            for (int i = this.m_CurrentLevels - 1; i >= 0; i--)
            {
                this.m_PathNodeBuffer[i] = new PathNode(currentIdx, iterationIdx);
                while (iterationPtr->forwards >= 0)
                {
                    int forwardIdx = iterationPtr->forwards;
                    var forwardPtr = this.m_PointerBuffer + forwardIdx;
                    var node = this.m_NodeBuffer + forwardPtr->node;
                    if (comparer.Compare(node->element, value) >= 0)
                    {
                        this.m_PointerBuffer[iterationIdx] = *iterationPtr;
                        break;
                    }
                    this.m_PathNodeBuffer[i] = new PathNode(currentIdx, forwardIdx);

                    currentIdx += iterationPtr->width;
                    iterationIdx = forwardIdx;
                    iterationPtr = forwardPtr;
                }

                if (iterationPtr->downwards >= 0)
                {
                    iterationIdx = iterationPtr->downwards;
                    iterationPtr = this.m_PointerBuffer + iterationIdx;
                }
            }

            var targetPtrIdx = iterationPtr->forwards;
            if (targetPtrIdx < 0) return false;
            var targetPtr = this.m_PointerBuffer[targetPtrIdx];
            var nodeToRemove = this.m_NodeBuffer[targetPtr.node];

            if (comparer.Compare(nodeToRemove.element, value) != 0) return false;

            int levels = this.GetNodeLevel(nodeToRemove);
            for (int i = 0; i < levels; i++)
            {
                var pathNode = this.m_PathNodeBuffer[i];
                var prevPointer = this.m_PointerBuffer[pathNode.pointer];
                int prevIdx = pathNode.currentIdx;

                var forwardsPointer = this.m_PointerBuffer[prevPointer.forwards];

                this.RemovePointer(prevPointer.forwards, forwardsPointer, pathNode.pointer, prevPointer);
            }
            this.RemoveNode(targetPtr.node);

            //All nodes on the path "higher" than the removed node need their width to be reduced by 1 as well
            for (int i = levels; i < this.m_CurrentLevels; i++)
            {
                var pathNode = this.m_PathNodeBuffer + i;
                var pathPointer = this.m_PointerBuffer + pathNode->pointer;
                pathPointer->width--;

            }

            //Reduce current levels if we removed the last remaining node of height "levels"
            if (levels == this.m_CurrentLevels)
            {
                var headerPtr = this.m_PointerBuffer + this.m_Header;
                while (headerPtr->forwards < 0)
                {
                    this.m_Header = headerPtr->downwards;
                    this.m_CurrentLevels--;
                    if (this.m_Header < 0) break;
                    headerPtr = this.m_PointerBuffer + this.m_Header;
                }
            }

            this.m_Length--;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.m_MaxIndex = this.m_Length - 1;
#endif

            return true;
        }


        [BurstDiscard]
        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        [BurstDiscard]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        [BurstDiscard]
        public T[] ToArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            if (this.m_Length == 0) return null;

            var arr = new T[this.m_Length];
            int bottomHeaderIdx = this.m_Header;
            var headerPtr = this.m_PointerBuffer[bottomHeaderIdx];
            while(headerPtr.downwards >= 0)
            {
                headerPtr = this.m_PointerBuffer[headerPtr.downwards];
            }

            int idx = 0;
            var currentPtr = headerPtr;

            do
            {
                currentPtr = this.m_PointerBuffer[currentPtr.forwards];

                var node = this.m_NodeBuffer[currentPtr.node];
                arr[idx] = node.element;
                idx++;

            } while (currentPtr.forwards >= 0);

            return arr;
        }

        public bool IsCreated
        {
            get
            {
                return this.m_ReservedNodes > 0;
            }
        }




        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref this.m_Safety, ref this.m_DisposeSentinel);

            this.m_MinIndex = 0;
            this.m_MaxIndex = -1;
#endif

            this.m_Length = 0;
            this.m_Header = -1;

            this.m_ReservedNodes = -1;
            this.m_ReservedPointers = -1;
            this.m_ReservedNodeSlots = -1;
            this.m_ReservedPointerSlots = -1;

            this.m_UsedNodes = -1;
            this.m_UsedPointers = -1;
            this.m_UsedNodeSlots = -1;
            this.m_UsedPointerSlots = -1;

            UnsafeUtility.Free(this.m_NodeBuffer, this.m_AllocatorLabel);
            UnsafeUtility.Free(this.m_PointerBuffer, this.m_AllocatorLabel);

            UnsafeUtility.Free(this.m_FreeNodeSlots, this.m_AllocatorLabel);
            UnsafeUtility.Free(this.m_FreePointerSlots, this.m_AllocatorLabel);

            UnsafeUtility.Free(this.m_PathNodeBuffer, this.m_AllocatorLabel);
        }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void FailOutOfRangeError(int index)
        {
            if (index < this.m_Length && (this.m_MinIndex != 0 && this.m_MaxIndex != this.m_Length - 1))
            {
                throw new IndexOutOfRangeException(string.Format(
                    "Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n" +
                    "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                    "You can use double buffering strategies to avoid race conditions due to " +
                    "reading & writing in parallel to the same elements from a job.",
                    index, this.m_MinIndex, this.m_MaxIndex));
            }
            throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, this.m_Length));
        }



#endif
    }
}
