
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;


namespace GimmeDOTSGeometry.Samples
{
    public class KochSnowflakeWithHoleSystem : MonoBehaviour
    {
        #region Public Variables

        public enum TriangulationMethod
        {
            EAR_CLIPPING = 0,
            Y_MONOTONE_SWEEPLINE = 1,
        }

        public float startT0 = 1.0f / 3.0f;
        public float startT1 = 1.0f / 2.0f;
        public float startT2 = 2.0f / 3.0f;
        public float startHoleT0 = 1.0f / 3.0f;
        public float startHoleT1 = 1.0f / 2.0f;
        public float startHoleT2 = 2.0f / 3.0f;

        public float startTriangleAngle = 60.0f;
        public float startHoleTriangleAngle = 60.0f;

        public int startSubdivisions = 0;
        public int holeStartSubdivisions = 0;

        public int startVertices = 3;
        public int holeStartVertices = 3;

        public Material polygonMaterial;

        public TriangulationMethod method;


        #endregion

        #region Private Variables

        private int subdivisions = 0;
        private int baseVertices = 3;
        private int triangles;

        private int holeSubdivisions = 0;
        private int holeBaseVertices = 3;

        private float t0, t1, t2;
        private float angle;

        private float holeT0, holeT1, holeT2;
        private float holeAngle;

        private NativeList<int> monotonePolyPointMapping;
        private NativeList<int> monotonePolySeparators;

        private MeshFilter polygonFilter;
        private MeshRenderer polygonRenderer;

        private Sampler triangulationSampler = null;

        #endregion

        private static readonly ProfilerMarker triangulationMarker = new ProfilerMarker("Triangulation");

        public Sampler GetTriangulationSampler() => this.triangulationSampler;

        public float T0 { get => this.t0; set => this.t0 = value; }
        public float T1 { get => this.t1; set => this.t1 = value; }
        public float T2 { get => this.t2; set => this.t2 = value; }

        public float HoleT0 { get => this.holeT0; set => this.holeT0 = value; }
        public float HoleT1 { get => this.holeT1; set => this.holeT1 = value; }
        public float HoleT2 { get => this.holeT2; set => this.holeT2 = value; }



        public int BaseVertices { get => this.baseVertices; set => this.baseVertices = value; }

        public int HoleBaseVertices { get => this.holeBaseVertices; set => this.holeBaseVertices = value; }

        public int Triangles => this.triangles;

        public float Angle { get => this.angle; set => this.angle = value; }
        public float HoleAngle { get => this.holeAngle; set => this.holeAngle = value; }

        public int Subdivisions { get => this.subdivisions; set => this.subdivisions = value; }

        public int HoleSubdivisions { get => this.holeSubdivisions; set => this.holeSubdivisions = value; }

        private void Awake()
        {
            this.t0 = this.startT0;
            this.t1 = this.startT1;
            this.t2 = this.startT2;

            this.angle = this.startTriangleAngle;
            this.baseVertices = this.startVertices;

            this.subdivisions = this.startSubdivisions;


            this.holeT0 = this.startHoleT0;
            this.holeT1 = this.startHoleT1;
            this.holeT2 = this.startHoleT2;

            this.holeAngle = this.startHoleTriangleAngle;
            this.holeBaseVertices = this.holeStartVertices;

            this.holeSubdivisions = this.holeStartSubdivisions;

            var polygonGO = new GameObject("Polygon");
            polygonGO.transform.parent = this.transform;

            this.polygonRenderer = polygonGO.AddComponent<MeshRenderer>();
            this.polygonFilter = polygonGO.AddComponent<MeshFilter>();

            this.polygonRenderer.sharedMaterial = this.polygonMaterial;

            this.monotonePolyPointMapping = new NativeList<int>(1, Allocator.Persistent);
            this.monotonePolySeparators = new NativeList<int>(1, Allocator.Persistent);
        }



        [BurstCompile]
        private struct SubdivideKochSnowflake : IJob
        {
            public bool outwards;

            public float t0, t1, t2;
            public float inverseCos;
            public float sin;

            [ReadOnly, NoAlias]
            public UnsafeList<float2> polyPoints;

            [WriteOnly, NoAlias]
            public NativeList<float2> subdividedPoints;

            public void Execute()
            {
                for (int i = 0; i < this.polyPoints.Length; i++)
                {
                    int nextIdx = (i + 1) % this.polyPoints.Length;

                    var start = this.polyPoints[i];
                    var end = this.polyPoints[nextIdx];

                    var dir = end - start;
                    var perp = this.outwards ? new float2(dir.y, -dir.x) : new float2(-dir.y, dir.x);
                    float length = math.length(dir);
                    float height = this.sin * (length * (this.t1 - this.t0)) * this.inverseCos;

                    var point0 = math.mad(dir, this.t0, start);
                    var point2 = math.mad(dir, this.t2, start);

                    var point1 = math.mad(dir, this.t1, start);
                    point1 += height * math.normalize(perp);

                    this.subdividedPoints.Add(start);
                    this.subdividedPoints.Add(point0);
                    this.subdividedPoints.Add(point1);
                    this.subdividedPoints.Add(point2);

                }

            }
        }

