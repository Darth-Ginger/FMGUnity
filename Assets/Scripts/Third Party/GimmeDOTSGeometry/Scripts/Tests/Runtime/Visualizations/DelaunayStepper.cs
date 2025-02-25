using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static GimmeDOTSGeometry.Delaunay2DJobs;

namespace GimmeDOTSGeometry
{
    public class DelaunayStepper : MonoBehaviour
    {
        #region Public Fields

        public bool finished = false;
        public bool hasDrawnTriangles = false;

        #endregion

        #region Private Fields

        private Delaunay2DJobs.DelaunayTriangulationJob delaunayJob;

        private float lineThickness;

        private int currentPoint = 0;

        private List<MeshRenderer> meshRenderer = new List<MeshRenderer>();

        private List<MeshFilter> halfEdgeMeshes = new List<MeshFilter>();

        private Material material;

        private NativeList<float2> points;

        private NativeGraph dag;
        private NativeList<int3> triangulation;
        private NativeList<HalfEdge> halfEdges;
        private NativeList<Delaunay2DJobs.DelaunayTriangleData> triangleBuffer;
        private NativeParallelHashMap<int, int> halfEdgeToTriangleMap;

        #endregion

        private void Dispose()
        {
            this.dag.Dispose();
            this.triangleBuffer.DisposeIfCreated();
            this.triangulation.DisposeIfCreated();
            this.halfEdges.DisposeIfCreated();
            this.halfEdgeToTriangleMap.DisposeIfCreated();
            this.points.Dispose();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }

        private void InitDelaunay()
        {
            this.dag = new NativeGraph(Allocator.Persistent);
            this.triangulation = new NativeList<int3>(Allocator.Persistent);
            this.halfEdges = new NativeList<HalfEdge>(Allocator.Persistent);
            this.triangleBuffer = new NativeList<Delaunay2DJobs.DelaunayTriangleData>(Allocator.Persistent);
            this.halfEdgeToTriangleMap = new NativeParallelHashMap<int, int>(1, Allocator.Persistent);

            this.delaunayJob = new Delaunay2DJobs.DelaunayTriangulationJob()
            {
                dag = this.dag,
                halfEdges = this.halfEdges,
                points = this.points.AsArray(),
                triangleBuffer = this.triangleBuffer,
                triangulation = this.triangulation,
            };

            int p0 = 0;
            int pMinusOne = -1;
            int pMinusTwo = -2;

            this.triangleBuffer.Add(new DelaunayTriangleData()
            {
                triangle = new int3(p0, pMinusOne, pMinusTwo),
                halfEdgeIdx = 0
            });

            this.dag.AddVertex(0);

            this.halfEdges.Add(new HalfEdge()
            {
                back = 2,
                fwd = 1,
                twin = -1,
                vertexBack = -1,
                vertexFwd = 0
            });

            this.halfEdges.Add(new HalfEdge()
            {
                back = 0,
                fwd = 2,
                twin = -1,
                vertexBack = 0,
                vertexFwd = -2,
            });

            this.halfEdges.Add(new HalfEdge()
            {
                back = 1,
                fwd = 0,
                twin = -1,
                vertexBack = -2,
                vertexFwd = -1
            });

            this.halfEdgeToTriangleMap.Clear();

            this.halfEdgeToTriangleMap.Add(0, 0);
            this.halfEdgeToTriangleMap.Add(1, 0);
            this.halfEdgeToTriangleMap.Add(2, 0);

            this.currentPoint = 1;
        }


        public void Init(NativeList<float2> points, float ringRadius, float lineThickness)
        {
            this.lineThickness = lineThickness;

            var findLargestAndPermuteJob = new Delaunay2DJobs.FindLexicographicLargestJob()
            {
                points = points.AsArray(),
            };

            findLargestAndPermuteJob.Schedule().Complete();

            this.points = points;

            var shader = Shader.Find("Unlit/Color");
            if(shader != null)
            {
                this.material = new Material(shader);
                this.material.color = Color.cyan;
                this.material.enableInstancing = true;
            }

            var mesh = MeshUtil.CreateRing(ringRadius, lineThickness * 0.3f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0.2f, 0.5f, 0.5f));
            for(int i = 0; i < points.Length; i++)
            {
                var ringGo = new GameObject($"Point_{i}");
                ringGo.transform.position = new Vector3(points[i].x, 0.01f, points[i].y);
                var mr = ringGo.AddComponent<MeshRenderer>();
                var mf = ringGo.AddComponent<MeshFilter>();

                mf.sharedMesh = mesh;
                mr.sharedMaterial = this.material;
                mr.SetPropertyBlock(mpb);

                mr.enabled = false;
                this.meshRenderer.Add(mr);
            }
            this.meshRenderer[0].enabled = true;

            this.InitDelaunay();

            this.finished = false;
        }

        private void CreateTriangles()
        {
            for (int i = 0; i < this.triangulation.Length; i++)
            {
                var triangleIndices = this.triangulation[i];

                float3 a = this.points[triangleIndices.x].AsFloat3(CardinalPlane.XZ);
                float3 b = this.points[triangleIndices.y].AsFloat3(CardinalPlane.XZ);
                float3 c = this.points[triangleIndices.z].AsFloat3(CardinalPlane.XZ);

                var triangle = new NativeTriangle3D(c, b, a);

                var triangleMesh = MeshUtil.CreateTriangle(triangle);

                var triangleGo = new GameObject($"Triangle_{i}");
                triangleGo.transform.parent = this.transform;

                var mf = triangleGo.AddComponent<MeshFilter>();
                mf.mesh = triangleMesh;

                var mr = triangleGo.AddComponent<MeshRenderer>();
                mr.material = this.material;
            }

        }

