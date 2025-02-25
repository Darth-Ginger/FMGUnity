using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Polygon2DTriangulationJobs
    {
        [BurstCompile]
        public struct FanTriangulationJob : IJob
        {
            public bool clockwiseWinding;

            [ReadOnly, NoAlias]
            public UnsafeList<float2> polyPoints;

            [ReadOnly, NoAlias]
            public UnsafeList<int> separators;

            [WriteOnly, NoAlias]
            public NativeList<int> triangles;

            public void Execute()
            {
                if (Hint.Likely(this.polyPoints.Length >= 3))
                {
                    int length = this.polyPoints.Length - 1;

                    if (Hint.Likely(this.clockwiseWinding))
                    {
                        for (int i = 1; i < length; i++)
                        {
                            this.triangles.Add(i + 1);
                            this.triangles.Add(i);
                            this.triangles.Add(0);
                        }
                    }
                    else
                    {
                        for (int i = 1; i < length; i++)
                        {
                            this.triangles.Add(0);
                            this.triangles.Add(i);
                            this.triangles.Add(i + 1);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        //Great paper: https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
        public struct EarClippingTriangulationJob : IJob
        {
            public bool clockwiseWinding;

            [ReadOnly, NoAlias]
            public UnsafeList<float2> polyPoints;

            [WriteOnly, NoAlias]
            public NativeList<int> triangles;

            private bool EarTest(NativeTriangle2D triangle, int3 triangleIndices, NativeList<int> reflexVertices)
            {
                
                var triangleBounds = triangle.GetBounds();
                for (int i = 0; i < reflexVertices.Length; i++)
                {
                    int idx = reflexVertices[i];
                    float2 reflexPoint = this.polyPoints[idx];

                    bool pointInside = math.all(idx != triangleIndices) 
                        && triangleBounds.Contains(reflexPoint) //Simpler test than triangles! Improves performance by 20%
                        && triangle.IsPointInside(reflexPoint);

                    if (pointInside) return false;
                }
                return true;
            }

            public void Execute()
            {

                this.triangles.Clear();
                if (Hint.Likely(this.polyPoints.Length >= 3))
                {
                    NativeList<int> currentVertices = new NativeList<int>(this.polyPoints.Length, Allocator.Temp);
                    NativeList<int> ears = new NativeList<int>(this.polyPoints.Length + 1, Allocator.Temp);

                    NativeList<int> reflexVertices = new NativeList<int>(this.polyPoints.Length, Allocator.Temp);

                    int length = this.polyPoints.Length;

                    float2 pointA = this.polyPoints[MathUtilDOTS.Mod(-1, length)];
                    float2 pointB = this.polyPoints[0];
                    float2 pointC;

                    for (int i = 0; i < length; i++)
                    {
                        currentVertices.AddNoResize(i);

                        pointC = this.polyPoints[(i + 1) % length];

                        if (VectorUtil.CompareLineDirection(pointA, pointB, pointC) > 0)
                        {
                            reflexVertices.Add(i);
                        }

                        pointA = pointB;
                        pointB = pointC;
                    }

                    NativeTriangle2D triangle = new NativeTriangle2D();

                    int pointAIdx = MathUtilDOTS.Mod(-1, length);
                    int pointBIdx = 0;
                    pointA = this.polyPoints[pointAIdx];
                    pointB = this.polyPoints[pointBIdx];

                    for (int i = 0; i < length; i++)
                    {
                        int pointCIdx = (i + 1) % length;
                        pointC = this.polyPoints[pointCIdx];

                        if (reflexVertices.BinarySearch(i) < 0)
                        {
                            triangle.a = pointA; triangle.b = pointB; triangle.c = pointC;
                            if (EarTest(triangle, new int3(pointAIdx, pointBIdx, pointCIdx), reflexVertices))
                            {
                                ears.Add(i);
                            }
                        }

                        pointA = pointB;
                        pointB = pointC;
                    }

                    int currentLength = currentVertices.Length;

                    NativeTriangle2D prevTriangle = new NativeTriangle2D();
                    NativeTriangle2D nextTriangle = new NativeTriangle2D();

                    //Runtime: The number of loops is at most n.
                    //
                    //         The number of ears is proportional to the number of triangles in total, which is proportional to the
                    //         vertices in total (=n). At the start, we have the most ears when triangulating a convex polygon. Namely
                    //         exactly n. The most expensive operation we are doing with ears is removing (n - because we have to copy stuff)
                    //         It is not possible to have more than n ears at the same time. So at most this is n²
                    //      
                    //         The number of reflex vertices also can at most be n - 2 (under the assumption that the polygon is simple
                    //         and does not contain self-intersections or holes). We have to loop through them when doing an ear test.
                    //         So at most this is n²
                    //
                    //         Therefore runtime is at most proportional to n² (but with real polygons that are used it has a very low
                    //         constant in front of it.

                    while (currentLength > 3 && ears.Length > 0)
                    {
                        int ear = ears[0];

                        //List is always sorted, as we always remove stuff, never add (and it was sorted to begin with)
                        //For smaller polygons .IndexOf is faster, but this is for scale ^^
                        int listIndex = currentVertices.BinarySearch(ear);
                        int prevIndex = MathUtilDOTS.Mod(listIndex - 1, currentLength);
                        int nextIndex = (listIndex + 1) % currentLength;

                        int prePrevIndex = MathUtilDOTS.Mod(listIndex - 2, currentLength);
                        int nextNextIndex = (listIndex + 2) % currentLength;

                        int vertex = currentVertices[listIndex];
                        int prevVertex = currentVertices[prevIndex];
                        int nextVertex = currentVertices[nextIndex];

                        int prePrevVertex = currentVertices[prePrevIndex];
                        int nextNextVertex = currentVertices[nextNextIndex];

                        float2 prePrevPoint = this.polyPoints[prePrevVertex];
                        float2 prevPoint = this.polyPoints[prevVertex];
                        float2 nextPoint = this.polyPoints[nextVertex];
                        float2 nextNextPoint = this.polyPoints[nextNextVertex];

                        if (this.clockwiseWinding)
                        {
                            this.triangles.Add(nextVertex);
                            this.triangles.Add(vertex);
                            this.triangles.Add(prevVertex);

                        }
                        else
                        {
                            this.triangles.Add(prevVertex);
                            this.triangles.Add(vertex);
                            this.triangles.Add(nextVertex);
                        }

                        prevTriangle.a = prePrevPoint; prevTriangle.b = prevPoint; prevTriangle.c = nextPoint;
                        nextTriangle.a = prevPoint; nextTriangle.b = nextPoint; nextTriangle.c = nextNextPoint;

                        ears.RemoveAt(0);
                        int searchIdx;
                        if ((searchIdx = ears.BinarySearch(prevVertex)) >= 0)
                        {
                            if (!this.EarTest(prevTriangle, new int3(prePrevVertex, prevVertex, nextVertex), reflexVertices))
                            {
                                ears.RemoveAt(searchIdx);
                            }
                        }
                        else if ((searchIdx = reflexVertices.BinarySearch(prevVertex)) >= 0)
                        {
                            if (VectorUtil.CompareLineDirection(prePrevPoint, prevPoint, nextPoint) <= 0)
                            {
                                reflexVertices.RemoveAt(searchIdx);
                                if (EarTest(prevTriangle, new int3(prePrevVertex, prevVertex, nextVertex), reflexVertices))
                                {
                                    searchIdx = (~ears.BinarySearch(prevVertex));
                                    ears.Insert(searchIdx, prevVertex);
                                }
                            }
                        }
                        else if (this.EarTest(prevTriangle, new int3(prePrevVertex, prevVertex, nextVertex), reflexVertices))
                        {
                            searchIdx = (~ears.BinarySearch(prevVertex));
                            ears.Insert(searchIdx, prevVertex);
                        }

                        if ((searchIdx = ears.BinarySearch(nextVertex)) >= 0)
                        {
                            if (!this.EarTest(nextTriangle, new int3(prevVertex, nextVertex, nextNextVertex), reflexVertices))
                            {
                                ears.RemoveAt(searchIdx);
                            }
                        }
                        else if ((searchIdx = reflexVertices.BinarySearch(nextVertex)) >= 0)
                        {
                            if (VectorUtil.CompareLineDirection(prevPoint, nextPoint, nextNextPoint) <= 0)
                            {
                                reflexVertices.RemoveAt(searchIdx);

                                if (EarTest(nextTriangle, new int3(prevVertex, nextVertex, nextNextVertex), reflexVertices))
                                {
                                    searchIdx = (~ears.BinarySearch(nextVertex));
                                    ears.Insert(searchIdx, nextVertex);
                                }
                            }
                        }
                        else if (this.EarTest(nextTriangle, new int3(prevVertex, nextVertex, nextNextVertex), reflexVertices))
                        {
                            searchIdx = (~ears.BinarySearch(nextVertex));
                            ears.Insert(searchIdx, nextVertex);
                        }

                        currentVertices.RemoveAt(listIndex);

                        currentLength--;
                    }

                    for (int i = 0; i < currentVertices.Length; i++)
                    {
                        int idx = clockwiseWinding ? (currentVertices.Length - 1 - i) : i;
                        this.triangles.Add(currentVertices[idx]);
                    }
                }
            }
        }


        private enum MonotoneVertexType : byte
        {
            REGULAR = 0,
            START = 1,
            END = 2,
            SPLIT = 3,
            MERGE = 4
        }

        private struct MonotoneVertexInfo
        {
            public int idx;
            public float2 pos;
            public MonotoneVertexType type;
        }

        private struct MonotoneEdgeInfo
        {
            public int helper;

            public int previous;
            public int next;

            public int origin;
        }

        private struct MonotoneVertexInfoComparer : IComparer<MonotoneVertexInfo>
        {
            public float epsilon;

            public int Compare(MonotoneVertexInfo a, MonotoneVertexInfo b)
            {
                float2 diff = a.pos - b.pos;
                if(math.abs(diff.y) > this.epsilon)
                {
                    return (int)math.sign(diff.y);
                } else
                {
                    return -(int)math.sign(diff.x);
                }
            }
        }

        private struct MonotoneEdgeInfoComparer : IComparer<MonotoneEdgeInfo>
        {
            public float epsilon;

            public float2 sweepPoint;

            [NoAlias] public NativeList<MonotoneVertexInfo> vertices;
            [NoAlias] public NativeList<MonotoneEdgeInfo> edges;

            public int Compare(MonotoneEdgeInfo a, MonotoneEdgeInfo b)
            {
                float y = this.sweepPoint.y;

                var originA = this.vertices[a.origin].pos;
                var originB = this.vertices[b.origin].pos;

                int targetIdxA = this.edges[a.next].origin;
                int targetIdxB = this.edges[b.next].origin;

                var targetA = this.vertices[targetIdxA].pos;
                var targetB = this.vertices[targetIdxB].pos;

                float2 diffA = targetA - originA;
                float2 diffB = targetB - originB;
                float p0X = diffA.x > 0 ? originA.x : targetA.x;
                float p1X = diffB.x > 0 ? originB.x : targetB.x;

                if (math.abs(diffA.y) > this.epsilon)
                {
                    float t0 = (y - originA.y) / diffA.y;
                    p0X = originA.x + t0 * diffA.x;
                }

                if (math.abs(diffB.y) > this.epsilon)
                {
                    float t1 = (y - originB.y) / diffB.y;
                    p1X = originB.x + t1 * diffB.x; 
                }

                return (int)math.sign(p0X - p1X);
            }
        }




        [BurstCompile(FloatPrecision = FloatPrecision.High)]
        public unsafe struct MonotoneDecompositionJob : IJob
        {

            public float epsilon;

            [ReadOnly, NoAlias]
            public UnsafeList<float2> polyPoints;
            [ReadOnly, NoAlias]
            public UnsafeList<int> polySeparators;

            [NoAlias]
            public NativeList<int> monotonePolyPointMapping;

            [NoAlias]
            public NativeList<int> monotonePolySeparators;

            private int GetEdgeArrayIndex(NativeList<MonotoneEdgeInfo> edges, MonotoneEdgeInfo edge)
            {
                return edges[edge.previous].next;
            }

            private int GetClosestEdgeToTheLeft(NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer> status,
                NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, MonotoneVertexInfo vertex)
            {
                if (status.Length == 0) return -1;

                int headerIdx = status.m_Header;
                var iterationPtr = status.m_PointerBuffer + headerIdx;

                for(int i = status.m_CurrentLevels; i > 0; i--)
                {
                    while (iterationPtr->forwards >= 0)
                    {
                        int forwardIdx = iterationPtr->forwards;
                        var forwardPtr = status.m_PointerBuffer + forwardIdx;
                        var node = status.m_NodeBuffer + forwardPtr->node;

                        var currentEdge = node->element;
                        var nextEdge = edges[currentEdge.next];

                        int idxA = currentEdge.origin;
                        int idxB = nextEdge.origin;

                        var vertexA = vertices[idxA].pos;
                        var vertexB = vertices[idxB].pos;

                        //The point is definitely somewhere between the y-values of the edge (that is how the algorithm works)
                        float t = math.unlerp(vertexA.y, vertexB.y, vertex.pos.y);
                        float2 sweepIntersection = vertexA + t * (vertexB - vertexA);

                        if (vertex.pos.x < sweepIntersection.x)
                        {
                            break;
                        }
                        iterationPtr = forwardPtr;
                    }

                    if (iterationPtr->downwards >= 0)
                    {
                        iterationPtr = status.m_PointerBuffer + iterationPtr->downwards;
                    }
                }

                int lastNodeIdx = iterationPtr->node;
                if(lastNodeIdx >= 0)
                {
                    var lastNode = status.m_NodeBuffer[lastNodeIdx];
                    return GetEdgeArrayIndex(edges, lastNode.element);
                }
                return -1;

                //Old AVL Tree Version
                /*
                var node = status.RootIdx;

                int leftMost = -1;
                if (node >= 0)
                {
                    leftMost = this.GetEdgeArrayIndex(edges, status.Elements[node].value);
                }

                while (node >= 0)
                {
                    var nodeElem = status.Elements[node];
                    var currentEdge = nodeElem.value;
                    var nextEdge = edges[currentEdge.next];

                    int idxA = currentEdge.origin;
                    int idxB = nextEdge.origin;

                    var vertexA = vertices[idxA].pos;
                    var vertexB = vertices[idxB].pos;

                    //The point is definitely somewhere between the y-values of the edge (that is how the algorithm works)
                    float t = Mathf.InverseLerp(vertexA.y, vertexB.y, vertex.pos.y);
                    Vector2 sweepIntersection = vertexA + t * (vertexB - vertexA);

                    if (vertex.pos.x < sweepIntersection.x)
                    {
                        var nextNode = nodeElem.left;
                        //Likely when the tree has height > 2... so most of the time
                        if (Hint.Likely(nextNode >= 0))
                        {
                            node = nextNode;
                            continue;
                        } else
                        {
                            return leftMost;
                        }
                    }
                    else
                    {
                        leftMost = this.GetEdgeArrayIndex(edges, currentEdge);
                        var nextNode = nodeElem.right;
                        //Likely when the tree has height > 2... so most of the time
                        if (Hint.Likely(nextNode >= 0))
                        {
                            node = nextNode;
                            continue;
                        } else
                        {
                            return leftMost;
                        }
                    }
                }
                //Should not be reachable
                return -1;
                */
            }

            private void AddEdge(NativeList<MonotoneEdgeInfo> diagonals, NativeList<MonotoneEdgeInfo> edges, int nextIdx, int idx)
            {
                int prevEdgeIdx = edges[idx].previous;

                var nextEdge = edges[nextIdx];
                int nextPrevEdgeIdx = nextEdge.previous;

                diagonals.Add(new MonotoneEdgeInfo()
                {
                    helper = -1,
                    origin = idx,
                    next = nextIdx,
                    previous = prevEdgeIdx
                });

                diagonals.Add(new MonotoneEdgeInfo()
                {
                    helper = -1,
                    origin = nextIdx,
                    next = idx,
                    previous = nextPrevEdgeIdx
                });
            }

            private void HandleStartVertex(ref NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer> status, NativeList<MonotoneEdgeInfo> edges, MonotoneVertexInfo vertex)
            {
                var edge = edges[vertex.idx];
                edge.helper = vertex.idx;
                edges[vertex.idx] = edge;

                status.Insert(edge);
            }

            private void HandleEndVertex(ref NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer> status, 
                NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, NativeList<MonotoneEdgeInfo> diagonals, MonotoneVertexInfo vertex)
            {
                var prevEdgeIdx = edges[vertex.idx].previous;
                var prevEdge = edges[prevEdgeIdx];

                int helper = prevEdge.helper;

                //Unlikely -- because the total number of merge vertices is less than half (far less actually)
                if (helper >= 0 && Hint.Unlikely(vertices[helper].type == MonotoneVertexType.MERGE))
                {
                    AddEdge(diagonals, edges, helper, vertex.idx);
                }
                status.Remove(prevEdge);
            }

            private void HandleSplitVertex(ref NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer> status,
                NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, NativeList<MonotoneEdgeInfo> diagonals, MonotoneVertexInfo vertex)
            {
                int edgeIdx = this.GetClosestEdgeToTheLeft(status, vertices, edges, vertex);

                //This might happen when there are self-intersections or wrong orderings -- but better to have a wrong triangulation than an exception
                if (Hint.Likely(edgeIdx >= 0))
                {
                    var edge = edges[edgeIdx];

                    int helper = edge.helper;

                    this.AddEdge(diagonals, edges, helper, vertex.idx);

                    edge.helper = vertex.idx;
                    edges[edgeIdx] = edge;
                }

                var vertexEdge = edges[vertex.idx];
                vertexEdge.helper = vertex.idx;
                edges[vertex.idx] = vertexEdge;

                status.Insert(vertexEdge);
            }

            private void HandleMergeVertex(ref NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer> status,
                NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, NativeList<MonotoneEdgeInfo> diagonals, MonotoneVertexInfo vertex)
            {
                int prevEdgeIdx = edges[vertex.idx].previous;
                var prevEdge = edges[prevEdgeIdx];

                int helper = prevEdge.helper;

                //Unlikely -- because the total number of merge vertices is less than half (far less actually)
                if (helper >= 0 && Hint.Unlikely(vertices[helper].type == MonotoneVertexType.MERGE))
                {
                    AddEdge(diagonals, edges, helper, vertex.idx);
                }

                status.Remove(prevEdge);
                var leftEdgeIdx = GetClosestEdgeToTheLeft(status, vertices, edges, vertex);

                //This might happen when there are self-intersections or wrong orderings -- but better to have a wrong triangulation than an exception
                if (Hint.Likely(leftEdgeIdx >= 0))
                {
                    var leftEdge = edges[leftEdgeIdx];

                    int leftHelper = leftEdge.helper;
                    //Unlikely -- because the total number of merge vertices is less than half (far less actually)
                    if (leftHelper >= 0 && Hint.Unlikely(vertices[leftHelper].type == MonotoneVertexType.MERGE))
                    {
                        AddEdge(diagonals, edges, leftHelper, vertex.idx);
                    }

                    leftEdge.helper = vertex.idx;
                    edges[leftEdgeIdx] = leftEdge;
                }
            }

            private void HandleRegularVertex(ref NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer> status,
                NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, NativeList<MonotoneEdgeInfo> diagonals, MonotoneVertexInfo vertex)
            {
                int prevEdgeIdx = edges[vertex.idx].previous;
                var prevEdge = edges[prevEdgeIdx];

                var point = vertex.pos;
                var prevPoint = vertices[prevEdgeIdx].pos;

                var dir = point - prevPoint;

                //Only true in counter-clockwise vertex ordering... but true nonetheless ^^
                if (dir.y < -this.epsilon || (math.abs(dir.y) < this.epsilon && dir.x > 0.0f))
                {
                    int helper = prevEdge.helper;

                    //Unlikely -- because the total number of merge vertices is less than half (far less actually)
                    if (helper >= 0 && Hint.Unlikely(vertices[helper].type == MonotoneVertexType.MERGE))
                    {
                        AddEdge(diagonals, edges, helper, vertex.idx);
                    }

                    status.Remove(prevEdge);

                    var edge = edges[vertex.idx];
                    edge.helper = vertex.idx;
                    edges[vertex.idx] = edge;

                    status.Insert(edge);

                } else
                {
                    int leftEdgeIdx = GetClosestEdgeToTheLeft(status, vertices, edges, vertex);
                    if (leftEdgeIdx >= 0)
                    {
                        var leftEdge = edges[leftEdgeIdx];
                        int leftHelper = leftEdge.helper;

                        //Unlikely -- because the total number of merge vertices is less than half (far less actually)
                        if (leftHelper >= 0 && Hint.Unlikely(vertices[leftHelper].type == MonotoneVertexType.MERGE))
                        {
                            AddEdge(diagonals, edges, leftHelper, vertex.idx);
                        }

                        leftEdge.helper = vertex.idx;
                        edges[leftEdgeIdx] = leftEdge;
                    }
                }
            }

            private void FillOutMonotoneBaseInfo(NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges)
            {

                int subsurface = 0;
                int prevSubsurfaceIdx = 0;
                int nextSubsurfaceIdx = this.polyPoints.Length;
                if (this.polySeparators.Length > 0)
                {
                    nextSubsurfaceIdx = this.polySeparators[subsurface];
                }
                int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                for (int i = 0; i < this.polyPoints.Length; i++)
                {
                    int idx = i - prevSubsurfaceIdx;
                    int prevIdx = prevSubsurfaceIdx + MathUtilDOTS.Mod(idx - 1, length);
                    int nextIdx = prevSubsurfaceIdx + MathUtilDOTS.Mod(idx + 1, length);

                    vertices.Add(new MonotoneVertexInfo()
                    {
                        idx = i,
                        pos = this.polyPoints[i],
                        type = MonotoneVertexType.REGULAR
                    });

                    var edge = new MonotoneEdgeInfo()
                    {
                        helper = -1,
                        next = nextIdx,
                        origin = i,
                        previous = prevIdx
                    };

                    //Holes have to be inverted
                    if (subsurface != 0)
                    {
                        edge.previous = nextIdx;
                        edge.next = prevIdx;
                    }

                    edges.Add(edge);

                    if (Hint.Unlikely(i >= nextSubsurfaceIdx - 1))
                    {
                        prevSubsurfaceIdx = nextSubsurfaceIdx;
                        subsurface++;
                        if (this.polySeparators.Length > subsurface)
                        {
                            nextSubsurfaceIdx = this.polySeparators[subsurface];
                        }
                        else
                        {
                            nextSubsurfaceIdx = this.polyPoints.Length;
                        }
                        length = nextSubsurfaceIdx - prevSubsurfaceIdx;
                    }
                }
            }

            private void FillOutMonotoneVertices(NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, NativePriorityQueue<MonotoneVertexInfo, MonotoneVertexInfoComparer> eventQueue)
            {
                int length = vertices.Length;

                for(int i = 0; i < length; i++)
                {
                    var vertex = vertices[i];
                    var edge = edges[i];
                    var nextEdge = edges[edge.next];
                    var prevEdge = edges[edge.previous];

                    var nextVertex = vertices[nextEdge.origin];
                    var prevVertex = vertices[prevEdge.origin];

                    int nextCmp = eventQueue.comparer.Compare(nextVertex, vertex);
                    int prevCmp = eventQueue.comparer.Compare(prevVertex, vertex);

                    if(nextCmp == prevCmp)
                    {
                        float2 nextDir = nextVertex.pos - vertex.pos;
                        float2 prevDir = prevVertex.pos - vertex.pos;

                        float angle = Vector2.SignedAngle(nextDir, prevDir);

                        //Both points below
                        if(nextCmp < 0)
                        {
                            if(angle >= 0.0f)
                            {
                                vertex.type = MonotoneVertexType.START;
                            } else
                            {
                                vertex.type = MonotoneVertexType.SPLIT;
                            }
                        //Both points above (or colinear)
                        } else
                        {
                            if(angle >= 0.0f)
                            {
                                vertex.type = MonotoneVertexType.END;
                            } else
                            {
                                vertex.type = MonotoneVertexType.MERGE;
                            }
                        }

                        vertices[i] = vertex;
                    }

                    eventQueue.Enqueue(vertices[i]);
                }
            }

            private int GetMinimumNext(float2 dir, float2 nextDir, NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges, int start, NativeParallelMultiHashMap<int, int> vertexDiagonals)
            {
                int minNext = -1;
                float minAngle = float.PositiveInfinity;

                float2 origin = vertices[start].pos;

                float angle = Vector2.SignedAngle(-dir, nextDir);
                angle = -angle;
                if(angle < 0.0f) { angle = 360.0f + angle; }
                if(angle < minAngle)
                {
                    minAngle = angle;
                }

                var enumerator = vertexDiagonals.GetValuesForKey(start);

                while(enumerator.MoveNext())
                {
                    int nextEdgeIdx = enumerator.Current;
                    int nextVertexIdx = edges[nextEdgeIdx].origin;

                    var next = vertices[nextVertexIdx].pos;

                    var diagonalDir = next - origin;

                    if(math.any(diagonalDir != -dir))
                    {
                        angle = Vector2.SignedAngle(-dir, diagonalDir);
                        angle = -angle;
                        if(angle < 0.0f) { angle = 360.0f + angle; }
                        if(angle < minAngle)
                        {
                            minAngle = angle;
                            minNext = nextEdgeIdx;
                        }
                    }
                }

                return minNext;
            }

            private void CreateMonotonePolygonsWithDiagonals(NativeList<MonotoneEdgeInfo> diagonals, NativeList<MonotoneVertexInfo> vertices, NativeList<MonotoneEdgeInfo> edges)
            {
                int circleCount = (diagonals.Length / 2) + 1;


                //-> Polygon was monotone already as there is no diagonal
                if(circleCount == 1)
                {
                    int vertexCount = vertices.Length;
                    for(int i = 0; i < vertexCount; i++)
                    {
                        this.monotonePolyPointMapping.Add(i);
                    }
                } else
                {
                    NativeParallelMultiHashMap<int, int> vertexDiagonals = new NativeParallelMultiHashMap<int, int>(diagonals.Length, Allocator.Temp);

                    //Used for preventing infinite loops for self-intersection polygons... if you're sure that you don't have them, you can remove it
                    NativeParallelHashSet<int> visited = new NativeParallelHashSet<int>(1, Allocator.Temp);

                    for(int i = 0; i < diagonals.Length; i++)
                    {
                        var diagonal = diagonals[i];
                        int vertex = diagonal.origin;
                        int nextEdgeIdx = diagonal.next;
                        int nextVertex = edges[nextEdgeIdx].origin;

                        vertexDiagonals.Add(vertex, nextVertex);
                    }

                    int counter = 0;
                    int diagonalCount = vertexDiagonals.Count();
                    while(diagonalCount != 0 && counter < diagonals.Length)
                    {
                        var diagonal = diagonals[counter];

                        int origin = diagonal.origin;

                        if (vertexDiagonals.ContainsKey(origin))
                        {
                            visited.Clear();

                            int currentVertex = origin;

                            var edge = edges[currentVertex];

                            int prevVertexIdx = edge.previous;
                            float2 prevPoint = vertices[prevVertexIdx].pos;
                            float2 point = vertices[currentVertex].pos;

                            this.monotonePolyPointMapping.Add(currentVertex);

                            while(edge.next != origin)
                            {
                                if(vertexDiagonals.ContainsKey(currentVertex))
                                {
                                    var dir = point - prevPoint;
                                    var nextPoint = vertices[edge.next].pos;
                                    var nextDir = nextPoint - point;

                                    int minNext = this.GetMinimumNext(dir, nextDir, vertices, edges, currentVertex, vertexDiagonals);

                                    if(minNext >= 0)
                                    {
                                        vertexDiagonals.Remove(currentVertex, minNext);
                                        diagonalCount--;
                                        if(!vertexDiagonals.TryGetFirstValue(currentVertex, out _, out _))
                                        {
                                            vertexDiagonals.Remove(currentVertex);
                                        }
                                        currentVertex = minNext;
                                        if (currentVertex == origin) { break; }

                                    } else
                                    {
                                        currentVertex = edge.next;
                                    }
                                } else
                                {
                                    currentVertex = edge.next;
                                }

                                edge = edges[currentVertex];
                                prevPoint = point;
                                point = vertices[currentVertex].pos;

                                if (visited.Contains(currentVertex)) break;
                                visited.Add(currentVertex);

                                this.monotonePolyPointMapping.Add(currentVertex);
                            }


                            this.monotonePolySeparators.Add(this.monotonePolyPointMapping.Length);
                        }

                        counter++;
                    }

                    this.monotonePolySeparators.RemoveAt(this.monotonePolySeparators.Length - 1);
                }
            }

            public void Execute()
            {
                if (Hint.Likely(this.polyPoints.Length >= 3))
                {

                    var vertices = new NativeList<MonotoneVertexInfo>(this.polyPoints.Length, Allocator.Temp);
                    var edges = new NativeList<MonotoneEdgeInfo>(this.polyPoints.Length, Allocator.Temp);
                    var diagonals = new NativeList<MonotoneEdgeInfo>(Allocator.Temp);


                    this.FillOutMonotoneBaseInfo(vertices, edges);


                    var vertexComparer = new MonotoneVertexInfoComparer() { epsilon = this.epsilon };
                    var edgeComparer = new MonotoneEdgeInfoComparer()
                    {
                        edges = edges,
                        epsilon = this.epsilon,
                        sweepPoint = float2.zero,
                        vertices = vertices,
                    };

                    var eventQueue = new NativePriorityQueue<MonotoneVertexInfo, MonotoneVertexInfoComparer>(vertexComparer, Allocator.Temp);
                    var status = new NativeSortedList<MonotoneEdgeInfo, MonotoneEdgeInfoComparer>(edgeComparer, Allocator.Temp);
                    //var status = new NativeAVLTree<MonotoneEdgeInfo, MonotoneEdgeInfoComparer>(edgeComparer, Allocator.Temp);


                    this.FillOutMonotoneVertices(vertices, edges, eventQueue);

                    while (!eventQueue.IsEmpty())
                    {
                        var nextVertex = eventQueue.Dequeue();

                        edgeComparer.sweepPoint = nextVertex.pos;
                        status.comparer = edgeComparer;

                        switch (nextVertex.type)
                        {
                            case MonotoneVertexType.END:
                                this.HandleEndVertex(ref status, vertices, edges, diagonals, nextVertex);
                                break;
                            case MonotoneVertexType.START:
                                this.HandleStartVertex(ref status, edges, nextVertex);
                                break;
                            case MonotoneVertexType.SPLIT:
                                this.HandleSplitVertex(ref status, vertices, edges, diagonals, nextVertex);
                                break;
                            case MonotoneVertexType.MERGE:
                                this.HandleMergeVertex(ref status, vertices, edges, diagonals, nextVertex);
                                break;
                            case MonotoneVertexType.REGULAR:
                                this.HandleRegularVertex(ref status, vertices, edges, diagonals, nextVertex);
                                break;
                        }
                    }

                    this.CreateMonotonePolygonsWithDiagonals(diagonals, vertices, edges);

                }
            }
        }

        [BurstCompile]
        public struct YMonotoneTriangulationJob : IJob
        {
            public bool clockwiseWinding;

            [ReadOnly, NoAlias]
            public UnsafeList<float2> polyPoints;

            [ReadOnly, NoAlias]
            public NativeList<int> monotonePolyPointMapping;

            [ReadOnly, NoAlias]
            public NativeList<int> monotonePolySeparators;

            [WriteOnly, NoAlias]
            public NativeList<int> triangles;

            private void GetSortedYMonotoneVertices(int polyStart, int polyEnd, NativeList<int> ySortedVertices, NativeList<bool> isOnLeftChain)
            {
                int maxVertex = -1;
                float maxY = float.NegativeInfinity;

                int length = polyEnd - polyStart;

                for(int i = polyStart; i < polyEnd; i++)
                {
                    int idx = this.monotonePolyPointMapping[i];
                    if (Hint.Unlikely(this.polyPoints[idx].y > maxY))
                    {
                        maxVertex = i - polyStart;
                        maxY = this.polyPoints[idx].y;
                    }
                }

                int left = (maxVertex + 1) % length;
                int right = MathUtilDOTS.Mod(maxVertex - 1, length);

                ySortedVertices.Add(maxVertex);
                isOnLeftChain.Add(false);

                for(int i = 1; i < length; i++)
                {
                    int leftIdx = this.monotonePolyPointMapping[polyStart + left];
                    int rightIdx = this.monotonePolyPointMapping[polyStart + right];

                    var leftPoint = this.polyPoints[leftIdx];
                    var rightPoint = this.polyPoints[rightIdx];

                    if(leftPoint.y > rightPoint.y || (leftPoint.y == rightPoint.y && leftPoint.x < rightPoint.x))
                    {
                        ySortedVertices.Add(left);
                        left = (left + 1) % length;
                        isOnLeftChain.Add(true);
                    } else
                    {
                        ySortedVertices.Add(right);
                        right = MathUtilDOTS.Mod(right- 1, length);
                        isOnLeftChain.Add(false);
                    }
                }
            }

            public void Execute()
            {
                int startIdx = 0;

                this.triangles.Clear();
                if (Hint.Likely(this.polyPoints.Length >= 3))
                {

                    NativeList<bool> isOnLeftChain = new NativeList<bool>(1, Allocator.Temp);
                    NativeList<int> ySortedVertices = new NativeList<int>(1, Allocator.Temp);

                    NativeList<int> vertexStack = new NativeList<int>(1, Allocator.Temp);

                    for (int i = 0; i < this.monotonePolySeparators.Length + 1; i++)
                    {
                        int endIdx;
                        if (i < this.monotonePolySeparators.Length)
                        {
                            endIdx = this.monotonePolySeparators[i];
                        }
                        else
                        {
                            endIdx = this.monotonePolyPointMapping.Length;
                        }

                        isOnLeftChain.Clear();
                        ySortedVertices.Clear();
                        vertexStack.Clear();

                        this.GetSortedYMonotoneVertices(startIdx, endIdx, ySortedVertices, isOnLeftChain);

                        vertexStack.Add(0);
                        vertexStack.Add(1);

                        for (int j = 2; j < ySortedVertices.Length; j++)
                        {
                            int lastIdx = vertexStack.Length - 1;
                            int topId = vertexStack[lastIdx];
                            vertexStack.Length--;

                            if (isOnLeftChain[topId] != isOnLeftChain[j])
                            {

                                while (vertexStack.Length > 0)
                                {
                                    int nextIdx = vertexStack.Length - 1;
                                    int nextId = vertexStack[nextIdx];
                                    vertexStack.Length--;

                                    int vertexJ = this.monotonePolyPointMapping[startIdx + ySortedVertices[j]];
                                    int vertexTop = this.monotonePolyPointMapping[startIdx + ySortedVertices[topId]];
                                    int vertexNext = this.monotonePolyPointMapping[startIdx + ySortedVertices[nextId]];

                                    if (Hint.Likely(this.clockwiseWinding))
                                    {

                                        this.triangles.Add(isOnLeftChain[j] ? vertexNext : vertexTop);
                                        this.triangles.Add(isOnLeftChain[j] ? vertexTop : vertexNext);

                                        this.triangles.Add(vertexJ);
                                    }
                                    else
                                    {
                                        this.triangles.Add(vertexJ);

                                        this.triangles.Add(isOnLeftChain[j] ? vertexTop : vertexNext);
                                        this.triangles.Add(isOnLeftChain[j] ? vertexNext : vertexTop);
                                    }

                                    topId = nextId;
                                }

                                vertexStack.Add(j - 1);
                                vertexStack.Add(j);

                            }
                            else
                            {
                                float2 origin = this.polyPoints[this.monotonePolyPointMapping[startIdx + ySortedVertices[j]]];
                                int lastPop = topId;
                                do
                                {
                                    int nextIdx = vertexStack.Length - 1;
                                    int nextId = vertexStack[nextIdx];

                                    float2 prev = this.polyPoints[this.monotonePolyPointMapping[startIdx + ySortedVertices[topId]]];
                                    float2 next = this.polyPoints[this.monotonePolyPointMapping[startIdx + ySortedVertices[nextId]]];

                                    int side = VectorUtil.CompareLineDirection(origin, next, prev);


                                    //If "previous" lies on the wrong side of the diagonal
                                    if ((isOnLeftChain[j] && side >= 0)
                                        || (!isOnLeftChain[j] && side <= 0))
                                    {
                                        break;
                                    }
                                    else
                                    {

                                        int vertexJ = this.monotonePolyPointMapping[startIdx + ySortedVertices[j]];
                                        int vertexTop = this.monotonePolyPointMapping[startIdx + ySortedVertices[topId]];
                                        int vertexNext = this.monotonePolyPointMapping[startIdx + ySortedVertices[nextId]];


                                        if (Hint.Likely(this.clockwiseWinding))
                                        {

                                            this.triangles.Add(isOnLeftChain[j] ? vertexTop : vertexNext);
                                            this.triangles.Add(isOnLeftChain[j] ? vertexNext : vertexTop);

                                            this.triangles.Add(vertexJ);
                                        }
                                        else
                                        {
                                            this.triangles.Add(vertexJ);

                                            this.triangles.Add(isOnLeftChain[j] ? vertexNext : vertexTop);
                                            this.triangles.Add(isOnLeftChain[j] ? vertexTop : vertexNext);
                                        }
                                    }

                                    lastPop = nextId;
                                    topId = nextId;
                                    vertexStack.Length--;

                                } while (vertexStack.Length > 0);

                                if (lastPop >= 0)
                                {
                                    vertexStack.Add(lastPop);
                                }

                                vertexStack.Add(j);
                            }
                        }

                        startIdx = endIdx;
                    }
                }
            }
        }
    }
}
