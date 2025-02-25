using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.NotBurstCompatible;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// A 2D polygon, that can be used within Unitys Job System. Supports holes,
    /// and is the basis for many advanced geometry algorithms
    /// </summary>
    [Serializable]
    public unsafe struct NativePolygon2D : IDisposable, IBinaryPersistable
    {

        #region Public Variables

        /// <summary>
        /// All points of the polygon. As native containers are unable to hold other native containers, an unsafe list is used instead (necessary for Voronoi)
        /// </summary>
        [NoAlias]
        public UnsafeList<float2> points;
        /// <summary>
        /// Separate the polygon into subsurfaces. The first subsurface is always considered
        /// the outer boundary of the polygon. Subsequent ones are considered to be holes
        /// As native containers are unable to hold other native containers, an unsafe list is used instead (necessary for Voronoi)
        /// </summary>
        [NoAlias]
        public UnsafeList<int> separators;

        #endregion

        #region Private Variables

        #endregion

        #region Static

        private struct SimplifyHoleInfo
        {
            public Vector2 maxX;
            public int maxIdx;
            public int separatorIdx;
        }

        private struct SimplifyHoleInfoComprarer : IComparer<SimplifyHoleInfo>
        {
            public int Compare(SimplifyHoleInfo holeA, SimplifyHoleInfo holeB)
            {
                return holeA.maxX.x.CompareTo(holeB.maxX.x);
            }
        }

        private static int FindBoundaryVertexToRight(float2 maxHolePoint, List<float2> points) {

            List<int> reflexVertices = ListPool<int>.Get();

            float2 closestPoint = Vector2.negativeInfinity;
            float minDist = float.PositiveInfinity;
            int closestIdx = -1;
            int closestEdge = -1;

            int length = points.Count;
            for (int i = 0; i < length; i++)
            {
                int nextIdx = MathUtil.Mod(i + 1, length);

                float2 pointA = points.ElementAt(MathUtil.Mod(i - 1, length));
                float2 pointB = points.ElementAt(i);
                float2 pointC = points.ElementAt(nextIdx);

                float angle = Vector2.SignedAngle(pointC - pointB, pointA - pointB);
                if (angle < 0.0f)
                {
                    reflexVertices.Add(i);
                }

                if ((maxHolePoint.x < pointB.x || maxHolePoint.x < pointC.x)
                    && ((maxHolePoint.y >= pointC.y && maxHolePoint.y <= pointB.y)
                    || (maxHolePoint.y <= pointC.y && maxHolePoint.y >= pointB.y)))
                {
                    float t = math.unlerp(pointB.y, pointC.y, maxHolePoint.y);
                    float2 intersectionPoint = math.mad(t, pointC - pointB, pointB);

                    float dist = math.distancesq(maxHolePoint, intersectionPoint);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        if (t <= 0.0f)
                        {
                            closestPoint = pointB;
                            closestIdx = i;
                        } else if (t >= 1.0f)
                        {
                            closestPoint = pointC;
                            closestIdx = nextIdx;
                        } else
                        {
                            closestPoint = intersectionPoint;
                            closestEdge = i;
                            closestIdx = -1;
                        }
                    }
                }
            }

            if (closestIdx >= 0)
            {
                ListPool<int>.Return(reflexVertices);
                return closestIdx;
            }

            if (closestEdge < 0) return -1;

            float2 edgeA = points.ElementAt(closestEdge);
            float2 edgeB = points.ElementAt(MathUtil.Mod(closestEdge + 1, length));

            float2 maxXPoint;
            int maxXPointIdx;
            if (edgeA.x > edgeB.x)
            {
                maxXPoint = edgeA;
                maxXPointIdx = closestEdge;
            } else
            {
                maxXPoint = edgeB;
                maxXPointIdx = MathUtil.Mod(closestEdge + 1, length);
            }

            var triangle = new NativeTriangle2D(maxHolePoint, closestPoint, maxXPoint);
            int closestReflexVertex = -1;
            minDist = float.PositiveInfinity;

            for (int i = 0; i < reflexVertices.Count; i++)
            {
                int reflexVertex = reflexVertices[i];
                float2 point = points.ElementAt(reflexVertex);

                if (triangle.IsPointInside(point))
                {
                    float dist = math.distancesq(maxHolePoint, point);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestReflexVertex = reflexVertex;
                    }
                }
            }

            ListPool<int>.Return(reflexVertices);
            if (closestReflexVertex >= 0)
            {
                return closestReflexVertex;
            } else
            {
                return maxXPointIdx;
            }
        }


        private struct SwitchHoleInfo
        {
            public float angle;

            public int globalIdx;
            public int holeLength;
        }

        private struct SwitchHoleInfoComparer : IComparer<SwitchHoleInfo>
        {
            public int Compare(SwitchHoleInfo x, SwitchHoleInfo y)
            {
                return -x.angle.CompareTo(y.angle);
            }
        }

        //This is what was not in the paper (but I contacted David Eberly, no worries)
        //When multiple holes map to the same vertex on the outer bounds, the order
        //is very important. We get the right one by choosing the one that makes
        //the least counter-clockwise angle with the edge that is previous to the
        //chosen point.
        //Very simply, we just choose the one that will not cause a closed loop in the
        //future
        public static void SwitchHolesByAngle(int idx, List<float2> tempPoints)
        {

            int prevIdx = MathUtil.Mod(idx - 1, tempPoints.Count);

            float2 prevPoint = tempPoints[prevIdx];
            float2 point = tempPoints[idx];

            float2 edgeDir = prevPoint - point;

            int length = 0;

            PriorityQueueHeap<SwitchHoleInfo> prio = new PriorityQueueHeap<SwitchHoleInfo>(new SwitchHoleInfoComparer());
            float angle = 0.0f;

            int start = 0;
            for (int i = 0; i < tempPoints.Count; i++)
            {
                if (math.all(tempPoints[i] == point))
                {
                    start = i;
                    break;
                }
            }

            int globalIdx = start;
            for (int i = start; i < tempPoints.Count; i++)
            {
                var currentPoint = tempPoints[i];

                if (math.all(currentPoint == point))
                {
                    if (length > 0)
                    {
                        prio.Enqueue(new SwitchHoleInfo()
                        {
                            holeLength = length,
                            angle = angle,
                            globalIdx = globalIdx
                        });
                        length = 0;
                    }

                    int nextIdx = MathUtil.Mod(i + 1, tempPoints.Count);
                    float2 nextPoint = tempPoints[nextIdx];
                    float2 nextDir = nextPoint - currentPoint;

                    angle = Vector2.SignedAngle(nextDir, edgeDir);
                    if (angle < 0.0f) angle += 180.0f;
                    globalIdx = i;
                }
                length++;

            }

            List<float2> correctlyOrderedHoles = ListPool<float2>.Get();

            while (!prio.IsEmpty())
            {
                var nextHoleSwitchInfo = prio.Dequeue();
                for (int i = nextHoleSwitchInfo.globalIdx; i < nextHoleSwitchInfo.globalIdx + nextHoleSwitchInfo.holeLength; i++)
                {
                    correctlyOrderedHoles.Add(tempPoints[i]);
                }
            }

            for (int i = start; i < start + correctlyOrderedHoles.Count; i++)
            {
                int holeIdx = i - start;
                tempPoints[i] = correctlyOrderedHoles[holeIdx];
            }

            ListPool<float2>.Return(correctlyOrderedHoles);
        }

        /// <summary>
        /// Removes all holes but keeps the appearance by adding bridge edges from the border of the polygon
        /// to the previous holes.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="polygon2D"></param>
        /// <returns></returns>
        public static NativePolygon2D MakeSimple(Allocator allocator, NativePolygon2D polygon2D)
        {
            var originalPoints = polygon2D.points;
            var tempPoints = ListPool<float2>.Get();

            int boundaryEnd = originalPoints.Length;
            if (polygon2D.separators.Length > 0)
            {
                boundaryEnd = polygon2D.separators[0];
            }
            for (int i = 0; i < boundaryEnd; i++)
            {
                tempPoints.Add(originalPoints.ElementAt(i));
            }
            var newSeparators = new NativeList<int>(allocator);
            var separators = polygon2D.separators;

            PriorityQueueHeap<SimplifyHoleInfo> holeQueue = new PriorityQueueHeap<SimplifyHoleInfo>(new SimplifyHoleInfoComprarer());

            for (int i = 0; i < separators.Length; i++)
            {
                int holeStart = separators.ElementAt(i);
                int holeEnd;
                if ((i + 1) >= separators.Length)
                {
                    holeEnd = originalPoints.Length;
                }
                else
                {
                    holeEnd = separators.ElementAt(i + 1);
                }
                float maxX = float.NegativeInfinity;
                int maxIdx = -1;

                for (int j = holeStart; j < holeEnd; j++)
                {
                    var point = originalPoints[j];
                    if (point.x > maxX)
                    {
                        maxX = point.x;
                        maxIdx = j;
                    }
                }
                holeQueue.Enqueue(new SimplifyHoleInfo()
                {
                    separatorIdx = i,
                    maxIdx = maxIdx,
                    maxX = originalPoints[maxIdx]
                });
            }

            Dictionary<float2, int> holeVertexMapping = new Dictionary<float2, int>();
            while (!holeQueue.IsEmpty())
            {
                var holeInfo = holeQueue.Dequeue();
                if (holeInfo.maxIdx >= 0)
                {
                    int holeStart = separators.ElementAt(holeInfo.separatorIdx);
                    int holeEnd;
                    if ((holeInfo.separatorIdx + 1) >= separators.Length)
                    {
                        holeEnd = originalPoints.Length;
                    } else
                    {
                        holeEnd = separators.ElementAt(holeInfo.separatorIdx + 1);
                    }
                    int holeLength = holeEnd - holeStart;

                    int maxVertex = holeInfo.maxIdx;
                    float2 maxPoint = originalPoints.ElementAt(maxVertex);

                    int vertexToRight = FindBoundaryVertexToRight(maxPoint, tempPoints);
                    if (vertexToRight >= 0)
                    {
                        float2 rightPoint = tempPoints[vertexToRight];

                        int nextVertex = MathUtil.Mod(vertexToRight + 1, tempPoints.Count);

                        tempPoints.Insert(nextVertex, rightPoint);
                        tempPoints.Insert(nextVertex, maxPoint);

                        int roundtripStart = (maxVertex - holeStart + 1) % holeLength;
                        for (int i = roundtripStart; i < holeLength + roundtripStart; i++)
                        {
                            int idx = i % holeLength;
                            tempPoints.Insert(nextVertex, originalPoints.ElementAt(holeStart + idx));
                        }

                        if (holeVertexMapping.ContainsKey(rightPoint))
                        {
                            SwitchHolesByAngle(holeVertexMapping[rightPoint], tempPoints);
                        }
                        else
                        {
                            holeVertexMapping.Add(rightPoint, vertexToRight);
                        }
                    }
                }
            }



            var points = new NativeList<float2>(polygon2D.points.Length, Allocator.TempJob);
            points.CopyFromNBC(tempPoints.ToArray());

            ListPool<float2>.Return(tempPoints);
            var poly = new NativePolygon2D(allocator, points, newSeparators);
            points.Dispose();
            newSeparators.Dispose();
            return poly;
        }


        #endregion

        #region Constructors

        public NativePolygon2D(Allocator allocator, int nrOfPoints)
        {
            this.points = new UnsafeList<float2>(nrOfPoints, allocator);
            this.separators = new UnsafeList<int>(1, allocator);
        }

        public unsafe NativePolygon2D(Allocator allocator, List<Vector2> points) : this(allocator, points.ToArray()) { }
        public unsafe NativePolygon2D(Allocator allocator, Vector2[] points)
        {
            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(1, allocator);
        
            this.points.Length = points.Length;

            if (points.Length > 0)
            {
                fixed (Vector2* ptr = &points[0])
                {
                    UnsafeUtility.MemCpy(this.points.Ptr, ptr, UnsafeUtility.SizeOf<Vector2>() * points.Length);
                    this.points.Length = points.Length;
                }
            }
        }

        public unsafe NativePolygon2D(Allocator allocator, Vector2[] points, int[] separators)
        {
            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(separators.Length, allocator);

            this.points.Length = points.Length;
            this.separators.Length = separators.Length;

            if (points.Length > 0)
            {
                fixed (Vector2* ptr = &points[0])
                {
                    UnsafeUtility.MemCpy(this.points.Ptr, ptr, UnsafeUtility.SizeOf<Vector2>() * points.Length);
                    this.points.Length = points.Length;
                }
            }

            if(separators.Length > 0)
            {
                fixed (int* ptr = &separators[0])
                {
                    UnsafeUtility.MemCpy(this.separators.Ptr, ptr, UnsafeUtility.SizeOf<int>() * separators.Length);
                    this.separators.Length = separators.Length;
                }
            }
        }

        public NativePolygon2D(Allocator allocator, List<float2> points) : this(allocator, points.ToArray()) { }

        public NativePolygon2D(Allocator allocator, float2[] points)
        {
            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(1, allocator);

            if (points.Length > 0)
            {
                fixed (float2* ptr = &points[0])
                {
                    UnsafeUtility.MemCpy(this.points.Ptr, ptr, UnsafeUtility.SizeOf<float2>() * points.Length);
                    this.points.Length = points.Length;
                }
            }
        }

        public NativePolygon2D(Allocator allocator, float2[] points, int[] separators)
        {
            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(separators.Length, allocator);

            if (points.Length > 0)
            {
                fixed (float2* ptr = &points[0])
                {
                    UnsafeUtility.MemCpy(this.points.Ptr, ptr, UnsafeUtility.SizeOf<float2>() * points.Length);
                    this.points.Length = points.Length;
                }
            }

            if(separators.Length > 0)
            {
                fixed(int* ptr = &separators[0])
                {
                    UnsafeUtility.MemCpy(this.separators.Ptr, ptr, UnsafeUtility.SizeOf<int>() * separators.Length);
                    this.separators.Length = separators.Length;
                }
            }
        }

        public NativePolygon2D(Allocator allocator, NativeArray<float2> points)
        {

            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(1, allocator);

            this.points.CopyFrom(points);
        }

        public NativePolygon2D(Allocator allocator, NativeList<float2> points)
        {

            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(1, allocator);

            this.points.CopyFrom(points.AsArray());
        }

        public unsafe NativePolygon2D(Allocator allocator, NativeList<float2> points, NativeList<int> separators)
        {

            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(separators.Length, allocator);

            this.points.CopyFrom(points.AsArray());
            this.separators.CopyFrom(separators.AsArray());
        }

        public NativePolygon2D(Allocator allocator, UnsafeList<float2> points, UnsafeList<int> separators)
        {
 
            this.points = new UnsafeList<float2>(points.Length, allocator);
            this.separators = new UnsafeList<int>(separators.Length, allocator);

            this.points.CopyFrom(points);
            this.separators.CopyFrom(separators);
        }

        #endregion

        public bool IsCreated => this.points.IsCreated || this.separators.IsCreated;

        /// <summary>
        /// Clears all vertices and holes in the polygon
        /// </summary>
        public void Clear()
        {
            this.points.Clear();
            this.separators.Clear();
        }

        public int NumberOfHoles() => this.separators.Length;


        private void HoleCheck(NativeArray<float2> points)
        {
            if(points.Length <= 0)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Cannot add hole of length 0");
            }