        private void DrawHalfEdges()
        {
            int meshCount = this.halfEdgeMeshes.Count; ;
            int halfEdgeCount = this.halfEdges.Length;

            int meshCounter = 0;
            int counter = 0;
            while(meshCounter <  meshCount && counter < halfEdgeCount) 
            {

                var halfEdge = this.halfEdges[counter];
                if (halfEdge.vertexFwd >= 0 && halfEdge.vertexBack >= 0)
                {
                    var meshFilter = this.halfEdgeMeshes[meshCounter];

                    var vertex0 = this.points[halfEdge.vertexFwd];
                    var vertex1 = this.points[halfEdge.vertexBack];

                    var dir = vertex0 - vertex1;
                    var perp = math.normalize(dir.Perpendicular());
                    vertex0 -= this.lineThickness * 0.3f * perp;
                    vertex1 -= this.lineThickness * 0.3f * perp;

                    var ls = new LineSegment2D(vertex0, vertex1);

                    meshFilter.mesh = MeshUtil.CreateArrow2D(ls, this.lineThickness * 0.1f, 0.2f, 0.1f);
                    meshCounter++;
                }
                counter++;
            }

            for(int i = counter; i < halfEdgeCount; i++)
            {
                var halfEdge = this.halfEdges[i];
                if (halfEdge.vertexFwd >= 0 && halfEdge.vertexBack >= 0) {

                    var halfEdgeGo = new GameObject($"HalfEdge_{i}");
                    var mr = halfEdgeGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = this.material;

                    var meshFilter = halfEdgeGo.AddComponent<MeshFilter>();

                    var vertex0 = this.points[halfEdge.vertexFwd];
                    var vertex1 = this.points[halfEdge.vertexBack];

                    var dir = vertex0 - vertex1;
                    var perp = math.normalize(dir.Perpendicular());
                    vertex0 -= this.lineThickness * 0.3f * perp;
                    vertex1 -= this.lineThickness * 0.3f * perp;

                    var ls = new LineSegment2D(vertex0, vertex1);

                    meshFilter.mesh = MeshUtil.CreateArrow2D(ls, this.lineThickness * 0.1f, 0.2f, 0.1f);
                    this.halfEdgeMeshes.Add(meshFilter);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!this.hasDrawnTriangles)
            {

                var style = new GUIStyle();
                style.normal.textColor = Color.cyan;
                style.fontStyle = FontStyle.Bold;

                int halfEdgeCount = this.halfEdges.Length;
                for (int i = 0; i < halfEdgeCount; i++)
                {
                    var halfEdge = this.halfEdges[i];

                    if (halfEdge.vertexFwd >= 0 && halfEdge.vertexBack >= 0)
                    {
                        var vertex0 = this.points[halfEdge.vertexFwd];
                        var vertex1 = this.points[halfEdge.vertexBack];

                        float2 dir = vertex1 - vertex0;
                        var perp = dir.Perpendicular();
                        float2 avgPos = (vertex0 + vertex1) * 0.5f;

                        float2 labelPos = avgPos - math.normalize(perp) * this.lineThickness;

                        float3 labelPos3D = new float3(labelPos.x, 0.1f, labelPos.y);
                        Handles.Label(labelPos3D, $"{i}", style);
                    }
                }

                for(int i = 0; i < this.currentPoint; i++)
                {
                    float2 labelPos = this.points[i];
                    float3 labelPos3D = new float3(labelPos.x, 0.1f, labelPos.y + 0.2f);
                    Handles.Label(labelPos3D, $"P{i}", style);
                }


            }
        }

        public void Step()
        {
            if (this.currentPoint < this.points.Length)
            {
                float2 point = this.points[this.currentPoint];

                int triangleBufferIdx = this.delaunayJob.FindTriangle(this.currentPoint);

                var triangleData = this.triangleBuffer[triangleBufferIdx];

                var halfEdgeIdx = triangleData.halfEdgeIdx;
                var halfEdge = this.halfEdges[halfEdgeIdx];

                int collinearEdgeIdx = halfEdgeIdx;

                bool isOnEdge = this.delaunayJob.AreCollinear(halfEdge.vertexBack, halfEdge.vertexFwd, point);
                if (!isOnEdge)
                {
                    while (halfEdge.fwd != halfEdgeIdx)
                    {
                        collinearEdgeIdx = halfEdge.fwd;
                        halfEdge = this.halfEdges[halfEdge.fwd];
                        if (this.delaunayJob.AreCollinear(halfEdge.vertexBack, halfEdge.vertexFwd, point))
                        {
                            isOnEdge = true;
                            break;
                        }
                    }
                }

                if (isOnEdge)
                {
                    this.delaunayJob.SplitIntoFour(this.currentPoint, collinearEdgeIdx, triangleBufferIdx);
                }
                else
                {
                    this.delaunayJob.SplitIntoThree(this.currentPoint, halfEdgeIdx, triangleBufferIdx);
                }

                this.meshRenderer[this.currentPoint].enabled = true;
                if (this.currentPoint + 1 < this.points.Length) this.meshRenderer[this.currentPoint + 1].enabled = true;
                this.DrawHalfEdges();

                this.currentPoint++;
            } else if(!this.hasDrawnTriangles)
            {
                this.delaunayJob.CreateTriangulation();
                this.CreateTriangles();

                this.hasDrawnTriangles = true;

            } else
            {
                this.finished = true;
            }
        }
    }
}
