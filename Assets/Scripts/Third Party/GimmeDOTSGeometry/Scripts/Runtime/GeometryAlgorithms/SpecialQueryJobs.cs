using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Collections.NativeSortExtension;

namespace GimmeDOTSGeometry
{

    public static class SpecialQueryJobs
    {


        public struct ShapeEventPoint
        {
            public float2 pos;

            public int idx;
        }


        private struct ShapeEventPointComparer : IComparer<ShapeEventPoint>
        {
            public int Compare(ShapeEventPoint x, ShapeEventPoint y)
            {
                return x.pos.y.CompareTo(y.pos.y);

            }
        }

        private struct SearchStatus
        {
            public float x;

            public int idx;
        }

        private struct SearchStatusComparer : IComparer<SearchStatus>
        {
            public int Compare(SearchStatus x, SearchStatus y)
            {
                
                int cmp = x.x.CompareTo(y.x);
                if (cmp == 0) return x.idx.CompareTo(y.idx);
                return cmp;
            }
        }

        [BurstCompile]
        public unsafe struct UpdatePresortedRadiusEventQueueJob : IJobParallelFor
        {
            public float radius;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeArray<ShapeEventPoint> radiusEventQueue;


            public void Execute(int index)
            {

                var evt = this.radiusEventQueue[index];
                int idx = evt.idx;

                if (idx >= 0)
                {
                    var point = this.points[idx];
                    evt.pos = point;
                }
                else
                {
                    var point = this.points[-idx - 1];
                    evt.pos = point + new float2(0, 1) * this.radius;
                }
                this.radiusEventQueue[index] = evt;
                
            }
        }


        [BurstCompile]
        public unsafe struct UpdatePresortedRectEventQueueJob : IJobParallelFor
        {

            public float height;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeArray<ShapeEventPoint> rectEventQueue;


            public void Execute(int index)
            {

                var evt = this.rectEventQueue[index];
                int idx = evt.idx;

                if (idx >= 0)
                {
                    var point = this.points[idx];
                    evt.pos = point;
                }
                else
                {
                    var point = this.points[-idx - 1];
                    evt.pos = point + new float2(0, 0.5f) * this.height;
                }
                this.rectEventQueue[index] = evt;

            }
        }

        [BurstCompile]
        public unsafe struct SortPresortedEventQueueJob : IJob
        {
            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<ShapeEventPoint> eventQueue;


            public void Execute()
            {
                NativeSorting.InsertionSort((ShapeEventPoint*)this.eventQueue.GetUnsafePtr(), 0, this.eventQueue.Length, new ShapeEventPointComparer());
            }
        }

        [BurstCompile]
        public unsafe struct SortPresortedEventQueueParallelJob : IJobParallelFor
        {

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<ShapeEventPoint> eventQueue;

            public int batchSize;
            public int offset;

            public void Execute(int index)
            {
                int start, end;
                if (index == 0)
                {
                    start = 0;
                    end = this.offset;
                    end = math.clamp(end, 0, this.eventQueue.Length);
                }
                else
                {
                    start = this.offset + index * this.batchSize;
                    end = this.offset + (index + 1) * this.batchSize;
                    end = math.clamp(end, 0, this.eventQueue.Length);
                }

                NativeSorting.InsertionSort((ShapeEventPoint*)this.eventQueue.GetUnsafePtr(), start, end, new ShapeEventPointComparer());
            }
        }

        [BurstCompile]
        public unsafe struct CreateRadiusEventQueueJob : IJob
        {
            public float radius;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeArray<ShapeEventPoint> radiusEventQueue;

            public void Execute()
            {

                for (int i = 0; i < this.points.Length; i++)
                {
                    var point = this.points[i];
                    var endPoint = point + new float2(0, 1) * this.radius;

                    this.radiusEventQueue[i * 2] = new ShapeEventPoint()
                    {
                        idx = i,
                        pos = point,
                    };

                    this.radiusEventQueue[i * 2 + 1] = new ShapeEventPoint()
                    {
                        idx = -i - 1,
                        pos = endPoint,
                    };
                }

                this.radiusEventQueue.Sort(new ShapeEventPointComparer());
            }
        }

        [BurstCompile]
        public unsafe struct CreateRectangleEventQueueJob : IJob
        {
            public float height;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeArray<ShapeEventPoint> rectEventQueue;