        private void OnDestroy()
        {
            if (this.monotonePolySeparators.IsCreated)
            {
                this.monotonePolySeparators.Dispose();
            }
            if (this.monotonePolyPointMapping.IsCreated)
            {
                this.monotonePolyPointMapping.Dispose();
            }
        }

        void Update()
        {

            var polygon = Polygon2DGeneration.Regular(Allocator.TempJob, Vector2.zero, 10.0f, this.baseVertices);
            for (int i = 0; i < this.subdivisions; i++)
            {
                var newPoints = new NativeList<float2>(Allocator.TempJob);
                var subdivisionJob = new SubdivideKochSnowflake()
                {
                    inverseCos = 1.0f / Mathf.Cos(this.angle * Mathf.Deg2Rad),
                    sin = Mathf.Sin(this.angle * Mathf.Deg2Rad),
                    polyPoints = polygon.points,
                    subdividedPoints = newPoints,
                    t0 = this.t0,
                    t1 = this.t1,
                    t2 = this.t2,
                    outwards = (i % 2) == 1
                };

                subdivisionJob.Schedule().Complete();

                polygon.Dispose();

                polygon = new NativePolygon2D(Allocator.TempJob, newPoints);

                newPoints.Dispose();
            }

            var holePolygon = Polygon2DGeneration.Regular(Allocator.TempJob, Vector2.zero, 6.0f, this.holeBaseVertices);
            for (int i = 0; i < this.holeSubdivisions; i++)
            {
                var newPoints = new NativeList<float2>(Allocator.TempJob);
                var subdivisionJob = new SubdivideKochSnowflake()
                {
                    inverseCos = 1.0f / Mathf.Cos(this.holeAngle * Mathf.Deg2Rad),
                    sin = Mathf.Sin(this.holeAngle * Mathf.Deg2Rad),
                    polyPoints = holePolygon.points,
                    subdividedPoints = newPoints,
                    t0 = this.holeT0,
                    t1 = this.holeT1,
                    t2 = this.holeT2,
                    outwards = (i % 2) == 1
                };

                subdivisionJob.Schedule().Complete();

                holePolygon.Dispose();

                holePolygon = new NativePolygon2D(Allocator.TempJob, newPoints);

                newPoints.Dispose();
            }

            int separator = polygon.points.Length;

            polygon.points.AddRange(holePolygon.points);
            polygon.separators.Add(separator);

            NativeList<int> triangles = new NativeList<int>(Allocator.TempJob);

            triangulationMarker.Begin();

            if (this.method == TriangulationMethod.EAR_CLIPPING)
            {
                var simplePolygon = NativePolygon2D.MakeSimple(Allocator.TempJob, polygon);

                polygon.Dispose();

                polygon = simplePolygon;

                var triangulationJob = new Polygon2DTriangulationJobs.EarClippingTriangulationJob()
                {
                    clockwiseWinding = true,
                    polyPoints = polygon.points,
                    triangles = triangles,
                };
                triangulationJob.Schedule().Complete();
            }
            else
            {
                this.monotonePolyPointMapping.Clear();
                this.monotonePolySeparators.Clear();
                var yMonotoneJob = new Polygon2DTriangulationJobs.MonotoneDecompositionJob()
                {
                    epsilon = 10e-5f,
                    polyPoints = polygon.points,
                    polySeparators = polygon.separators,
                    monotonePolyPointMapping = this.monotonePolyPointMapping,
                    monotonePolySeparators = this.monotonePolySeparators,
                };
                yMonotoneJob.Schedule().Complete();

                var triangulationJob = new Polygon2DTriangulationJobs.YMonotoneTriangulationJob()
                {
                    triangles = triangles,
                    monotonePolyPointMapping = this.monotonePolyPointMapping,
                    monotonePolySeparators = this.monotonePolySeparators,
                    polyPoints = polygon.points,
                    clockwiseWinding = true
                };
                triangulationJob.Schedule().Complete();
            }

            triangulationMarker.End();

            if ((triangles.Length % 3) == 0)
            {
                this.triangles = triangles.Length;

                var mesh = MeshUtil.CreatePolygonMesh(polygon, triangles.AsArray());

                var bounds = polygon.GetBoundingRect();

                var vertices = mesh.vertices;
                Vector2[] uvs = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 uv = new Vector2();
                    var point = vertices[i];

                    uv.x = Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x);
                    uv.y = Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.z);

                    uvs[i] = uv;
                }
                mesh.SetUVs(0, uvs);

                this.polygonFilter.sharedMesh = mesh;
            }
            else
            {
                Debug.Log("Can't triangulate with these snowflake parameters (because of self-intersections)");
            }


            triangles.Dispose();
            polygon.Dispose();
            holePolygon.Dispose();


            if (this.triangulationSampler == null || !this.triangulationSampler.isValid)
            {
                this.triangulationSampler = Sampler.Get("Triangulation");
            }
        }
    }
}