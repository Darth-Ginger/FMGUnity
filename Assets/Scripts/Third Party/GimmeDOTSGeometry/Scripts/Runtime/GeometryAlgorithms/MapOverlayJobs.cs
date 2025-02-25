using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class MapOverlayJobs
    {
        public struct SegmentTreeCode
        {
            public LS2D segment;
            public float nodeCode;
            public int elementIdx;
        }

        public struct LS2D
        {
            public float2 a;
            public float2 b;

            public int edgePtr;
        }

        public struct EventPoint
        {
            public float2 point;

            public int id;

            public UnsafeList<int> segments;

            public override int GetHashCode()
            {
                return id;
            }

            public override string ToString()
            {
                string format = "{0:0.00}";
                return $"{string.Format(format, this.point.x)}, {string.Format(format, this.point.y)}";
            }
        }
        
        public struct EventPointComparer : IComparer<EventPoint>
        {
            public float epsilon;

            public int Compare(EventPoint a, EventPoint b)
            {
                float2 diff = a.point - b.point;
                if (math.abs(diff.y) > this.epsilon) return (int)math.sign(diff.y);
                else return (diff.x > this.epsilon ? 1 : 0) - (diff.x < -this.epsilon ? 1 : 0);
            }
        }

        public struct SweepLineComparer : IComparer<LS2D>
        {
            public float epsilon;

            public float2 sweepPoint;


            public int Compare(LS2D l0, LS2D l1)
            {
                float p0X, p1X;

                float2 dirL0 = l0.b - l0.a;
                float2 dirL1 = l1.b - l1.a;

                if (Hint.Unlikely(math.abs(dirL0.y) < this.epsilon))
                {
                    p0X = math.min(l0.a.x, l0.b.x);
                    if (math.abs(this.sweepPoint.y - l0.a.y) < this.epsilon)
                    {
                        float maxL0 = math.max(l0.a.x, l0.b.x);
                        //We shouldn't go too far backwards so to speak on a horizontal line
                        if (this.sweepPoint.x > p0X && this.sweepPoint.x < maxL0) p0X = this.sweepPoint.x;
                    }
                }
                else
                {
                    float t0 = (this.sweepPoint.y + this.epsilon - l0.a.y) / dirL0.y;
                    p0X = l0.a.x + dirL0.x * t0;
                }

                if (Hint.Unlikely(math.abs(dirL1.y) < this.epsilon))
                {
                    p1X = math.min(l1.a.x, l1.b.x);
                    if (math.abs(this.sweepPoint.y - l1.a.y) < this.epsilon)
                    {
                        float maxL1 = math.max(l1.a.x, l1.b.x);
                        //We shouldn't go too far backwards so to speak on a horizontal line
                        if (this.sweepPoint.x > p1X && this.sweepPoint.x < maxL1) p1X = this.sweepPoint.x;
                    }
                }
                else
                {
                    float t1 = (this.sweepPoint.y + this.epsilon - l1.a.y) / dirL1.y;
                    p1X = l1.a.x + dirL1.x * t1;
                }

                float diffX = p0X - p1X;
                if (Hint.Likely(math.abs(diffX) > this.epsilon))
                {
                    return (int)math.sign(diffX);
                }
                else
                {
                    var dir0 = math.normalize(dirL0);
                    var dir1 = math.normalize(dirL1);
                    return (int)math.sign(dir0.x - dir1.x);
                }
            }
        }


        [BurstCompile]
        public struct MapOverlayMergeJob : IJob
        {
            [NoAlias, ReadOnly]
            public DCEL dcelA;
            [NoAlias, ReadOnly]
            public DCEL dcelB;

            [NoAlias, ReadOnly]
            public DCEL mergedDCEL;

            public void Execute()
            {
                DCEL.Merge(this.dcelA, this.dcelB, ref this.mergedDCEL);
            }
        }

        [BurstCompile]
        public struct MapOverlayDCELSegmentsJob : IJob
        {
            [NoAlias, ReadOnly]
            public DCEL mergedDCEL;

            [NoAlias, WriteOnly]
            public NativeList<LS2D> lineSegments;

            public void Execute()
            {
                
                NativeParallelHashSet<int> edgeHashes = new NativeParallelHashSet<int>(16, Allocator.Temp);

                var edges = this.mergedDCEL.edges;
                var vertices = this.mergedDCEL.vertices;
                for(int i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    int hash = MathUtilDOTS.EdgeToHash(edge.vertexBack, edge.vertexFwd);
                    if(!edgeHashes.Contains(hash))
                    {
                        var vertexA = vertices[edge.vertexBack];
                        var vertexB = vertices[edge.vertexFwd];

                        if(vertexA.y > vertexB.y || (vertexA.y == vertexB.y && vertexA.x > vertexB.x))
                        {
                            AlgoUtil.Swap(ref vertexA, ref vertexB);
                        }

                        var ls2D = new LS2D()
                        {
                            a = vertexA,
                            b = vertexB,
                            edgePtr = i,
                        };

                        edgeHashes.Add(hash);
                        this.lineSegments.Add(ls2D);
                    }
                }
            }
        }

        [BurstCompile]
        public struct MapOverlaySweepJob : IJob
        {

            public float epsilon;

            [NoAlias]
            public NativeList<LS2D> lineSegments;

            private void InsertSegment(LS2D segment, int id, ref NativeAVLTree<EventPoint, EventPointComparer> eventQueue)
            {
                var lowerEventPoint = new EventPoint()
                {
                    id = id,
                    point = segment.a,
                };
                var upperEventPoint = new EventPoint()
                {
                    id = -1,
                    point = segment.b,
                };

                if(!eventQueue.Contains(lowerEventPoint))
                {
                    lowerEventPoint.segments.Add(id);
                    eventQueue.Insert(lowerEventPoint);
                }
                else
                {
                    int idx = eventQueue.GetElementIdx(lowerEventPoint);
                    var elements = eventQueue.Elements;
                    var elem = elements[idx];
                    var ls2D = elem.Value;
                    if(ls2D.id < 0)
                    {
                        ls2D.id = id;
                    }
                    ls2D.segments.Add(id);
                    elem.Value = ls2D;
                    elements[idx] = elem;
                }

                if(!eventQueue.Contains(upperEventPoint))
                {
                    eventQueue.Insert(upperEventPoint);
                }
            }

            public EventPoint FetchNextEvent(ref NativeAVLTree<EventPoint, EventPointComparer> eventQueue)
            {
                int leftmostNode = eventQueue.GetLeftmostNode();
                var nextEvent = eventQueue.Elements[leftmostNode].Value;

                eventQueue.RemoveNode(leftmostNode);

                return nextEvent;
            }

            public void Execute()
            {

                var sweepComparer = new SweepLineComparer() { epsilon = this.epsilon };
                var eventComparer = new EventPointComparer() { epsilon = this.epsilon * 0.1f };

                var eventQueue = new NativeAVLTree<EventPoint, EventPointComparer>(eventComparer, Allocator.Temp);
                var status = new NativeAVLTree<LS2D, SweepLineComparer>(sweepComparer, Allocator.Temp);

                var upperSegments = new NativeList<LS2D>(Allocator.Temp);
                var innerSegments = new NativeList<LS2D>(Allocator.Temp);

                var treePositions = new NativeList<SegmentTreeCode>(Allocator.Temp);

                for(int i = 0; i < this.lineSegments.Length; i++)
                {
                    this.InsertSegment(this.lineSegments[i], i, ref eventQueue);
                }

                while(!eventQueue.IsEmpty())
                {
                    
                }
            }
        }

        

    }
}