            public void Execute()
            {

                for (int i = 0; i < this.points.Length; i++)
                {
                    var point = this.points[i];
                    var endPoint = point + new float2(0, 0.5f) * this.height;

                    this.rectEventQueue[i * 2] = new ShapeEventPoint()
                    {
                        idx = i,
                        pos = point,
                    };

                    this.rectEventQueue[i * 2 + 1] = new ShapeEventPoint()
                    {
                        idx = -i - 1,
                        pos = endPoint,
                    };
                }

                this.rectEventQueue.Sort(new ShapeEventPointComparer());
            }
        }

        [BurstCompile]
        public unsafe struct AllRadiusQueryJob : IJob
        {
            public float radius;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeList<UnsafeList<int>> result;

            [NoAlias, ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<ShapeEventPoint> radiusEventQueue;


            public void Execute()
            {
                var resultPtr = (UnsafeList<int>*)this.result.GetUnsafePtr();
                for (int i = 0; i < this.result.Length; i++)
                {
                    var list = resultPtr + i;
                    list->Clear();
                }

                float radiusSq = this.radius * this.radius;

                var status = new NativeSortedList<SearchStatus, SearchStatusComparer>(new SearchStatusComparer(), Allocator.Temp);

                var searchStart = new SearchStatus()
                {
                    idx = -1,
                };
                var searchEnd = new SearchStatus()
                {
                    idx = -1,
                };


                for (int i = 0; i < this.radiusEventQueue.Length; i++)
                {
                    var evt = this.radiusEventQueue[i];
                    int pointIdx = evt.idx;
                    if (pointIdx < 0) pointIdx = -pointIdx - 1;
                    var point = this.points[pointIdx];
                    var searchStatus = new SearchStatus()
                    {
                        idx = pointIdx,
                        x = point.x
                    };

                    if (evt.idx >= 0)
                    {

                        var list0 = resultPtr + evt.idx;

                        searchStart.x = point.x - this.radius;
                        searchEnd.x = point.x + this.radius;

                        var prevStartPtr = status.GetClosestPointer(searchStart);
                        var prevEndPtr = status.GetClosestPointer(searchEnd);

                        var iterPtr = prevStartPtr;
                        while(iterPtr != prevEndPtr)
                        {
                            int nextPtrIdx = iterPtr->forwards;
                            iterPtr = status.m_PointerBuffer + nextPtrIdx;

                            int elementIdx = status.m_NodeBuffer[iterPtr->node].element.idx;
                            var statusPoint = this.points[elementIdx];
                            if (math.distancesq(statusPoint, point) <= radiusSq)
                            {
                                var list1 = resultPtr + elementIdx;
                                list0->Add(elementIdx);
                                list1->Add(evt.idx);
                            }
                        }

                        status.Insert(searchStatus);
                    }
                    else
                    {
                        status.Remove(searchStatus);
                    }
                }
            }

        }

        [BurstCompile]
        public unsafe struct PrepareParallelQueryJob : IJob
        {
            [NoAlias]
            public NativeList<UnsafeList<int>> result;

            [NoAlias]
            public NativeList<UnsafeList<int>.ParallelWriter> writerList;

            public int targetCapacity;

            public void Execute()
            {
                var pointer = (UnsafeList<int>*)this.result.GetUnsafePtr();
                for(int i = 0; i < this.result.Length; i++)
                {
                    var list = pointer + i;
                    if(list->Capacity < this.targetCapacity)
                    {
                        list->Capacity = this.targetCapacity;
                    }
                    list->Clear();
                    this.writerList.Add(list->AsParallelWriter());
                }
            }
        }

        [BurstCompile]
        public unsafe struct AllRadiusParallelQueryJob : IJobParallelFor
        {
            public float radius;

            public int batchSize;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            //Who needs parallel multi hashmaps anyway, am I right?
            //(Kind of going a little bit beyond the intention of the Jobs System I think ^^)
            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<UnsafeList<int>.ParallelWriter> result;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<ShapeEventPoint> radiusEventQueue;