#if !GDG_LENIENT_SAFETY_CHECKS
            for (int i = 0; i < points.Length; i++)
            {
                if (!IsPointInside(points[i]))
                {
                    Debug.LogError("[Gimme DOTS Geometry]: A hole cannot be outside the polygon!");
                    break;
                }
            }
#endif
        }

        private void HoleCheck(Vector2[] points)
        {
            if (points.Length <= 0)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Cannot add hole of length 0");
            }

#if !GDG_LENIENT_SAFETY_CHECKS
            for (int i = 0; i < points.Length; i++)
            {
                if (!IsPointInside(points[i]))
                {
                    Debug.LogError("[Gimme DOTS Geometry]: A hole cannot be completely or partially outside the polygon!");
                    break;
                }
            }
#endif
        }

        /// <summary>
        /// Appends a hole to the polygon. The order of the points added should be counter-clockwise (the algorithms sort it out later)
        /// </summary>
        /// <param name="points"></param>
        public void AddHole(NativeList<float2> points)
        {

            this.HoleCheck(points.AsArray());

            this.separators.Add(this.points.Length);
            this.points.AddRange(points.GetUnsafePtr(), points.Length);
        }

        /// <summary>
        /// Appends a hole to the polygon. The order of the points added should be counter-clockwise (the algorithms sort it out later)
        /// </summary>
        /// <param name="points"></param>
        public void AddHole(NativeArray<float2> points)
        {
            this.HoleCheck(points);

            this.separators.Add(this.points.Length);
            this.points.AddRange(points.GetUnsafePtr(), points.Length);
        }

        /// <summary>
        /// Appends a hole to the polygon. The order of the points added should be counter-clockwise (the algorithms sort it out later)
        /// </summary>
        /// <param name="points"></param>
        public void AddHole(List<Vector2> points)
        {
            var array = points.ToArray();
            this.AddHole(array);
        }

        /// <summary>
        /// Appends a hole to the polygon. The order of the points added should be counter-clockwise (the algorithms sort it out later)
        /// </summary>
        /// <param name="points"></param>
        public void AddHole(Vector2[] points)
        {
            this.HoleCheck(points);

            this.separators.Add(this.points.Length);
            fixed (Vector2* ptr = &points[0]) {
                this.points.AddRange(ptr, points.Length);
            }
        }

        /// <summary>
        /// Removes the hole with the specified subsurface (the index of the hole)
        /// </summary>
        /// <param name="subsurfaceIdx"></param>
        public void RemoveHole(int subsurfaceIdx)
        {
            if (subsurfaceIdx > 0 && subsurfaceIdx <= this.separators.Length) {
                int removeStart = this.separators[subsurfaceIdx - 1];
                int removeEnd = this.points.Length;
                if(subsurfaceIdx < this.separators.Length)
                {
                    removeEnd = this.separators.ElementAt(subsurfaceIdx);
                }

                int length = removeEnd - removeStart;

                this.points.RemoveRange(removeStart, length);
                this.separators.RemoveAt(subsurfaceIdx - 1);

                for(int i = subsurfaceIdx - 1; i < this.separators.Length; i++)
                {
                    this.separators.ElementAt(i) -= length;
                }
            }
        }

        /// <summary>
        /// Insert a point at any index in the polygon (method will handle holes/subsurfaces etc.)
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="point"></param>
        public void InsertPoint(int idx, float2 point)
        {
            this.points.Insert(idx, point);

            if(this.separators.Length > 0)
            {
                int nextSubsurface = -1;
                for(int i = 0; i < this.separators.Length; i++)
                {
                    if(idx < this.separators.ElementAt(i))
                    {
                        nextSubsurface = i;
                        break;
                    }
                }

                if (nextSubsurface >= 0)
                {
                    for (int i = nextSubsurface; i < this.separators.Length; i++)
                    {
                        this.separators[i]++;
                    }
                }
            }
        }

        /// <summary>
        /// Removes a point at any index in the polygon (method will handle holes/subsurfaces etc.)
        /// </summary>
        /// <param name="idx"></param>
        public void RemovePoint(int idx, out int removedSubsurface)
        {
            removedSubsurface = -1;
            int nextSubsurface = 0;
            for(int i = 0; i < this.separators.Length; i++)
            {
                if (idx >= this.separators.ElementAt(i))
                {
                    nextSubsurface = i + 1;
                }
            }

            int nextIdx = this.points.Length - 1;
            if(nextSubsurface > 0 && nextSubsurface < this.separators.Length)
            {
                for (int i = nextSubsurface; i < this.separators.Length; i++)
                {
                    this.separators[i]--;
                }
                nextIdx = this.separators[nextSubsurface];
            }

            if (nextSubsurface > 0 && nextSubsurface - 1 < this.separators.Length && this.separators[nextSubsurface - 1] == nextIdx)
            {
                this.points.RemoveAt(idx);
                this.separators.RemoveAt(nextSubsurface - 1);

                removedSubsurface = nextSubsurface;
            } else
            {
                this.points.RemoveAt(idx);
            }

        }

        internal NativeArray<int> PrepareOffsets(Allocator allocator)
        {
            NativeArray<int> offsets = new NativeArray<int>(this.points.Length + 1, Allocator.Temp);

            UnsafeUtility.MemSet(offsets.GetUnsafePtr(), 0, offsets.Length * UnsafeUtility.SizeOf<int>());

            int prev = 0;
            var separators = this.separators;
            for (int i = 0; i < separators.Length; i++)
            {
                offsets[separators[i]] = separators[i] - prev;
                prev = separators[i];
            }
            offsets[this.points.Length] = this.points.Length - prev;

            return offsets;
        }

        /// <summary>
        /// This version is used in some queries to speed them up a bit. Requires some preprocessing
        /// </summary>
        /// <param name="point"></param>
        /// <param name="offsets"></param>
        /// <returns></returns>
        internal bool IsPointInsideInternal(float2 point, NativeArray<int> offsets)
        {
            int windingNumber = 0;
            for (int j = 0; j < this.points.Length; j++)
            {
                float4 p = point.xyxy - new float4(this.points[j], this.points[j + 1 - offsets[j + 1]]);
                if ((math.asint(p.y) ^ math.asint(p.w)) < 0)
                {
                    int r = (math.asint(p.y * p.z - p.x * p.w) ^ math.asint(p.y - p.w));
                    windingNumber += 2 * (r >> 31);
                }
            }
            return MathUtilDOTS.Mod(windingNumber, 4) != 0;
        }


        //https://www.engr.colostate.edu/~dga/documents/papers/point_in_polygon.pdf
        //The algorithm is adapted to handle holes (going through the counter-clockwise holes)
        //Note:

        /// <summary>
        /// Calculates the winding number of the point (i.e. how often is the boundary going around in circles around the point).
        /// An even number means the point is outside the polygon, while an odd number means it is inside
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public float GetWindingNumber(Vector2 point)
        {
            float w = 0.0f;

            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = this.points.Length;
            if (this.separators.Length > 0)
            {
                nextSubsurfaceIdx = this.separators.ElementAt(subsurface);
            }

            for (int i = 0; i < this.points.Length; i++)
            {
                int idx = i - prevSubsurfaceIdx;
                var a = this.points.ElementAt(prevSubsurfaceIdx + idx);
                var b = this.points.ElementAt(prevSubsurfaceIdx + ((idx + 1) % (nextSubsurfaceIdx - prevSubsurfaceIdx)));

                var pointA = (float2)point - a;
                var pointB = (float2)point - b;

                if (pointA.y * pointB.y < 0.0f)
                {
                    float r = pointA.x + ((pointA.y * (pointB.x - pointA.x)) / (pointA.y - pointB.y));
                    if (r > 0)
                    {
                        w += pointA.y < 0.0f ? 1.0f : -1.0f;
                    }
                }
                else if (pointA.y == 0.0f && pointA.x > 0.0f)
                {
                    w += pointB.y > 0.0f ? 0.5f : -0.5f;
                }
                else if (pointB.y == 0.0f && pointB.x > 0.0f)
                {
                    w += pointA.y < 0.0f ? 0.5f : -0.5f;
                }

                if (i >= nextSubsurfaceIdx - 1)
                {
                    prevSubsurfaceIdx = nextSubsurfaceIdx;
                    subsurface++;
                    if (this.separators.Length > subsurface)
                    {
                        nextSubsurfaceIdx = this.separators.ElementAt(subsurface);
                    } else
                    {
                        nextSubsurfaceIdx = this.points.Length;
                    }
                }
            }

            return w;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <returns>True, if point lies inside the surface of the polygon and not outside boundary of it or inside a hole. False, otherwise</returns>
        public bool IsPointInside(Vector2 point)
        {
            return (MathUtilDOTS.Mod((int)this.GetWindingNumber(point), 2) != 0);
        }

        private bool IsReflexVertex(int prev, int current, int next)
        {
            var pointA = this.points.ElementAt(prev);
            var pointB = this.points.ElementAt(current);
            var pointC = this.points.ElementAt(next);

            var angle = Vector2.SignedAngle(pointC - pointB, pointA - pointB);

            if (angle < 0.0f)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true, if the polygon does not contain any reflex vertices</returns>
        public bool IsConvex()
        {
#if !GDG_LENIENT_SAFETY_CHECKS
            if (this.points.Length <= 2)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Polygon has less than three points");
                return false;
            }
#endif

            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = this.points.Length;
            if (this.separators.Length > 0)
            {
                nextSubsurfaceIdx = this.separators.ElementAt(subsurface);
            }

            for (int i = 0; i < this.points.Length; i++)
            {
                int idx = i - prevSubsurfaceIdx;
                int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                int next = prevSubsurfaceIdx + ((idx + 1) % length);
                int previous = prevSubsurfaceIdx + MathUtil.Mod(idx - 1, length);

                if (this.IsReflexVertex(previous, idx, next))
                {
                    return false;
                }

                if (i >= nextSubsurfaceIdx - 1)
                {
                    //We do not need to check the holes for convexity, it does not really make sense in this context,
                    //and would lead to confusion
                    break;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true if the polygon does not contain any holes</returns>
        public bool IsSimple()
        {
            return this.separators.Length == 0;
        }

        /// <summary>
        /// Returns false if the separators in the polygon contain negative length holes
        /// </summary>
        /// <returns></returns>
        public bool AreHolesValid()
        {
            for(int i = 1; i < this.separators.Length; i++)
            {
                int separator = this.separators[i];
                int lastSeparator = this.separators[i - 1];
                if(separator <= 0 || separator - lastSeparator <= 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Calculates the area of the polygon
        /// </summary>
        /// <returns></returns>
        public float Area()
        {
#if !GDG_LENIENT_SAFETY_CHECKS
            if (this.points.Length <= 2)
            {
                Debug.LogError("[Gimme DOTS Geometry]: Polygon has less than three points");
                return float.NaN;
            }
#endif

            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = this.points.Length;
            if (this.separators.Length > 0)
            {
                nextSubsurfaceIdx = this.separators.ElementAt(subsurface);
            }

            //Shoelace Formula
            float area = 0.0f;
            for (int i = 0; i < this.points.Length; i++)
            {
                int idx = i - prevSubsurfaceIdx;
                int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                int next = prevSubsurfaceIdx + ((idx + 1) % length);

                var currentPoint = this.points[i];
                var nextPoint = this.points[next];

                float det = currentPoint.x * nextPoint.y - currentPoint.y * nextPoint.x;

                area += subsurface == 0 ? det : -det;

                if (i >= nextSubsurfaceIdx - 1)
                {
                    prevSubsurfaceIdx = nextSubsurfaceIdx;
                    subsurface++;
                    if (this.separators.Length > subsurface)
                    {
                        nextSubsurfaceIdx = this.separators.ElementAt(subsurface);
                    }
                    else
                    {
                        nextSubsurfaceIdx = this.points.Length;
                    }
                }
            }
            return area / 2.0f;

        }

        /// <summary>
        /// Returns the average position of all the points of the polygon. This is not the real centroid of the polygon,
        /// but the more useful variant in most cases
        /// </summary>
        /// <returns></returns>
        public Vector2 GetCenter()
        {
            Vector2 avg = Vector2.zero;
            for (int i = 0; i < this.points.Length; i++)
            {
                avg += (Vector2)this.points[i];
            }
            return avg / this.points.Length;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The minimum rectangle that contains all the points of the polygon</returns>
        public Rect GetBoundingRect()
        {
            float xMin = float.PositiveInfinity;
            float yMin = float.PositiveInfinity;

            float xMax = float.NegativeInfinity;
            float yMax = float.NegativeInfinity;

            for(int i = 0; i < this.points.Length; i++)
            {
                var point = this.points.ElementAt(i);

                if (point.x < xMin) xMin = point.x;
                if (point.y < yMin) yMin = point.y;
                if (point.x > xMax) xMax = point.x;
                if (point.y > yMax) yMax = point.y;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// Converts a polygon to a doubly-connected edge list (DCEL)
        /// </summary>
        /// <param name="polygon">The polygon to convert</param>
        /// <param name="faceID">A pointer that is stored in the face record, to point to attribute data or for identification</param>
        /// <param name="allocator"></param>
        /// <returns></returns>
        public unsafe static DCEL ConvertToDCEL(NativePolygon2D polygon, int faceID, Allocator allocator = Allocator.TempJob)
        {
            DCEL dcel = new DCEL(allocator);

            dcel.vertices.Resize(polygon.points.Length, NativeArrayOptions.UninitializedMemory);
            UnsafeUtility.MemCpy(dcel.vertices.GetUnsafePtr(), polygon.points.Ptr, polygon.points.Length * sizeof(float2));

            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = polygon.points.Length;
            if (polygon.separators.Length > 0)
            {
                nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
            }

            int length = nextSubsurfaceIdx - prevSubsurfaceIdx;
            for (int i = 0; i < polygon.points.Length; i++)
            {
                int idx = i;
                int nextIdx = prevSubsurfaceIdx + MathUtilDOTS.Mod(i + 1 - prevSubsurfaceIdx, length);
                int prevIdx = prevSubsurfaceIdx + MathUtilDOTS.Mod(i - 1 - prevSubsurfaceIdx, length);

                var hEdge = new HalfEdge()
                {
                    back = prevIdx,
                    fwd = nextIdx,
                    face = faceID,
                    twin = -1,
                    vertexBack = idx,
                    vertexFwd = nextIdx
                };
                dcel.edges.Add(hEdge);
                dcel.incidentEdges.Add(idx);

                if (i >= nextSubsurfaceIdx - 1)
                {
                    prevSubsurfaceIdx = nextSubsurfaceIdx;
                    subsurface++;
                    nextSubsurfaceIdx = polygon.separators.Length > subsurface ? polygon.separators.ElementAt(subsurface) : polygon.points.Length;
                    length = nextSubsurfaceIdx - prevSubsurfaceIdx;
                }
            }

            var faceRecord = new FaceRecord(allocator);
            faceRecord.outerComponent = -1;
            faceRecord.innerComponents.Resize(polygon.separators.Length + 1);
            faceRecord.innerComponents.Add(0);
            UnsafeUtility.MemCpy(faceRecord.innerComponents.Ptr + 1, polygon.separators.Ptr, sizeof(int) * polygon.separators.Length);

            dcel.faces.Add(faceRecord);
        
            return dcel;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="point"></param>
        /// <returns>The shortest distance between the point and the polygon. If signed, the distances inside the polygon
        /// are negative</returns>
        public static float Distance(NativePolygon2D polygon, Vector2 point, out int closestIndex, out int closestSubsurface, bool signed = false)
        {
            var vertices = polygon.points;

            float shortestDistance = float.PositiveInfinity;

            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = polygon.points.Length;
            closestIndex = -1;
            closestSubsurface = 0;
            if (polygon.separators.Length > 0)
            {
                nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
            }

            float dist;
            for (int i = 0; i < vertices.Length; i++)
            {

                int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                var edgeA = vertices.ElementAt(i);
                var edgeB = vertices.ElementAt(prevSubsurfaceIdx + ((i + 1 - prevSubsurfaceIdx) % length));

                var edgeDir = edgeB - edgeA;
                var pointDir = (float2)point - edgeA;

                float scalar = VectorUtil.ScalarProjection(pointDir, edgeDir);
                if (scalar >= 0.0f && scalar <= 1.0f)
                {
                    var edgePoint = edgeA + scalar * edgeDir;
                    dist = math.distancesq(point, edgePoint);
                    if (dist < shortestDistance)
                    {
                        shortestDistance = dist;
                        closestIndex = i;
                        closestSubsurface = subsurface;
                    }
                }

                var vertex = vertices.ElementAt(i);
                dist = math.distancesq(point, vertex);
                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    closestIndex = i;
                    closestSubsurface = subsurface;
                }

                if (i >= nextSubsurfaceIdx - 1)
                {
                    prevSubsurfaceIdx = nextSubsurfaceIdx;
                    subsurface++;
                    if (polygon.separators.Length > subsurface)
                    {
                        nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
                    }
                    else
                    {
                        nextSubsurfaceIdx = polygon.points.Length;
                    }
                }
            }

            shortestDistance = math.sqrt(shortestDistance);
            if (signed)
            {
                if (polygon.IsPointInside(point))
                {
                    shortestDistance = -shortestDistance;
                }
            }
            return shortestDistance;
        }

        /// <summary>
        /// Flips the polygon - i.e. it reverses the vertex order so it faces the other way
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="polygon"></param>
        /// <returns></returns>
        public static NativePolygon2D Flip(Allocator allocator, NativePolygon2D polygon)
        {
            UnsafeList<float2> reversedPoints = new UnsafeList<float2>(polygon.points.Length, Allocator.TempJob);

            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = polygon.points.Length;
            if(polygon.separators.Length > 0)
            {
                nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
            }

            for (int i = 0; i < polygon.points.Length; i++)
            {

                int idx = i - prevSubsurfaceIdx;
                reversedPoints.Add(polygon.points[nextSubsurfaceIdx - 1 - idx]);

                if (i >= nextSubsurfaceIdx - 1)
                {
                    prevSubsurfaceIdx = nextSubsurfaceIdx;
                    subsurface++;
                    if (polygon.separators.Length > subsurface)
                    {
                        nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
                    }
                    else
                    {
                        nextSubsurfaceIdx = polygon.points.Length;
                    }
                }
            }

            var flippedPoly =  new NativePolygon2D(allocator, reversedPoints, polygon.separators);
            reversedPoints.Dispose();

            return flippedPoly;
        }

        /// <summary>
        /// Removes colinear vertices of the polygon (Runtime = O(n)) and returns a copy
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static NativePolygon2D RemoveColinearVertices(Allocator allocator, NativePolygon2D polygon, float epsilon = 0.01f)
        {
            //If a polygon only contains three points and the points are colinear... well not much of a polygon
            //if only two points remain ^^. -> Therefore at least four points
            if (polygon.points.Length >= 4)
            {
                var newPoints = new List<Vector2>();

                int subsurface = 0;
                int prevSubsurfaceIdx = 0;
                int nextSubsurfaceIdx = polygon.points.Length;
                if (polygon.separators.Length > 0)
                {
                    nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
                }

                for (int i = 0; i < polygon.points.Length; i++)
                {

                    int idx = i - prevSubsurfaceIdx;
                    int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                    int prevIdx = MathUtil.Mod(idx - 1, length);
                    int nextIdx = MathUtil.Mod(idx + 1, length);

                    var prevPoint = polygon.points.ElementAt(prevSubsurfaceIdx + prevIdx);
                    var nextPoint = polygon.points.ElementAt(prevSubsurfaceIdx + nextIdx);
                    var point = polygon.points.ElementAt(prevSubsurfaceIdx + idx);

                    var prevDir = point - prevPoint;
                    var nextDir = nextPoint - point;

                    var checkVec = math.normalize(prevDir) - math.normalize(nextDir);

                    if (Mathf.Abs(checkVec.x) + Mathf.Abs(checkVec.y) > epsilon)
                    {
                        newPoints.Add(point);
                    }

                    if (i >= nextSubsurfaceIdx - 1)
                    {
                        prevSubsurfaceIdx = nextSubsurfaceIdx;
                        subsurface++;
                        if (polygon.separators.Length > subsurface)
                        {
                            nextSubsurfaceIdx = polygon.separators.ElementAt(subsurface);
                        }
                        else
                        {
                            nextSubsurfaceIdx = polygon.points.Length;
                        }
                    }
                }

                var newPolygon = new NativePolygon2D(allocator, newPoints);
                return newPolygon;
            }
            else
            {
                var newPolygon = new NativePolygon2D(allocator, polygon.points, polygon.separators);
                return newPolygon;
            }
        }

        #region Getters

        public UnsafeList<float2> GetPoints()
        {
            return this.points;
        } 

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (this.points.IsCreated) this.points.Dispose();
            if (this.separators.IsCreated) this.separators.Dispose();
        }

        public JobHandle Dispose(JobHandle jobHandle)
        {

            var pointsHandle = this.points.Dispose(jobHandle);
            var returnHandle = this.separators.Dispose(pointsHandle);

            return returnHandle;
        }


        #endregion

        #region IBinaryPersistable

        public void SaveAsBinary(string filePath)
        {
            var bytes = this.SerializeBinary();

            File.WriteAllBytes(filePath, bytes);
        }

        public void LoadFromBinary(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);

            this.DeserializeBinary(bytes);
        }

        public byte[] SerializeBinary()
        {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(BitConverter.GetBytes(this.points.Length));

            for(int i = 0; i < this.points.Length; i++)
            {
                var point = this.points.ElementAt(i);
                bytes.AddRange(BitConverter.GetBytes(point.x));
                bytes.AddRange(BitConverter.GetBytes(point.y));
            }

            bytes.AddRange(BitConverter.GetBytes(this.separators.Length));

            for(int i = 0; i < this.separators.Length; i++)
            {
                var separator = this.separators.ElementAt(i);
                bytes.AddRange(BitConverter.GetBytes(separator));
            }

            return bytes.ToArray();
        }

        public void DeserializeBinary(byte[] data)
        {
            this.points.Clear();
            this.separators.Clear();

            int idx = 0;
            int pointsLength = BitConverter.ToInt32(data, idx);
            idx += sizeof(int);

            for(int i = idx; i < idx + pointsLength * sizeof(float2); i += sizeof(float2))
            {
                float x = BitConverter.ToSingle(data, i);
                float y = BitConverter.ToSingle(data, i + sizeof(float));

                this.points.Add(new float2(x, y));
            }

            idx += pointsLength * sizeof(float2);

            int separatorsLength = BitConverter.ToInt32(data, idx);
            idx += sizeof(int);

            for(int i = idx; i < idx + separatorsLength * sizeof(int); i += sizeof(int))
            {
                int separator = BitConverter.ToInt32(data, i);
                this.separators.Add(separator);
            }
        }



        public void OnBeforeSerialize()
        {

        }

        public void OnAfterDeserialize()
        {

        }

        #endregion
    }
}
