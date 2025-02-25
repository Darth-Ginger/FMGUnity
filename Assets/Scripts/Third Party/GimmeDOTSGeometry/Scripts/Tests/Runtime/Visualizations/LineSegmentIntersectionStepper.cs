using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static GimmeDOTSGeometry.LineIntersectionJobs;
using EventPoint = GimmeDOTSGeometry.LineIntersectionJobs.EventPoint;
using SweepLineComparer = GimmeDOTSGeometry.LineIntersectionJobs.SweepLineComparer;

namespace GimmeDOTSGeometry
{
    public class LineSegmentIntersectionStepper : MonoBehaviour
    {
        #region Public Variables

        public int intersectionVertices = 16;

        public float intersectionRadius = 0.025f;
        public float intersectionThickness = 0.005f;

        #endregion

        #region Private Variables


        private bool isDone = false;


        LineIntersectionJobs.FindLineIntersectionsSweep linesweepJob;

        private NativeList<LineSegment2D> lineSegments;
        private List<Mesh> lineSegmentMeshes;
        private List<MeshRenderer> meshRenderers;

        

        private NativeList<float2> intersections;
        private List<Mesh> intersectionMeshes;

        private NativeRBTree<EventPoint, LineIntersectionJobs.EventPointComparer> eventQueue;
        private NativeRBTree<LineSegment2D, SweepLineComparer> status;

        private NativeParallelMultiHashMap<int, LineSegment2D> lowerSegments;

        private NativeList<LineSegment2D> upperSegments;
        private NativeList<LineSegment2D> innerSegments;

        private NativeList<SegmentTreeCode> treePositions;



        private Material material;
        private Mesh intersectionMesh;

        private LineIntersectionJobs.SweepLineComparer sweepLineComparer;

        #endregion

        public NativeList<LineSegment2D> LineSegments => this.lineSegments;

        public NativeList<float2> Intersections => this.intersections;

        public NativeRBTree<EventPoint, LineIntersectionJobs.EventPointComparer> EventQueue => this.eventQueue;
        public NativeRBTree<LineSegment2D, SweepLineComparer> Status => this.status;

        private NativeList<LineSegment2D> GenerateRandomLineSegments(Rect bounds, int count)
        {
            var list = new NativeList<LineSegment2D>(Allocator.Persistent);

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                float x0 = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);
                float x1 = UnityEngine.Random.Range(bounds.min.x, bounds.max.x);

                float y0 = UnityEngine.Random.Range(bounds.min.y, bounds.max.y);
                float y1 = UnityEngine.Random.Range(bounds.min.y, bounds.max.y);

                LineSegment2D rndSegment = new LineSegment2D()
                {
                    a = new Vector2(x0, y0),
                    b = new Vector2(x1, y1),
                };

                sb.AppendLine("segments.Add(new LineSegment() {");
                sb.AppendLine($"\ta = new Vector2({x0.ToString("F3")}f, {y0.ToString("F3")}f),");
                sb.AppendLine($"\tb = new Vector2({x1.ToString("F3")}f, {y1.ToString("F3")}f),");
                sb.AppendLine("});");

                list.Add(rndSegment);
            }

            Debug.Log(sb.ToString());