            public void Execute(int index)
            {
                int startIndex = index * this.batchSize;
                int endIndex = math.min((index + 1) * this.batchSize, this.radiusEventQueue.Length);

                float radiusSq = this.radius * this.radius;

                var status = new NativeSortedList<SearchStatus, SearchStatusComparer>(new SearchStatusComparer(), Allocator.Temp);

                var searchStart = new SearchStatus()
                {
                    idx = -1,
                };
                var searchEnd = new SearchStatus()
                {
                    idx = -1,
                };

                var startEvt = this.radiusEventQueue[startIndex];

                var statusSearchStart = startEvt;
                startEvt.pos.y -= this.radius;

                //Apparently, binary searching is not a ReadOnly operation...
                var statusStart = this.radiusEventQueue.BinarySearch(startEvt, new ShapeEventPointComparer());
                if (statusStart < 0) statusStart = ~statusStart;

                //We rebuild the status here that we would have had if we did it sequentially
                for (int i = statusStart; i < startIndex; i++)
                {
                    var evt = this.radiusEventQueue[i];
                    int pointIdx = evt.idx;
                    if (pointIdx < 0) pointIdx = -pointIdx - 1;
                    var point = this.points[pointIdx];
                    var searchStatus = new SearchStatus()
                    {
                        idx = pointIdx,
                        x = point.x
                    };
                    if (evt.idx >= 0)
                    {
                        status.Insert(searchStatus);
                    }
                }

                var resultPtr = (UnsafeList<int>.ParallelWriter*)this.result.GetUnsafePtr();

                for (int i = startIndex; i < endIndex; i++)
                {
                    var evt = this.radiusEventQueue[i];
                    int pointIdx = evt.idx;
                    if (pointIdx < 0) pointIdx = -pointIdx - 1;
                    var point = this.points[pointIdx];
                    var searchStatus = new SearchStatus()
                    {
                        idx = pointIdx,
                        x = point.x
                    };

                    if (evt.idx >= 0)
                    {
                        var list0 = resultPtr + evt.idx;

                        searchStart.x = point.x - this.radius;
                        searchEnd.x = point.x + this.radius;

                        var prevStartPtr = status.GetClosestPointer(searchStart);
                        var prevEndPtr = status.GetClosestPointer(searchEnd);


                        var iterPtr = prevStartPtr;
                        while (iterPtr != prevEndPtr)
                        {
                            int nextPtrIdx = iterPtr->forwards;
                            iterPtr = status.m_PointerBuffer + nextPtrIdx;

                            var nodePtr = status.m_NodeBuffer + iterPtr->node;
                            int elementIdx = nodePtr->element.idx;
                            var statusPoint = this.points[elementIdx];
                            if (math.lengthsq(statusPoint - point) <= radiusSq)
                            {
                                var list1 = resultPtr + elementIdx;
                                list0->AddNoResize(elementIdx);
                                list1->AddNoResize(evt.idx);
                            }
                        }


                        status.Insert(searchStatus);
                    }
                    else
                    {
                        status.Remove(searchStatus);
                    }
                }
            }
        }

        [BurstCompile]
        public unsafe struct AllRectangleQueryJob : IJob
        {
            public Rect rect;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeList<UnsafeList<int>> result;


            [NoAlias, ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<ShapeEventPoint> rectEventQueue;


            public void Execute()
            {
                var resultPtr = (UnsafeList<int>*)this.result.GetUnsafePtr();
                for (int i = 0; i < this.result.Length; i++)
                {
                    var list = resultPtr + i;
                    list->Clear();
                }

                float width = this.rect.width;
                float halfWidth = width * 0.5f;

                var status = new NativeSortedList<SearchStatus, SearchStatusComparer>(new SearchStatusComparer(), Allocator.Temp);

                var searchStart = new SearchStatus()
                {
                    idx = -1,
                };
                var searchEnd = new SearchStatus()
                {
                    idx = -1,
                };

                for (int i = 0; i < this.rectEventQueue.Length; i++)
                {
                    var evt = this.rectEventQueue[i];
                    int pointIdx = evt.idx;
                    if (pointIdx < 0) pointIdx = -pointIdx - 1;
                    var point = this.points[pointIdx];
                    var searchStatus = new SearchStatus()
                    {
                        idx = pointIdx,
                        x = point.x
                    };

                    if (evt.idx >= 0)
                    {

                        var list0 = resultPtr + evt.idx;

                        searchStart.x = point.x - halfWidth;
                        searchEnd.x = point.x + halfWidth;

                        var prevStartPtr = status.GetClosestPointer(searchStart);
                        var prevEndPtr = status.GetClosestPointer(searchEnd);

                        var iterPtr = prevStartPtr;
                        while (iterPtr != prevEndPtr)
                        {
                            int nextPtrIdx = iterPtr->forwards;
                            iterPtr = &status.m_PointerBuffer[nextPtrIdx];

                            int elementIdx = status.m_NodeBuffer[iterPtr->node].element.idx;
                            var statusPoint = this.points[elementIdx];

                            var list1 = resultPtr + elementIdx;

                            list0->Add(elementIdx);
                            list1->Add(evt.idx);
                        }

                        status.Insert(searchStatus);
                    }
                    else
                    {
                        status.Remove(searchStatus);
                    }
                }
            }
        }


