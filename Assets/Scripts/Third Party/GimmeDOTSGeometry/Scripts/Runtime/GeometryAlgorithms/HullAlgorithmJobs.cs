using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class HullAlgorithmJobs
    {
        [BurstCompile]
        public struct BoundingRectangleJob : IJob
        {

            public float addedMargin;

            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeReference<Rect> boundingRect;

            public void Execute()
            {
#if !GDG_UNSAFE_DEGENERACIES
                if (this.points.Length == 0)
                {
                    this.boundingRect.Value = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
                    return;
                }
#endif

                float2 min = float.PositiveInfinity;
                float2 max = float.NegativeInfinity;

                for(int i = 0; i < this.points.Length; i++)
                {
                    min = math.min(this.points[i], min);
                    max = math.max(this.points[i], max);
                }

                this.boundingRect.Value = new Rect(min.x - this.addedMargin, min.y - this.addedMargin,
                    (max.x - min.x) + this.addedMargin, (max.y - min.y) + this.addedMargin);
            }
        }

        [BurstCompile]
        public struct BoundingBoxJob : IJob
        {
            public float addedMargin;

            [NoAlias, ReadOnly]
            public NativeArray<float3> points;

            [NoAlias]
            public NativeReference<Bounds> bounds;

            public void Execute()
            {
#if !GDG_UNSAFE_DEGENERACIES
                if (this.points.Length == 0)
                {
                    this.bounds.Value = new Bounds((float3)float.NaN, (float3)float.NaN);
                    return;
                }
#endif

                float3 min = float.PositiveInfinity;
                float3 max = float.NegativeInfinity;

                for(int i = 0; i < this.points.Length; i++)
                {
                    min = math.min(this.points[i], min);
                    max = math.max(this.points[i], max);
                }

                this.bounds.Value = new Bounds((min + max) * 0.5f, (max - min) + this.addedMargin * 2);
            }
        }


        [BurstCompile]
        public struct MinimumEnclosingDiskJob : IJob
        {
            [ReadOnly, NoAlias]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeReference<float2> center;
            [NoAlias]
            public NativeReference<float> radius;


            private void MinimumDisc2(float2 point0, float2 point1, out float2 center, out float radiusSq)
            {
                var dir = (point1 - point0) * 0.5f;
                center = point0 + dir;
                radiusSq = math.dot(dir, dir);
            }

            public void Execute()
            {
                if (this.points.Length == 0) return;
                if (this.points.Length == 1)
                {
                    this.center.Value = this.points[0];
                    this.radius.Value = 0.0f;
                    return;
                }

                float2 currentCenter = float2.zero;
                float currentRadiusSq = 0.0f;

                //Assumption: Points are in a random permutation already
                //Algorithm: Welzl
                this.MinimumDisc2(this.points[0], this.points[1], out currentCenter, out currentRadiusSq);
                for(int i = 2; i < this.points.Length; i++)
                {
                    var point = this.points[i];
                    if(!ShapeLocation.IsInsideCircle(currentCenter, currentRadiusSq, point))
                    {
                        this.MinimumDisc2(this.points[0], this.points[i], out currentCenter, out currentRadiusSq);
                        for(int j = 1; j < i; j++)
                        {
                            point = this.points[j];
                            if(!ShapeLocation.IsInsideCircle(currentCenter, currentRadiusSq, point))
                            {
                                this.MinimumDisc2(this.points[i], this.points[j], out currentCenter, out currentRadiusSq);
                                for(int k = 0; k < j; k++)
                                {
                                    point = this.points[k];
                                    if(!ShapeLocation.IsInsideCircle(currentCenter, currentRadiusSq, point))
                                    {
                                        var triangle = new NativeTriangle2D(this.points[i], this.points[j], this.points[k]);
                                        NativeTriangle2D.CalculateCircumcircle(triangle, out currentCenter, out currentRadiusSq);

                                    }
                                }
                            }
                        }
                    }
                }
               

                this.center.Value = currentCenter;
                this.radius.Value = math.sqrt(currentRadiusSq);
            }
        }



        [BurstCompile]
        public struct MinimumEnclosingSphereJob : IJob
        {
            [ReadOnly, NoAlias]
            public NativeArray<float3> points;

            [NoAlias]
            public NativeReference<float3> center;
            [NoAlias]
            public NativeReference<float> radius;

            private void MinimumSphere2(float3 point0, float3 point1, out float3 center, out float radiusSq)
            {
                var dir = (point1 - point0) * 0.5f;
                center = point0 + dir;
                radiusSq = math.dot(dir, dir);
            }

            //If we >have< to place three points on the surface of a sphere, the smallest one should be the one where all three points
            //lie on a great circle
            private void MinimumSphere3(float3 point0, float3 point1, float3 point2, out float3 center, out float radiusSq)
            {
                var triangle = new NativeTriangle3D(point0, point1, point2);
                NativeTriangle3D.CalculateCircumcircle(triangle, out center, out radiusSq, 10e-4f, 10e-7f);
            }

            public void Execute()
            {
                if (this.points.Length == 0) return;
                if (this.points.Length == 1)
                {
                    this.center.Value = this.points[0];
                    this.radius.Value = 0.0f;
                    return;
                }

                //Assumption: Points are in a random permutation already

                float3 currentCenter = float3.zero;
                float currentRadiusSq = 0.0f;

                //Assumption: Points are in a random permutation already
                //Algorithm: Welzl (extended to spheres)
                this.MinimumSphere2(this.points[0], this.points[1], out currentCenter, out currentRadiusSq);
                for (int i = 2; i < this.points.Length; i++)
                {
                    var point = this.points[i];
                    if (!ShapeLocation.IsInsideSphere(currentCenter, currentRadiusSq, point))
                    {
                        this.MinimumSphere2(this.points[0], this.points[i], out currentCenter, out currentRadiusSq);
                        for (int j = 1; j < i; j++)
                        {
                            point = this.points[j];
                            if (!ShapeLocation.IsInsideSphere(currentCenter, currentRadiusSq, point))
                            {
                                this.MinimumSphere2(this.points[i], this.points[j], out currentCenter, out currentRadiusSq);
                                for (int k = 0; k < j; k++)
                                {
                                    point = this.points[k];
                                    if (!ShapeLocation.IsInsideSphere(currentCenter, currentRadiusSq, point))
                                    {
                                        this.MinimumSphere3(this.points[i], this.points[j], this.points[k], out currentCenter, out currentRadiusSq);
                                        for(int l = 0; l < k; l++)
                                        {
                                            point = this.points[l];
                                            if(!ShapeLocation.IsInsideSphere(currentCenter, currentRadiusSq, point))
                                            {
                                                var tetrahedron = new Tetrahedron(this.points[i], this.points[j], this.points[k], this.points[l]);
                                                //The points of the tetrahedron can become very close with random points... which is why the epsilon is set to low
                                                //In addition, for large tetrahedra, the lines might not meet perfectly, which is why the merge distance is set to high
                                                //All in all, the parameters are set for maximum stability, but you can change them if you'd like for precision
                                                Tetrahedron.CalculateCircumsphere(tetrahedron, out currentCenter, out currentRadiusSq, 10e-4f, 10e-7f);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }


                this.center.Value = currentCenter;
                this.radius.Value = math.sqrt(currentRadiusSq);
            }
        }





        [BurstCompile]
        public struct AklToussaintBoundaryJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeArray<float2> points;

            [NoAlias]
            public NativeReference<FixedList64Bytes<float2>> bounds;

            public void Execute()
            {
                float2 left = Vector2.positiveInfinity;
                float2 right = Vector2.negativeInfinity;
                float2 bottom = left;
                float2 top = right;

                for (int i = 0; i < this.points.Length; i++)
                {
                    var point = this.points[i];

                    if (Hint.Unlikely(point.x < left.x)) { left = point; }
                    if (Hint.Unlikely(point.x > right.x)) { right = point; }
                    if (Hint.Unlikely(point.y < bottom.y)) { bottom = point; }
                    if (Hint.Unlikely(point.y > top.y)) { top = point; }
                }

                float2 leftToBottom = bottom - left;
                float2 bottomToRight = right - bottom;
                float2 rightToTop = top - right;
                float2 topToLeft = left - top;

                var list = new FixedList64Bytes<float2>();

                //Because it might be that one of the points is equal to another (e.g. top = right),
                //In which case we check for the triangle not the quadrilateral (this is what is not on the Wiki-Page haha)
                if (math.any(leftToBottom != float2.zero)) list.Add(left);
                if (math.any(bottomToRight != float2.zero)) list.Add(bottom);
                if (math.any(rightToTop != float2.zero)) list.Add(right);
                if (math.any(topToLeft != float2.zero)) list.Add(top);

                this.bounds.Value = list;
            }
        }

        [BurstCompile]
        public struct AklToussaintFilterJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeReference<FixedList64Bytes<float2>> convexBound;

            [NoAlias, ReadOnly]
            public NativeArray<float2> inputPoints;

            [NoAlias, WriteOnly]
            public NativeList<float2> outputPoints;

            public void Execute()
            {
                var bound = this.convexBound.Value;

                for (int i = 0; i < this.inputPoints.Length; i++)
                {
                    var point = this.inputPoints[i];

                    float2 start = bound[bound.Length - 1];
                    float2 end;
                    for (int j = 0; j < bound.Length; j++)
                    {
                        end = bound[j];

                        float2 perpEdge = (end - start).yx;
                        float2 pointDir = point - start;
                        perpEdge.y = -perpEdge.y;

                        //All points outside must lie right of one and exactly one of the edges
                        //If a point were to lie to the right of multiple edges, it would be
                        //a point larger in one of the X- or Y-Axis... but this cannot be!

                        //Equivalent Logic:
                        //if(VectorUtil.TurnDirection(start, end, point) >= 0)
                        //{
                        if (Hint.Unlikely(math.dot(perpEdge, pointDir) >= 0))
                        {
                            this.outputPoints.AddNoResize(point);
                            break;
                        }
                        start = end;
                    }
                }
            }
        }

        [BurstCompile]
        public unsafe struct ConvexHullIndicesJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeArray<float2> inputPoints;

            [NoAlias]
            public NativeList<int> indices;

            //Cheaper than calculating angles or determinants
            private bool RightTurnCheck(float2 a, float2 b, float2 c)
            {
                var perp = b - a;
                perp.xy = perp.yx;
                perp.y = -perp.y;
                float2 nextDir = c - a;

                return math.dot(perp, nextDir) >= 0;
            }

            public void Execute()
            {
                NativeList<int> lowerHull = new NativeList<int>(16, Allocator.Temp);

                int pointCount = this.inputPoints.Length;

                lowerHull.Add(0);
                lowerHull.Add(1);

                for (int i = 2; i < pointCount; i++)
                {
                    lowerHull.Add(i);

                    int count = lowerHull.Length;

                    while (Hint.Likely(lowerHull.Length > 2) &&
                        this.RightTurnCheck(this.inputPoints[lowerHull[count - 3]], 
                        this.inputPoints[lowerHull[count - 2]], 
                        this.inputPoints[lowerHull[count - 1]]))
                    {
                        lowerHull.RemoveAt(count - 2);
                        count = lowerHull.Length;
                    }
                }

                this.indices.Add(pointCount - 1);
                this.indices.Add(pointCount - 2);

                for (int i = pointCount - 3; i >= 0; i--)
                {
                    this.indices.Add(i);

                    int count = this.indices.Length;
                    while (Hint.Likely(this.indices.Length > 2) &&
                        this.RightTurnCheck(this.inputPoints[this.indices.ElementAt(count - 3)],
                        this.inputPoints[this.indices.ElementAt(count - 2)],
                        this.inputPoints[this.indices.ElementAt(count - 1)]))
                    {
                        this.indices.RemoveAt(count - 2);
                        count = this.indices.Length;
                    }
                }

                lowerHull.RemoveAt(0);
                lowerHull.RemoveAt(lowerHull.Length - 1);

                this.indices.AddRange(lowerHull.AsArray());

            }
        }


        [BurstCompile]
        public unsafe struct ConvexHullJob : IJob
        {

            [NoAlias, ReadOnly]
            public NativeArray<float2> inputPoints;

            [NoAlias, NativeDisableUnsafePtrRestriction]
            public UnsafeList<float2>* outputPoints;

            //Cheaper than calculating angles or determinants
            private bool RightTurnCheck(float2 a, float2 b, float2 c)
            {
                var perp = b - a;
                perp.xy = perp.yx;
                perp.y = -perp.y;
                float2 nextDir = c - a;

                return math.dot(perp, nextDir) >= 0;
            }

            public void Execute()
            {
                UnsafeList<float2> lowerHull = new UnsafeList<float2>(16, Allocator.Temp);

                int pointCount = this.inputPoints.Length;

                lowerHull.Add(this.inputPoints[0]);
                lowerHull.Add(this.inputPoints[1]);

                for(int i = 2; i < pointCount; i++)
                {
                    lowerHull.Add(this.inputPoints[i]);

                    int count = lowerHull.Length;

                    while (Hint.Likely(lowerHull.Length > 2) &&
                        this.RightTurnCheck(lowerHull[count - 3], lowerHull[count - 2], lowerHull[count - 1]))
                    {
                        lowerHull.RemoveAt(count - 2);
                        count = lowerHull.Length;
                    }
                }

                this.outputPoints->Add(this.inputPoints[pointCount - 1]);
                this.outputPoints->Add(this.inputPoints[pointCount - 2]);

                for(int i = pointCount - 3; i >= 0; i--)
                {
                    this.outputPoints->Add(this.inputPoints[i]);

                    int count = this.outputPoints->Length;
                    while (Hint.Likely(this.outputPoints->Length > 2) &&
                        this.RightTurnCheck(this.outputPoints->ElementAt(count - 3), 
                        this.outputPoints->ElementAt(count - 2), 
                        this.outputPoints->ElementAt(count - 1)))
                    {
                        this.outputPoints->RemoveAt(count - 2);
                        count = this.outputPoints->Length;
                    }
                }

                lowerHull.RemoveAt(0);
                lowerHull.RemoveAt(lowerHull.Length - 1);

                this.outputPoints->AddRange(lowerHull);
                
            }
        }

        //Performance could be potentially improved for a large point set by the use of a tree structure - though the construction time might
        //not make it worth it
        [BurstCompile]
        public unsafe struct ConcaveHullJob : IJob
        {
            public float concavity;
            public float epsilon;


            [NoAlias, ReadOnly]
            public NativeArray<float2> inputPoints;

            [NoAlias, ReadOnly]
            public NativeList<int> convexHullIndices;

            [NoAlias, NativeDisableUnsafePtrRestriction]
            public UnsafeList<float2>* outputPoints;


            public void Execute()
            {
                NativeParallelHashSet<int> concaveIndices = new NativeParallelHashSet<int>(1, Allocator.Temp);
                for(int i = 0; i < this.convexHullIndices.Length; i++)
                {
                    int index = this.convexHullIndices[i];
                    this.outputPoints->Add(this.inputPoints[index]);
                    concaveIndices.Add(index);
                }

                int idx = 0;
                do
                {

                    int currentLength = this.outputPoints->Length;
                    int prevIdx = MathUtilDOTS.Mod(idx - 1, currentLength);
                    int nextIdx = MathUtilDOTS.Mod(idx + 1, currentLength);
                    int nextNextIdx = MathUtilDOTS.Mod(idx + 2, currentLength);

                    var prevPoint = this.outputPoints->ElementAt(prevIdx);
                    var point = this.outputPoints->ElementAt(idx);
                    var nextPoint = this.outputPoints->ElementAt(nextIdx);
                    var nextNextPoint = this.outputPoints->ElementAt(nextNextIdx);

                    var thisEdge = new LineSegment2D(point, nextPoint);
                    var prevEdge = new LineSegment2D(prevPoint, point);
                    var nextEdge = new LineSegment2D(nextPoint, nextNextPoint);


                    float closestPointDist = float.MaxValue;
                    int closestPointIndex = -1;
                    for(int i = 0; i < this.inputPoints.Length; i++)
                    {
                        if (concaveIndices.Contains(i)) continue;

                        var p = this.inputPoints[i];

                        float thisDist = LineSegment2D.Distance(thisEdge, p);
                        float prevDist = LineSegment2D.Distance(prevEdge, p);
                        float nextDist = LineSegment2D.Distance(nextEdge, p);

                        if(thisDist < prevDist && thisDist < nextDist && thisDist < closestPointDist
                            && thisDist > this.epsilon)
                        {
                            closestPointIndex = i;
                            closestPointDist = thisDist;
                        }
                    }

                    bool addedEdge = false;
                    if(closestPointIndex >= 0)
                    {
                        var closestPoint = this.inputPoints[closestPointIndex];

                        float edgeLength = thisEdge.Length();
                        if((edgeLength / closestPointDist) > this.concavity)
                        {
                            this.outputPoints->Insert(idx + 1, closestPoint);
                            concaveIndices.Add(closestPointIndex);
                            addedEdge = true;
                        }
                    }

                    if(!addedEdge) idx++;

                } while (idx < this.outputPoints->Length);


            }
        }

    }
}