            return list;
        }

        private Mesh CreateMeshFromLineSegment(LineSegment2D segment, float thickness)
        {
            var mesh = new Mesh();

            var dir = segment.b - segment.a;
            var perpendicular = new Vector3(dir.y, 0.0f, -dir.x).normalized;

            var vertices = new Vector3[4];
            vertices[0] = new Vector3(segment.a.x, 0.0f, segment.a.y) + perpendicular * thickness * 0.5f;
            vertices[1] = new Vector3(segment.b.x, 0.0f, segment.b.y) + perpendicular * thickness * 0.5f;
            vertices[2] = new Vector3(segment.b.x, 0.0f, segment.b.y) - perpendicular * thickness * 0.5f;
            vertices[3] = new Vector3(segment.a.x, 0.0f, segment.a.y) - perpendicular * thickness * 0.5f;

            var triangles = new int[6] { 2, 1, 0, 3, 2, 0};

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            return mesh;
        }

        private Mesh CreateMeshFromIntersectionPoint(int circlePoints, float radius, float thickness)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[circlePoints * 2];
            var triangles = new int[circlePoints * 2 * 3];

            float angle = 0.0f;
            float angleIncrease = Constants.TAU / (float)circlePoints;

            Vector3 offset = new Vector3(0.0f, 0.0f, 0.0f);
            for(int i = 0; i < circlePoints; i++)
            {
                int innerPointIdx = i;
                int outerPointIdx = i + circlePoints;

                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                Vector3 outerPos = offset + new Vector3(cos, 0.0f, sin) * radius;
                Vector3 innerPos = offset + new Vector3(cos, 0.0f, sin) * (radius - thickness);

                vertices[innerPointIdx] = innerPos;
                vertices[outerPointIdx] = outerPos;

                angle += angleIncrease;

                int triangleStart = innerPointIdx * 6;

                int outerNext = outerPointIdx + 1;
                if (outerNext - circlePoints >= circlePoints) outerNext = circlePoints + (outerNext % circlePoints);

                triangles[triangleStart] = outerNext;
                triangles[triangleStart + 1] = outerPointIdx;
                triangles[triangleStart + 2] = innerPointIdx;
                triangles[triangleStart + 3] = innerPointIdx;
                triangles[triangleStart + 4] = (innerPointIdx + 1) % circlePoints;
                triangles[triangleStart + 5] = outerNext;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            return mesh;

        }

        public bool IsDone() => this.isDone;

        public void Step()
        {

            var faultySegments = new NativeList<LineSegment2D>(Allocator.TempJob);
            var allElements = new NativeList<int>(Allocator.Temp);
            var uniqueSegments = new NativeList<LineSegment2D>(1, Allocator.Temp);
            var tempQueueCopy = new NativeList<LineIntersectionJobs.EventPoint>(Allocator.Temp);

            if (!this.eventQueue.IsEmpty())
            {
                var nextEvent = this.linesweepJob.FetchNextEvent(ref this.eventQueue);
                bool success = true;

                int intersectionCount = this.intersections.Length;
                success &= this.linesweepJob.HandleEventPoint(nextEvent, ref faultySegments,
                    ref this.upperSegments, ref this.innerSegments,
                    ref this.treePositions, ref this.status, ref this.eventQueue);

                if (!success)
                {
                    this.linesweepJob.Restart(faultySegments, allElements, uniqueSegments, tempQueueCopy,
                        ref this.status);
                    faultySegments.Clear();
                }

                if(this.intersections.Length > intersectionCount)
                {
                    this.AddIntersectionMesh(this.intersections.Length - 1, this.intersections[this.intersections.Length - 1]);
                }

                this.upperSegments.Clear();
                this.innerSegments.Clear();
                this.treePositions.Clear();

            }
            else
            {
                this.isDone = true;
            }
            faultySegments.Dispose();
        }


        private void InitIntersectionAlgorithm()
        {
            float epsilon = 10e-4f;

            this.intersections = new NativeList<float2>(Allocator.Persistent);

            this.linesweepJob = new LineIntersectionJobs.FindLineIntersectionsSweep()
            {
                epsilon = epsilon,
                intersections = this.intersections,
                restart = true,
                segments = this.lineSegments,
            };

            var prepareLineSegmentSweep = new LineIntersectionJobs.PrepareLineSegmentsSweep()
            {
                epsilon = epsilon,
                segments = this.lineSegments
            };

            prepareLineSegmentSweep.Schedule(this.lineSegments.Length, 64).Complete();


            this.sweepLineComparer = new LineIntersectionJobs.SweepLineComparer() { epsilon = epsilon };

            this.eventQueue = new NativeRBTree<LineIntersectionJobs.EventPoint, LineIntersectionJobs.EventPointComparer>(
                new LineIntersectionJobs.EventPointComparer()
                {
                    epsilon = 10e-5f,
                },
                Allocator.Persistent);

            this.status = new NativeRBTree<LineSegment2D, LineIntersectionJobs.SweepLineComparer>(this.sweepLineComparer, Allocator.Persistent);

            this.lowerSegments = new NativeParallelMultiHashMap<int, LineSegment2D>(1, Allocator.Persistent);

            this.upperSegments = new NativeList<LineSegment2D>(Allocator.Persistent);
            this.innerSegments = new NativeList<LineSegment2D>(Allocator.Persistent);

            this.treePositions = new NativeList<SegmentTreeCode>(Allocator.Persistent);

            for (int i = 0; i < this.lineSegments.Length; i++)
            {
                this.linesweepJob.InsertSegment(this.lineSegments[i], i, ref this.eventQueue, Allocator.Persistent);
            }
        }

        public void Init(NativeList<LineSegment2D> segments, Color color, float lineThickness)
        {
            this.lineSegments = segments;
            this.lineSegmentMeshes = new List<Mesh>();
            this.meshRenderers = new List<MeshRenderer>();
            this.intersections = new NativeList<float2>();

            var shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                this.material = new Material(shader);
                this.material.SetColor("_Color", color);
                this.material.enableInstancing = true;
            }

            for (int i = 0; i < this.lineSegments.Length; i++)
            {
                var mesh = this.CreateMeshFromLineSegment(this.lineSegments[i], lineThickness);
                this.lineSegmentMeshes.Add(mesh);

                var meshRendererGo = new GameObject($"LineSegmentRenderer_{i}");
                var meshRenderer = meshRendererGo.AddComponent<MeshRenderer>();
                var meshFilter = meshRendererGo.AddComponent<MeshFilter>();

                meshRendererGo.transform.parent = this.transform;
                meshFilter.mesh = mesh;
                meshRenderer.sharedMaterial = this.material;

                this.meshRenderers.Add(meshRenderer);
            }

            this.intersectionMesh = this.CreateMeshFromIntersectionPoint(this.intersectionVertices, this.intersectionRadius, this.intersectionThickness);

            this.InitIntersectionAlgorithm();

            this.isDone = false;
        }

        public void AddIntersectionMesh(int idx, Vector2 point)
        {
            var meshRendererGo = new GameObject($"IntersectionRenderer_{idx}");
            var meshRenderer = meshRendererGo.AddComponent<MeshRenderer>();
            var meshFilter = meshRendererGo.AddComponent<MeshFilter>();

            meshRendererGo.transform.parent = this.transform;
            meshRendererGo.transform.transform.position = new Vector3(point.x, 0.0f, point.y);
            meshRendererGo.transform.transform.rotation = Quaternion.Euler(0.0f, UnityEngine.Random.Range(0.0f, 360.0f), 0.0f);
            meshFilter.sharedMesh = this.intersectionMesh;
            meshRenderer.sharedMaterial = this.material;

            this.meshRenderers.Add(meshRenderer);
        }

        public void Init(Rect bounds, int count, Color color, float lineThickness)
        {
            this.lineSegments = this.GenerateRandomLineSegments(bounds, count);
            this.Init(this.lineSegments, color, lineThickness);
        }

        private void OnDestroy()
        {
            if (this.status.IsCreated)
            {
                this.status.Dispose();
            }
            if(this.eventQueue.IsCreated)
            {
                this.eventQueue.Dispose();
            }
            if(this.lineSegments.IsCreated)
            {
                this.lineSegments.Dispose();
            }

            if(this.lowerSegments.IsCreated)
            {
                this.lowerSegments.Dispose();
            }

            if(this.upperSegments.IsCreated)
            {
                this.upperSegments.Dispose();
            }

            if(this.innerSegments.IsCreated)
            {
                this.innerSegments.Dispose();
            }

            if(this.treePositions.IsCreated)
            {
                this.treePositions.Dispose();
            }

            if(this.intersections.IsCreated)
            {
                this.intersections.Dispose();
            }
        }

        private void Update()
        {
            if (!this.eventQueue.IsEmpty()) {
                var nextEvent = this.eventQueue.GetLeftmostNode();
                if (nextEvent >= 0)
                {
                    var element = this.eventQueue.Elements[nextEvent];
                    var sweepPoint = element.Value.point;
                    Debug.DrawLine(new Vector3(-50.0f, 0.0f, sweepPoint.y), new Vector3(50.0f, 0.0f, sweepPoint.y), Color.green);
                    Debug.DrawLine(new Vector3(sweepPoint.x - 3.03f, 0.0f, sweepPoint.y - 3.03f), new Vector3(sweepPoint.x + 3.03f, 0.0f, sweepPoint.y + 3.03f), Color.green);
                    Debug.DrawLine(new Vector3(sweepPoint.x - 3.03f, 0.0f, sweepPoint.y + 3.03f), new Vector3(sweepPoint.x + 3.03f, 0.0f, sweepPoint.y - 3.03f), Color.green);

                }
            }
        }

        
    }
}