        [BurstCompile]
        public unsafe struct AllRectangleParallelQueryJob : IJobParallelFor
        {

            public Rect allRectangle;

            public int batchSize;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            //Who needs parallel multi hashmaps anyway, am I right?
            //(Kind of going a little bit beyond the intention of the Jobs System I think ^^)
            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<UnsafeList<int>.ParallelWriter> result;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<ShapeEventPoint> rectEventQueue;

            public void Execute(int index)
            {
                float width = this.allRectangle.width;
                float height = this.allRectangle.height;

                int startIndex = index * this.batchSize;
                int endIndex = math.min((index + 1) * this.batchSize, this.rectEventQueue.Length);

                var status = new NativeSortedList<SearchStatus, SearchStatusComparer>(new SearchStatusComparer(), Allocator.Temp);

                var searchStart = new SearchStatus()
                {
                    idx = -1,
                };
                var searchEnd = new SearchStatus()
                {
                    idx = -1,
                };

                var startEvt = this.rectEventQueue[startIndex];

                var statusSearchStart = startEvt;
                startEvt.pos.y -= height * 0.5f;

                //Apparently, binary searching is not a ReadOnly operation...
                var statusStart = this.rectEventQueue.BinarySearch(startEvt, new ShapeEventPointComparer());
                if (statusStart < 0) statusStart = ~statusStart;

                //We rebuild the status here that we would have had if we did it sequentially
                for (int i = statusStart; i < startIndex; i++)
                {
                    var evt = this.rectEventQueue[i];
                    int pointIdx = evt.idx;
                    if (pointIdx < 0) pointIdx = -pointIdx - 1;
                    var point = this.points[pointIdx];
                    var searchStatus = new SearchStatus()
                    {
                        idx = pointIdx,
                        x = point.x
                    };
                    if (evt.idx >= 0)
                    {
                        status.Insert(searchStatus);
                    }
                }

                float halfWidth = width * 0.5f;
                var resultPtr = (UnsafeList<int>.ParallelWriter*)this.result.GetUnsafePtr();
                for (int i = startIndex; i < endIndex; i++)
                {
                    var evt = this.rectEventQueue[i];
                    int pointIdx = evt.idx;
                    if (pointIdx < 0) pointIdx = -pointIdx - 1;
                    var point = this.points[pointIdx];
                    var searchStatus = new SearchStatus()
                    {
                        idx = pointIdx,
                        x = point.x
                    };

                    if (evt.idx >= 0)
                    {
                        var list0 = resultPtr + evt.idx;

                        searchStart.x = point.x - halfWidth;
                        searchEnd.x = point.x + halfWidth;

                        var prevStartPtr = status.GetClosestPointer(searchStart);
                        var prevEndPtr = status.GetClosestPointer(searchEnd);

                        var iterPtr = prevStartPtr;
                        while (iterPtr != prevEndPtr)
                        {
                            int nextPtrIdx = iterPtr->forwards;
                            iterPtr = &status.m_PointerBuffer[nextPtrIdx];

                            int elementIdx = status.m_NodeBuffer[iterPtr->node].element.idx;
                            var statusPoint = this.points[elementIdx];

                            var list1 = resultPtr + elementIdx;

                            list0->AddNoResize(elementIdx);
                            list1->AddNoResize(evt.idx);
                            
                        }

                        status.Insert(searchStatus);
                    }
                    else
                    {
                        status.Remove(searchStatus);
                    }
                }
            }
        }

    }
}

