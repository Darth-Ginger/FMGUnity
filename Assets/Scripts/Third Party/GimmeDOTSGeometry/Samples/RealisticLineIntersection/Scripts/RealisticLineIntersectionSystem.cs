
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using static UnityEngine.ParticleSystem;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{

    public class RealisticLineIntersectionSystem : MonoBehaviour
    {

        public enum IntersectionMethod
        {
            COMBINATORICAL = 0,
            COMBINATORICAL_PARALLEL = 1,
            SWEEP = 2
        }

        #region Public Variables

        public float radius;
        public float angularVelocity;

        public float radialStart;
        public float radialEnd;

        public float spiralA;
        public float spiralB;

        public GameObject pointGO;
        public GameObject segmentGO;

        public int startRadialLines;
        public int startLineDivisions;

        public IntersectionMethod intersectionMethod;

        public Material boundingCircleMaterial;

        public ParticleSystem intersectionParticles;


        #endregion

        #region Private Variables

        private float timer = 0.0f;

        private JobHandle updateMovementHandle = default;

        private NativeList<float2> intersections = new NativeList<float2>();
        private NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>();
        private NativeList<LineSegment2D> segmentsCopy = new NativeList<LineSegment2D>();

        private Sampler lineIntersectionSampler = null;

        private TransformAccessArray pointsAccessArray;
        private TransformAccessArray segmentsAccessArray;

        #endregion

        private static readonly ProfilerMarker lineIntersectionMarker = new ProfilerMarker("LineIntersection");

        public Sampler GetLineIntersectionSampler() => this.lineIntersectionSampler;

        public int NrOfSegments
        {
            get
            {
                this.updateMovementHandle.Complete();
                return this.segments.Length;
            }
        }

        public int NrOfIntersections => this.intersections.Length;

        [BurstCompile]
        private struct UpdateSegmentPointsJob : IJobParallelForTransform
        {

            [ReadOnly, NoAlias, NativeDisableParallelForRestriction]
            public NativeList<LineSegment2D> segments;

            public void Execute(int index, TransformAccess transform)
            {
                var segment = this.segments[index / 2];
                var dir = segment.b - segment.a;

                if (index % 2 == 0)
                {
                    transform.position = new Vector3(segment.a.x, 0.0f, segment.a.y);
                }
                else
                {
                    transform.position = new Vector3(segment.b.x, 0.0f, segment.b.y);
                }
                transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0.0f, dir.y));
            }
        }

        [BurstCompile]
        private struct UpdateSegmentsJob : IJobParallelForTransform
        {
            public float deltaTime;
            public float radius;
            public float angularVelocity;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<LineSegment2D> segments;

            public void Execute(int index, TransformAccess transform)
            {
                var segment = this.segments[index];


                float distA = math.length(segment.a);
                float distB = math.length(segment.b);

                float angleA = Vector2.SignedAngle(Vector2.right, segment.a);
                float angleB = Vector2.SignedAngle(Vector2.right, segment.b);

                if (angleA < 0.0f) angleA += 360.0f;
                if (angleB < 0.0f) angleB += 360.0f;

                angleA += this.angularVelocity * this.deltaTime;
                angleB += this.angularVelocity * this.deltaTime;

                angleA *= Mathf.Deg2Rad;
                angleB *= Mathf.Deg2Rad;

                segment.a.x = math.cos(angleA) * distA;
                segment.a.y = math.sin(angleA) * distA;
                segment.b.x = math.cos(angleB) * distB;
                segment.b.y = math.sin(angleB) * distB;

                this.segments[index] = segment;

                var center = (segment.a + segment.b) * 0.5f;
                var dir = segment.b - segment.a;

                transform.position = new Vector3(center.x, 0.0f, center.y);
                transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0.0f, dir.y));
                transform.localScale = new Vector3(1.0f, 1.0f, dir.magnitude);
            }
        }

        private Mesh CreateCircle()
        {
            var mesh = new Mesh();

            var vertices = new Vector3[64];
            var triangles = new List<int>();

            float anglePerPoint = Constants.TAU / 32.0f;
            float currentAngle = 0.0f;

            for (int i = 0; i < 32; i++)
            {
                float sin = Mathf.Sin(currentAngle);
                float cos = Mathf.Cos(currentAngle);

                Vector3 circleVec = new Vector3(sin, 0.0f, cos);

                vertices[i] = circleVec * (this.radius + 0.25f);
                vertices[i + 32] = circleVec * (this.radius + 0.27f);

                currentAngle += anglePerPoint;

                triangles.Add(i);
                triangles.Add(i + 32);
                triangles.Add(32 + ((i + 1) % 32));

                triangles.Add(32 + ((i + 1) % 32));
                triangles.Add((i + 1) % 32);
                triangles.Add(i);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles.ToArray(), 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        private void CreateBoundingCircle()
        {
            var mesh = this.CreateCircle();

            var meshRenderer = this.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = this.boundingCircleMaterial;

            var meshFilter = this.gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
        }

        private void Awake()
        {

            this.pointsAccessArray = new TransformAccessArray(1);
            this.segmentsAccessArray = new TransformAccessArray(1);
            this.segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            this.intersections = new NativeList<float2>(Allocator.Persistent);
            this.segmentsCopy = new NativeList<LineSegment2D>(Allocator.Persistent);

            this.SetSegments(this.startRadialLines, this.startLineDivisions);
            this.CreateBoundingCircle();
        }

        private void CreateSegment()
        {
            var segmentInstance = GameObject.Instantiate(this.segmentGO);
            segmentInstance.transform.parent = this.transform;
            this.segmentsAccessArray.Add(segmentInstance.transform);

            var point0Instance = GameObject.Instantiate(this.pointGO);
            point0Instance.transform.parent = this.transform;
            this.pointsAccessArray.Add(point0Instance.transform);

            var point1Instance = GameObject.Instantiate(this.pointGO);
            point1Instance.transform.parent = this.transform;
            this.pointsAccessArray.Add(point1Instance.transform);
        }

        public void SetSegments(int radialLines, int lineDivisions)
        {
            this.updateMovementHandle.Complete();

            this.segments.Clear();

            float angle = 0.0f;
            float anglePerLine = Constants.TAU / (float)radialLines;
            float halfAngle = anglePerLine * 0.5f;

            float currentR = this.radialStart;

            for (int i = 0; i < radialLines; i++)
            {

                Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currentR;
                Vector2 b = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * this.radialEnd;

                var radialSegment = new LineSegment2D() { a = a, b = b };

                this.segments.Add(radialSegment);
                this.CreateSegment();


                float rPerDivision = (this.radialEnd - currentR) / lineDivisions;
                float currentDivisionR = rPerDivision * 0.5f;

                for (int j = 0; j < lineDivisions; j++)
                {
                    a = new Vector2(Mathf.Cos(angle + halfAngle * 0.5f), Mathf.Sin(angle + halfAngle * 0.5f)) * (currentR + currentDivisionR);
                    b = new Vector2(Mathf.Cos(angle - halfAngle * 0.5f), Mathf.Sin(angle - halfAngle * 0.5f)) * (currentR + currentDivisionR);

                    var dividingSegment = new LineSegment2D() { a = a, b = b };

                    this.segments.Add(dividingSegment);
                    this.CreateSegment();

                    currentR += rPerDivision;
                }

                angle += anglePerLine;
                currentR = this.spiralA * Mathf.Exp(angle * Mathf.Atan(this.spiralB));
            }
        }

        public void AddSegments(int nrOfSegments)
        {
            this.updateMovementHandle.Complete();

            for (int i = 0; i < nrOfSegments; i++)
            {

                float angle0 = UnityEngine.Random.value * Constants.TAU;
                float angle1 = UnityEngine.Random.value * Constants.TAU;

                float r0 = Mathf.Pow(UnityEngine.Random.value, 1.0f) * this.radius;
                float r1 = Mathf.Pow(UnityEngine.Random.value, 1.0f) * this.radius;

                float2 pos2D0 = new float2(Mathf.Cos(angle0) * r0, Mathf.Sin(angle0) * r0);
                float2 pos2D1 = new float2(Mathf.Cos(angle1) * r1, Mathf.Sin(angle1) * r1);

                var segment = new LineSegment2D()
                {
                    a = pos2D0,
                    b = pos2D1
                };

                this.segments.Add(segment);



            }
        }


        void Update()
        {
            this.updateMovementHandle.Complete();


            this.segmentsCopy.CopyFrom(this.segments);


            var updateSegmentsJob = new UpdateSegmentsJob()
            {
                deltaTime = Time.deltaTime,
                segments = this.segments,
                radius = this.radius,
                angularVelocity = this.angularVelocity,
            };

            this.updateMovementHandle = IJobParallelForTransformExtensions.Schedule(updateSegmentsJob, this.segmentsAccessArray);


            var updateSegmentPointsJob = new UpdateSegmentPointsJob()
            {
                segments = this.segments,
            };

            this.updateMovementHandle = IJobParallelForTransformExtensions.Schedule(updateSegmentPointsJob, this.pointsAccessArray, this.updateMovementHandle);

            lineIntersectionMarker.Begin();

            JobHandle intersectionJob = default;
            switch (this.intersectionMethod)
            {
                case IntersectionMethod.COMBINATORICAL:
                    {
                        intersectionJob = LineIntersection.FindLineSegmentIntersectionsCombinatorial(this.segmentsCopy, ref this.intersections);
                    }
                    break;
                case IntersectionMethod.COMBINATORICAL_PARALLEL:
                    {

                        intersectionJob = LineIntersection.FindLineSegmentIntersectionsCombinatorialParallel(this.segmentsCopy, ref this.intersections);
                    }
                    break;
                case IntersectionMethod.SWEEP:
                    {
                        intersectionJob = LineIntersection.FindLineSegmentIntersectionsSweep(this.segmentsCopy, ref this.intersections);
                    }
                    break;
            }

            intersectionJob.Complete();

            lineIntersectionMarker.End();

            if (this.timer < 0.1f)
            {
                this.timer += Time.deltaTime;
            }
            else
            {
                //There are too many intersections at some point... we have to limit them somehow...
                float chance = 1.0f / (Mathf.Pow(this.intersections.Length, 0.5f) + 1.0f);
                for (int i = 0; i < this.intersections.Length; i++)
                {
                    if (UnityEngine.Random.value < chance)
                    {
                        var emitParams = new EmitParams()
                        {
                            position = new Vector3(this.intersections[i].x, 0.0f, this.intersections[i].y)
                        };
                        this.intersectionParticles.Emit(emitParams, 1);
                    }
                }
                this.timer = 0.0f;
            }

            if (this.lineIntersectionSampler == null || !this.lineIntersectionSampler.isValid)
            {
                this.lineIntersectionSampler = Sampler.Get("LineIntersection");
            }
        }



        private void Dispose()
        {

            if (this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }

            if (this.segments.IsCreated)
            {
                this.segments.Dispose();
            }

            if (this.segmentsAccessArray.isCreated)
            {
                this.segmentsAccessArray.Dispose();
            }

            if (this.intersections.IsCreated)
            {
                this.intersections.Dispose();
            }

            if (this.segmentsCopy.IsCreated)
            {
                this.segmentsCopy.Dispose();
            }
        }
        private void OnDestroy()
        {
            this.updateMovementHandle.Complete();

            this.Dispose();
        }
    }
}