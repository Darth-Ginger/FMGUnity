using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class MeshSlicingJobs
    {
        public struct EdgeNeighborsEntry
        {
            public int vertex0;
            public int vertex1;

            public int triangleIdx0;
            public int triangleIdx1;

            public int4 neighborHashes;
        }

        public struct LoopEntry
        {
            public int vertexBelow;
            public int vertexAbove;

            public float3 intersection;
        }

        [BurstCompile]
        public struct CreateEdgeNeighborMapJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeArray<int> triangles;

            [NoAlias, ReadOnly]
            public NativeArray<Vector3> vertices;

            [NoAlias]
            public NativeParallelHashMap<int, EdgeNeighborsEntry> neighborMap;


            private void HandleEdgeEntry(int hash, int idxA, int idxB, int triangleIdx, int neighborHashA, int neighborHashB)
            {

                if (!this.neighborMap.ContainsKey(hash))
                {
                    this.neighborMap.Add(hash, new EdgeNeighborsEntry()
                    {
                        vertex0 = idxA,
                        vertex1 = idxB,

                        triangleIdx0 = triangleIdx,
                        triangleIdx1 = -1,

                        neighborHashes = new int4(neighborHashA, neighborHashB, -1, -1),
                    });
                } else
                {
                    var entry = this.neighborMap[hash];
                    entry.triangleIdx1 = triangleIdx;
                    entry.neighborHashes.z = neighborHashA;
                    entry.neighborHashes.w = neighborHashB;
                    this.neighborMap[hash] = entry;
                }
            }

            public void Execute()
            {
                NativeParallelHashMap<float3, int> duplicateVertices = new NativeParallelHashMap<float3, int>(1, Allocator.Temp);
                for(int i = 0; i < this.triangles.Length; i += 3)
                {
                    int idxA = this.triangles[i + 0];
                    int idxB = this.triangles[i + 1];
                    int idxC = this.triangles[i + 2];

                    var vertexA = this.vertices[idxA];
                    var vertexB = this.vertices[idxB];
                    var vertexC = this.vertices[idxC];

                    if(duplicateVertices.ContainsKey(vertexA))
                    {
                        idxA = duplicateVertices[vertexA];
                    } else
                    {
                        duplicateVertices.Add(vertexA, idxA);
                    }
                    if(duplicateVertices.ContainsKey(vertexB))
                    {
                        idxB = duplicateVertices[vertexB];
                    } else
                    {
                        duplicateVertices.Add(vertexB, idxB);
                    }
                    if (duplicateVertices.ContainsKey(vertexC))
                    {
                        idxC = duplicateVertices[vertexC];
                    }
                    else
                    {
                        duplicateVertices.Add(vertexC, idxC);
                    }

                    int triangleIdx = i / 3;

                    int edgeAHash = MathUtilDOTS.EdgeToHash(idxA, idxB);
                    int edgeBHash = MathUtilDOTS.EdgeToHash(idxB, idxC);
                    int edgeCHash = MathUtilDOTS.EdgeToHash(idxC, idxA);

                    this.HandleEdgeEntry(edgeAHash, idxA, idxB, triangleIdx, edgeBHash, edgeCHash);
                    this.HandleEdgeEntry(edgeBHash, idxB, idxC, triangleIdx, edgeCHash, edgeAHash);
                    this.HandleEdgeEntry(edgeCHash, idxC, idxA, triangleIdx, edgeAHash, edgeBHash);
                }

            }
        }

        [BurstCompile(FloatPrecision = FloatPrecision.High)]
        public struct FindLoopsJob : IJob
        {
            public Allocator allocator;

            public float epsilon;

            [NoAlias, ReadOnly]
            public NativeArray<Vector3> vertices;

            [NoAlias, ReadOnly]
            public NativeArray<int> triangles;

            [NoAlias]
            public NativeParallelHashMap<int, EdgeNeighborsEntry> neighborMap;

            [NoAlias]
            public NativeList<UnsafeList<LoopEntry>> loops;

            public Plane plane;

            private LoopEntry CreateLoopEntry(float3 intersection, float3 cmp, int vertex0, int vertex1)
            {
                var loopEntry = new LoopEntry()
                {
                    intersection = intersection,
                };

                if (this.plane.GetDistanceToPoint(cmp) > 0)
                {
                    loopEntry.vertexAbove = vertex0;
                    loopEntry.vertexBelow = vertex1;
                }
                else
                {
                    loopEntry.vertexAbove = vertex1;
                    loopEntry.vertexBelow = vertex0;
                }
                return loopEntry;
            }

            private bool IsNextEdge(LineSegment3D ls, float3 prevIntersection, float3 triNormal, LineSegment3D prevLS, out float3 intersection, 
                out bool addToLoop)
            {
                addToLoop = true;
                bool intersects = ShapeIntersection.PlaneLineSegmentIntersection(this.plane, ls, out intersection, this.epsilon);
                if(intersects)
                {
                    float3 dir = intersection - prevIntersection;
                    bool isCounterclockwise = math.any(math.abs(dir) > this.epsilon)
                        && Vector3.SignedAngle(triNormal, dir, this.plane.normal) < 0.0f;
                    if (isCounterclockwise)
                    {
                        return true;
                    }

                    addToLoop = false;

                    if (math.all(math.abs(dir) < this.epsilon))
                    {
                        bool aIsPlanePoint = math.all(math.abs((float3)ls.a - prevIntersection) < this.epsilon);

                        float3 otherA = math.all(math.abs((float3)prevLS.a - prevIntersection) < this.epsilon) ? prevLS.b : prevLS.a;
                        float3 otherB = aIsPlanePoint ? ls.b : ls.a;

                        float3 hypDir = otherB - otherA;

                        return Vector3.SignedAngle(triNormal, hypDir, this.plane.normal) < 0.0f;

                    }
                }
                return false;

            }

            private void FindLoop(NativeParallelHashSet<int> markedEdges, int startHash, int idxA, int idxB, float3 startIntersection)
            {
                //var debugColor = new Color32((byte)idxA, (byte)idxB, (byte)startHash, 255);

                var loop = new UnsafeList<LoopEntry>(1, this.allocator);

                var loopStartEntry = new LoopEntry()
                {
                    intersection = startIntersection,
                };

                if (this.plane.GetDistanceToPoint(this.vertices[idxA]) > this.plane.GetDistanceToPoint(this.vertices[idxB]))
                {
                    loopStartEntry.vertexAbove = idxA;
                    loopStartEntry.vertexBelow = idxB;
                } else
                {
                    loopStartEntry.vertexAbove = idxB;
                    loopStartEntry.vertexBelow = idxA;
                }

                var neighborEntry = this.neighborMap[startHash];

                loop.Add(loopStartEntry);

                float3 prevIntersection = startIntersection;

                LineSegment3D prevLS = new LineSegment3D()
                {
                    a = this.vertices[idxA],
                    b = this.vertices[idxB],
                };

                bool loopCompleted = false;

                for (;;)
                {

                    int triangleIdx0 = neighborEntry.triangleIdx0;
                    int triangleIdx1 = neighborEntry.triangleIdx1;

                    bool triangle1Exists = triangleIdx1 >= 0;

                    int triIdxA0 = this.triangles[triangleIdx0 * 3 + 0];
                    int triIdxB0 = this.triangles[triangleIdx0 * 3 + 1];

                    int triIdxC0 = this.triangles[triangleIdx0 * 3 + 2];
                    var triVertexA0 = this.vertices[triIdxA0];
                    var triVertexB0 = this.vertices[triIdxB0];
                    var triVertexC0 = this.vertices[triIdxC0];

                    var triangle0 = new NativeTriangle3D() { a = triVertexA0, b = triVertexB0, c = triVertexC0 };
                    var triNormal0 = triangle0.GetNormal();

                    var neighborA = this.neighborMap[neighborEntry.neighborHashes.x];
                    var neighborB = this.neighborMap[neighborEntry.neighborHashes.y];

                    var vertexA0 = this.vertices[neighborA.vertex0];
                    var vertexA1 = this.vertices[neighborA.vertex1];

                    var vertexB0 = this.vertices[neighborB.vertex0];
                    var vertexB1 = this.vertices[neighborB.vertex1];

                    var lsA = new LineSegment3D() { a = vertexA0, b = vertexA1 };
                    var lsB = new LineSegment3D() { a = vertexB0, b = vertexB1 };



                    bool hasNextIntersection = false;
                    if (IsNextEdge(lsA, prevIntersection, triNormal0, prevLS, out float3 intersectionA, 
                        out bool addToLoopA))
                    {
                        //Debug.DrawLine(lsA.a, lsA.b, debugColor);
                        if (neighborEntry.neighborHashes.x == startHash || (loop.Length > 1 && math.all(math.abs(intersectionA - startIntersection) < this.epsilon)))
                        {
                            loopCompleted = true;
                            break;
                        }
                        if (markedEdges.Contains(neighborEntry.neighborHashes.x))
                        {
                            break;
                        }

                        if (addToLoopA)
                        {
                            var loopEntry = this.CreateLoopEntry(intersectionA, vertexA0, neighborA.vertex0, neighborA.vertex1);
                            loop.Add(loopEntry);
                        }

                        markedEdges.Add(neighborEntry.neighborHashes.x);
                        neighborEntry = neighborA;
                        prevIntersection = intersectionA;
                        prevLS = lsA;

                        hasNextIntersection = true;

                    } else if (IsNextEdge(lsB, prevIntersection, triNormal0, prevLS, out float3 intersectionB, 
                        out bool addToLoopB)) {

                        //Debug.DrawLine(lsB.a, lsB.b, debugColor);

                        if (neighborEntry.neighborHashes.y == startHash || (loop.Length > 1 && math.all(math.abs(intersectionB - startIntersection) < this.epsilon)))
                        {
                            loopCompleted = true;
                            break;
                        }
                        if (markedEdges.Contains(neighborEntry.neighborHashes.y))
                        {
                            break;
                        }

                        if (addToLoopB)
                        {
                            var loopEntry = this.CreateLoopEntry(intersectionB, vertexB0, neighborB.vertex0, neighborB.vertex1);
                            loop.Add(loopEntry);
                        }

                        markedEdges.Add(neighborEntry.neighborHashes.y);
                        neighborEntry = neighborB;
                        prevIntersection = intersectionB;
                        prevLS = lsB;



                        hasNextIntersection = true;

                    } else if (triangle1Exists)
                    {
                        int triIdxA1 = this.triangles[triangleIdx1 * 3 + 0];
                        int triIdxB1 = this.triangles[triangleIdx1 * 3 + 1];
                        int triIdxC1 = this.triangles[triangleIdx1 * 3 + 2];

                        var triVertexA1 = this.vertices[triIdxA1];
                        var triVertexB1 = this.vertices[triIdxB1];
                        var triVertexC1 = this.vertices[triIdxC1];

                        var triangle1 = new NativeTriangle3D() { a = triVertexA1, b = triVertexB1, c = triVertexC1 };
                        var triNormal1 = triangle1.GetNormal();

                        var neighborC = this.neighborMap[neighborEntry.neighborHashes.z];
                        var neighborD = this.neighborMap[neighborEntry.neighborHashes.w];

                        var vertexC0 = this.vertices[neighborC.vertex0];
                        var vertexC1 = this.vertices[neighborC.vertex1];

                        var vertexD0 = this.vertices[neighborD.vertex0];
                        var vertexD1 = this.vertices[neighborD.vertex1];

                        var lsC = new LineSegment3D() { a = vertexC0, b = vertexC1 };
                        var lsD = new LineSegment3D() { a = vertexD0, b = vertexD1 };

                        if (IsNextEdge(lsC, prevIntersection, triNormal1, prevLS, out float3 intersectionC, 
                            out bool addToLoopC)) {

                            //Debug.DrawLine(lsC.a, lsC.b, debugColor);
                            if (neighborEntry.neighborHashes.z == startHash || (loop.Length > 1 && math.all(math.abs(intersectionC - startIntersection) < this.epsilon)))
                            {
                                loopCompleted = true;
                                break;
                            }
                            if (markedEdges.Contains(neighborEntry.neighborHashes.z))
                            {
                                break;
                            }

                            if (addToLoopC)
                            {
                                var loopEntry = this.CreateLoopEntry(intersectionC, vertexC0, neighborC.vertex0, neighborC.vertex1);
                                loop.Add(loopEntry);
                            }

                            markedEdges.Add(neighborEntry.neighborHashes.z);
                            neighborEntry = neighborC;
                            prevIntersection = intersectionC;
                            prevLS = lsC;


                            hasNextIntersection = true;
                        }

                        else if (IsNextEdge(lsD, prevIntersection, triNormal1, prevLS, out float3 intersectionD, 
                            out bool addToLoopD))
                        {
                            //Debug.DrawLine(lsD.a, lsD.b, debugColor);
                            if (neighborEntry.neighborHashes.w == startHash || (loop.Length > 1 && math.all(math.abs(intersectionD - startIntersection) < this.epsilon)))
                            {
                                loopCompleted = true;
                                break;
                            }
                            if (markedEdges.Contains(neighborEntry.neighborHashes.w))
                            {
                                break;
                            }

                            if (addToLoopD)
                            {
                                var loopEntry = this.CreateLoopEntry(intersectionD, vertexD0, neighborD.vertex0, neighborD.vertex1);
                                loop.Add(loopEntry);
                            }

                            markedEdges.Add(neighborEntry.neighborHashes.w);
                            neighborEntry = neighborD;
                            prevIntersection = intersectionD;
                            prevLS = lsD;


                            hasNextIntersection = true;

                        }
                    }

                    if (!hasNextIntersection)
                    {
                        break;
                    }
                }

                if (loopCompleted && loop.Length > 2)
                {
                    this.loops.Add(loop);
                }
            }

            private void HandleEdge(NativeParallelHashSet<int> markedEdges, int hash, int idxA, int idxB, LineSegment3D ls)
            {
                if (!markedEdges.Contains(hash))
                {
                    markedEdges.Add(hash);
                    if (ShapeIntersection.PlaneLineSegmentIntersection(this.plane, ls, out float3 i0, this.epsilon))
                    {
                        this.FindLoop(markedEdges, hash, idxA, idxB, i0);
                    }

                }
            }

            public void Execute()
            {
                NativeParallelHashSet<int> markedEdges = new NativeParallelHashSet<int>(this.triangles.Length, Allocator.Temp);
                NativeParallelHashMap<float3, int> duplicateVertices = new NativeParallelHashMap<float3, int>(1, Allocator.Temp);
                for (int i = 0; i < this.triangles.Length; i += 3)
                {
                    int idxA = this.triangles[i + 0];
                    int idxB = this.triangles[i + 1];
                    int idxC = this.triangles[i + 2];

                    var vertexA = this.vertices[idxA];
                    var vertexB = this.vertices[idxB];
                    var vertexC = this.vertices[idxC];

                    if (duplicateVertices.ContainsKey(vertexA))
                    {
                        idxA = duplicateVertices[vertexA];
                    }
                    else
                    {
                        duplicateVertices.Add(vertexA, idxA);
                    }
                    if (duplicateVertices.ContainsKey(vertexB))
                    {
                        idxB = duplicateVertices[vertexB];
                    }
                    else
                    {
                        duplicateVertices.Add(vertexB, idxB);
                    }
                    if (duplicateVertices.ContainsKey(vertexC))
                    {
                        idxC = duplicateVertices[vertexC];
                    }
                    else
                    {
                        duplicateVertices.Add(vertexC, idxC);
                    }

                    var lsAB = new LineSegment3D() { a = vertexA, b = vertexB };
                    var lsBC = new LineSegment3D() { a = vertexB, b = vertexC };
                    var lsCA = new LineSegment3D() { a = vertexC, b = vertexA };

                    int edgeAHash = MathUtilDOTS.EdgeToHash(idxA, idxB);
                    int edgeBHash = MathUtilDOTS.EdgeToHash(idxB, idxC);
                    int edgeCHash = MathUtilDOTS.EdgeToHash(idxC, idxA);

                    this.HandleEdge(markedEdges, edgeAHash, idxA, idxB, lsAB);
                    this.HandleEdge(markedEdges, edgeBHash, idxB, idxC, lsBC);
                    this.HandleEdge(markedEdges, edgeCHash, idxC, idxA, lsCA);
                }

            }
        }

        [BurstCompile]
        public struct DisposePolygonsJob : IJob
        {
            [NoAlias]
            public NativeList<NativePolygon2D> polygons;

            public void Execute()
            {
                for(int i = 0; i < this.polygons.Length; i++)
                {
                    this.polygons[i].Dispose();
                }
            }
        }

        [BurstCompile]
        public struct CreatePolygonsFromLoopsJob : IJob
        {
            public Allocator allocator;

            public float epsilon;

            [NoAlias]
            public NativeList<UnsafeList<LoopEntry>> loops;

            [NoAlias]
            public NativeList<NativePolygon2D> polygons;

            [NoAlias]
            public NativeReference<float3> xAxisRef;
            [NoAlias]
            public NativeReference<float3> yAxisRef;
            [NoAlias]
            public NativeReference<Rect> uvBounds;

            public Plane plane;


            public void Execute()
            {
                float3 xAxis = float3.zero, yAxis = float3.zero;

                if (this.loops.Length <= 0) return;


                //Choose them canonically with the Y-Axis

                if (math.abs(math.dot(new float3(0, 1, 0), this.plane.normal)) < 1.0f - this.epsilon)
                {
                    xAxis = math.cross(new float3(0, 1, 0), this.plane.normal);
                    yAxis = math.cross(xAxis, this.plane.normal);
                } else
                {
                    xAxis = new float3(1, 0, 0);
                    yAxis = new float3(0, 0, 1);
                }

                //Alternative - can be inconsistent
                /*
                var planePoint = this.plane.distance * this.plane.normal;
                var firstLoop = this.loops[0];

                for(int i = 0; i < firstLoop.Length; i++)
                {
                    var point = firstLoop[i].intersection;
                    if(math.any(point != (float3)planePoint))
                    {
                        xAxis = (float3)planePoint - point;
                        yAxis = math.cross(xAxis, this.plane.normal);

                        xAxis = math.normalize(xAxis);
                        yAxis = math.normalize(yAxis);
                    }
                }*/

                this.xAxisRef.Value = xAxis;
                this.yAxisRef.Value = yAxis;

                float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;

                for(int i = 0; i < this.loops.Length; i++)
                {
                    var loop = this.loops[i];
                    NativePolygon2D polygon = new NativePolygon2D(this.allocator, loop.Length);

                    for (int j = 0; j < loop.Length; j++)
                    {
                        var loopEntry = loop[j];
                        var point = loopEntry.intersection;

                        float x = VectorUtil.ScalarProjection(point, xAxis);
                        float y = VectorUtil.ScalarProjection(point, yAxis);

                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;

                        var p = new float2(x, y);
                        polygon.points.Add(p);
                    }

                    this.polygons.Add(polygon);
                }

                this.uvBounds.Value = Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
        }

        [BurstCompile]
        public struct CreateShellsJob : IJob
        {
            public float epsilon;

            [NoAlias, ReadOnly]
            public NativeArray<Vector3> vertices;
            [NoAlias, ReadOnly]
            public NativeArray<int> triangles;
            [NoAlias, ReadOnly]
            public NativeArray<Vector2> uvs;

            [NoAlias]
            public NativeList<Vector3> shellAVertices;
            [NoAlias]
            public NativeList<Vector3> shellBVertices;

            [NoAlias]
            public NativeList<int> shellATriangles;
            [NoAlias]
            public NativeList<int> shellBTriangles;

            [NoAlias]
            public NativeList<Vector2> shellAUVs;
            [NoAlias]
            public NativeList<Vector2> shellBUVs;

            public Plane plane;

            private void AddToShellMap(NativeParallelHashMap<int, int> shellMap, NativeList<Vector3> shellVertices, 
                NativeList<int> shellTriangles, NativeList<Vector2> shellUVs, Vector3 vertex, Vector2 uv, int triangleIdx)
            {
                if (!shellMap.ContainsKey(triangleIdx))
                {
                    int shellVertexCount = shellVertices.Length;

                    shellVertices.Add(vertex);
                    shellUVs.Add(uv);

                    shellMap.Add(triangleIdx, shellVertexCount);
                    shellTriangles.Add(shellVertexCount);
                }
                else
                {
                    int idx = shellMap[triangleIdx];
                    shellTriangles.Add(idx);
                }
            }

            private void Split(NativeParallelHashMap<int, int> shellAMap, NativeParallelHashMap<int, int> shellBMap, float3 i0, float3 i1, float2 uv0,
                float2 uv1, float otherDist, float3 upperVertex, int upperIdx, float3 lowerVertex0, int lowerIdx0, float3 lowerVertex1, int lowerIdx1)
            {

                this.shellAVertices.Add(i0);
                this.shellAVertices.Add(i1);

                this.shellBVertices.Add(i0);
                this.shellBVertices.Add(i1);

                this.shellAUVs.Add(uv0);
                this.shellAUVs.Add(uv1);

                this.shellBUVs.Add(uv0);
                this.shellBUVs.Add(uv1);

                int shellAVertexCount = this.shellAVertices.Length;
                int shellBVertexCount = this.shellBVertices.Length;

                var upperTriangles = this.shellATriangles;
                var lowerTriangles = this.shellBTriangles;

                var upperVertices = this.shellAVertices;
                var lowerVertices = this.shellBVertices;

                var upperUVs = this.shellAUVs;
                var lowerUVs = this.shellBUVs;

                var upperShell = shellAMap;
                var lowerShell = shellBMap;

                int upperShellCount = shellAVertexCount;
                int lowerShellCount = shellBVertexCount;


                if (otherDist <= 0.0f)
                {
                    upperTriangles = this.shellBTriangles;
                    lowerTriangles = this.shellATriangles;

                    upperVertices = this.shellBVertices;
                    lowerVertices = this.shellAVertices;

                    upperUVs = this.shellBUVs;
                    lowerUVs = this.shellAUVs;

                    upperShell = shellBMap;
                    lowerShell = shellAMap;

                    upperShellCount = shellBVertexCount;
                    lowerShellCount = shellAVertexCount;
                }

                this.AddToShellMap(upperShell, upperVertices, upperTriangles, upperUVs, upperVertex, this.uvs[upperIdx], upperIdx);
                upperTriangles.Add(upperShellCount - 1);
                upperTriangles.Add(upperShellCount - 2);

                this.AddToShellMap(lowerShell, lowerVertices, lowerTriangles, lowerUVs, lowerVertex0, this.uvs[lowerIdx0], lowerIdx0);
                this.AddToShellMap(lowerShell, lowerVertices, lowerTriangles, lowerUVs, lowerVertex1, this.uvs[lowerIdx1], lowerIdx1);
                lowerTriangles.Add(lowerShellCount - 1);

                lowerTriangles.Add(lowerShellCount - 1);
                this.AddToShellMap(lowerShell, lowerVertices, lowerTriangles, lowerUVs, lowerVertex1, this.uvs[lowerIdx1], lowerIdx1);
                lowerTriangles.Add(lowerShellCount - 2);
            }

            public void Execute()
            {
                NativeParallelHashMap<int, int> shellAMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
                NativeParallelHashMap<int, int> shellBMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);

                var planePoint = this.plane.normal * this.plane.distance;

                for(int i = 0; i < this.triangles.Length; i += 3)
                {
                    int idxA = this.triangles[i + 0];
                    int idxB = this.triangles[i + 1];
                    int idxC = this.triangles[i + 2];

                    var vertexA = this.vertices[idxA];
                    var vertexB = this.vertices[idxB];
                    var vertexC = this.vertices[idxC];

                    var uvA = this.uvs[idxA];
                    var uvB = this.uvs[idxB];
                    var uvC = this.uvs[idxC];

                    float3 distances;

                    float3 dirA = vertexA - planePoint;
                    float3 dirB = vertexB - planePoint;
                    float3 dirC = vertexC - planePoint;

                    distances.x = math.dot(dirA, this.plane.normal); 
                    distances.y = math.dot(dirB, this.plane.normal);
                    distances.z = math.dot(dirC, this.plane.normal);

                    if(math.all(distances >= -this.epsilon))
                    {
                        this.AddToShellMap(shellAMap, this.shellAVertices, this.shellATriangles, this.shellAUVs, vertexA, uvA, idxA);
                        this.AddToShellMap(shellAMap, this.shellAVertices, this.shellATriangles, this.shellAUVs, vertexB, uvB, idxB);
                        this.AddToShellMap(shellAMap, this.shellAVertices, this.shellATriangles, this.shellAUVs, vertexC, uvC, idxC);

                    } else if(math.all(distances <= this.epsilon))
                    {
                        this.AddToShellMap(shellBMap, this.shellBVertices, this.shellBTriangles, this.shellBUVs, vertexA, uvA, idxA);
                        this.AddToShellMap(shellBMap, this.shellBVertices, this.shellBTriangles, this.shellBUVs, vertexB, uvB, idxB);
                        this.AddToShellMap(shellBMap, this.shellBVertices, this.shellBTriangles, this.shellBUVs, vertexC, uvC, idxC);

                    } else
                    {
                        var lsA = new LineSegment3D() { a = vertexA, b = vertexB };
                        var lsB = new LineSegment3D() { a = vertexB, b = vertexC };
                        var lsC = new LineSegment3D() { a = vertexC, b = vertexA };

                        if(distances.x * distances.y >= 0.0f)
                        {
                            ShapeIntersection.PlaneLineSegmentIntersection(this.plane, lsB, out float3 i0, this.epsilon);
                            ShapeIntersection.PlaneLineSegmentIntersection(this.plane, lsC, out float3 i1, this.epsilon);

                            float dB = VectorUtil.ScalarProjection(i0 - (float3)lsB.a, lsB.b - lsB.a);
                            float dC = VectorUtil.ScalarProjection(i1 - (float3)lsC.a, lsC.b - lsC.a);

                            Vector2 uv0 = Vector2.Lerp(uvB, uvC, dB);
                            Vector2 uv1 = Vector2.Lerp(uvC, uvA, dC);

                            this.Split(shellAMap, shellBMap, i0, i1, uv0, uv1, distances.z, vertexC, idxC, vertexA, idxA, vertexB, idxB);

                        } else if(distances.y * distances.z >= 0.0f)
                        {
                            ShapeIntersection.PlaneLineSegmentIntersection(this.plane, lsC, out float3 i0, this.epsilon);
                            ShapeIntersection.PlaneLineSegmentIntersection(this.plane, lsA, out float3 i1, this.epsilon);

                            float dC = VectorUtil.ScalarProjection(i0 - (float3)lsC.a, lsC.b - lsC.a);
                            float dA = VectorUtil.ScalarProjection(i1 - (float3)lsA.a, lsA.b - lsA.a);

                            Vector2 uv0 = Vector2.Lerp(uvC, uvA, dC);
                            Vector2 uv1 = Vector2.Lerp(uvA, uvB, dA);

                            this.Split(shellAMap, shellBMap, i0, i1, uv0, uv1, distances.x, vertexA, idxA, vertexB, idxB, vertexC, idxC);

                        } else
                        {
                            ShapeIntersection.PlaneLineSegmentIntersection(this.plane, lsA, out float3 i0, this.epsilon);
                            ShapeIntersection.PlaneLineSegmentIntersection(this.plane, lsB, out float3 i1, this.epsilon);

                            float dA = VectorUtil.ScalarProjection(i0 - (float3)lsA.a, lsA.b - lsA.a);
                            float dB = VectorUtil.ScalarProjection(i1 - (float3)lsB.a, lsB.b - lsB.a);

                            Vector2 uv0 = Vector2.Lerp(uvA, uvB, dA);
                            Vector2 uv1 = Vector2.Lerp(uvB, uvC, dB);

                            this.Split(shellAMap, shellBMap, i0, i1, uv0, uv1, distances.y, vertexB, idxB, vertexC, idxC, vertexA, idxA);
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public struct CombinePolygonsJob : IJob
        {
            [NoAlias]
            public NativeList<NativePolygon2D> polygons;

            public Plane plane;

            private bool IsClockwise(NativePolygon2D polygon)
            {
                float totalAngle = 0.0f;
                for(int i = 0; i < polygon.points.Length; i++)
                {
                    float2 a = polygon.points[i];
                    float2 b = polygon.points[(i + 1) % polygon.points.Length];
                    float2 c = polygon.points[(i + 2) % polygon.points.Length];

                    float2 dirAB = b - a;
                    float2 dirBC = c - b;

                    totalAngle += Vector2.SignedAngle(dirAB, dirBC);
                }
                return totalAngle < 0.0f;
            }

            public void Execute()
            {
                NativeList<NativePolygon2D> clockwise = new NativeList<NativePolygon2D>(1, Allocator.Temp);
                NativeList<NativePolygon2D> counterclockwise = new NativeList<NativePolygon2D>(1, Allocator.Temp);

                for(int i = 0; i < this.polygons.Length; i++)
                {
                    var poly = this.polygons[i];
                    if(this.IsClockwise(poly))
                    {
                        clockwise.Add(poly);
                    } else
                    {
                        counterclockwise.Add(poly);
                    }
                }

                for(int i = 0; i < clockwise.Length; i++)
                {
                    var clockwisePoly = clockwise[i];
                    var p = clockwisePoly.points[0];
                    for(int j = 0; j < counterclockwise.Length; j++)
                    {
                        var poly = counterclockwise[j];
                        if(poly.IsPointInside(p))
                        {
                            poly.separators.Add(poly.points.Length);
                            poly.points.AddRange(clockwisePoly.points);

                            break;
                        }
                    }
                }

                for (int i = 0; i < clockwise.Length; i++)
                {
                    clockwise[i].Dispose();
                }
                this.polygons.Clear();
                for(int i = 0; i < counterclockwise.Length; i++)
                {
                    this.polygons.Add(counterclockwise[i]);
                }
            }
        }

    }
}
