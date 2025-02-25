using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class DelaunaySystem : MonoBehaviour
    {
        #region Public Fields

        public float ringRadius = 0.1f;
        public float ringThickness = 0.02f;
        public float rectThickness = 0.1f;
        public float lineThickness = 0.1f;

        public Gradient triangleGradient = new Gradient();

        public int rings = 3;

        public Material boundsMaterial = null;
        public Material ringMaterial = null;
        public Material triangleMaterial = null;

        public Rect bounds;

        public Vector2 angularVelocityRange;

        #endregion

        #region Private Fields

        private List<GameObject> points = new List<GameObject>();

        private List<GameObject> triangles = new List<GameObject>();
        private List<GameObject> triangleOutlines = new List<GameObject>();
        private List<MeshFilter> triangleMFs = new List<MeshFilter>();
        private List<MeshFilter> triangleOutlineMFs = new List<MeshFilter>();

        private MaterialPropertyBlock colorBlock;

        private NativeList<float3> ringPositions;
        private NativeList<float2> positions;
        private NativeList<float> radii;
        private NativeList<float> currentAngles;
        private NativeList<float> angularVelocities;
        private NativeList<int3> triangulation;

        private Sampler delaunaySampler = null;

        private TransformAccessArray pointsAccessArray;

        private Unity.Mathematics.Random rnd;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker delaunayMarker = new ProfilerMarker("Delaunay");

        public Sampler GetDelaunaySampler() => this.delaunaySampler;

        public int GetNrOfPoints() => this.positions.Length;



        public void Start()
        {
            var rectMesh = MeshUtil.CreateRectangleOutline(this.bounds, this.rectThickness);

            var rectangleGO = new GameObject("Rectangle");
            rectangleGO.transform.parent = this.transform;
            rectangleGO.transform.position = new Vector3(0.0f, -0.01f, 0.0f);

            var rectMeshFilter = rectangleGO.AddComponent<MeshFilter>();
            rectMeshFilter.mesh = rectMesh;

            var rectMeshRenderer = rectangleGO.AddComponent<MeshRenderer>();
            rectMeshRenderer.material = this.boundsMaterial;

            this.colorBlock = new MaterialPropertyBlock();

            this.rnd = new Unity.Mathematics.Random();
            this.rnd.InitState();

            this.triangulation = new NativeList<int3>(Allocator.Persistent);

            this.CreateRingPoints();
        }

        public void CreateRingPoints()
        {
            this.Dispose();

            int points = (this.rings * (this.rings + 1)) / 2;

            this.ringPositions = new NativeList<float3>(points, Allocator.Persistent);
            this.positions = new NativeList<float2>(points, Allocator.Persistent);
            this.angularVelocities = new NativeList<float>(points, Allocator.Persistent);
            this.radii = new NativeList<float>(points, Allocator.Persistent);
            this.pointsAccessArray = new TransformAccessArray(points);
            this.currentAngles = new NativeList<float>(points, Allocator.Persistent);
            this.triangulation = new NativeList<int3>(Allocator.Persistent);

            for (int i = 0; i < this.points.Count; i++)
            {
                GameObject.Destroy(this.points[i]);
            }
            this.points.Clear();

            var ringMesh = MeshUtil.CreateRing(this.ringRadius, this.ringThickness);

            float width = this.bounds.width;
            float height = this.bounds.height;

            float centerX = this.bounds.xMin + width / 2.0f;
            float centerY = this.bounds.yMin + height / 2.0f;

            float currentRadius = 0.0f;
            float radiusIncrease = (this.bounds.width / (this.rings + 1)) * 0.5f;
            int counter = 0;
            for(int i = 1; i < this.rings + 1; i++)
            {
                float anglePerPoint = Constants.TAU / (float)i;
                float currentAngle = UnityEngine.Random.Range(0.0f, anglePerPoint);

                for(int j = 0; j < i; j++)
                {
                    Vector2 pos = new Vector2(centerX, centerY);

                    pos += new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle)) * currentRadius;
                    this.ringPositions.Add(new float3(pos.x, 0.0f, pos.y));
                        
                    this.angularVelocities.Add(this.rnd.NextFloat(this.angularVelocityRange.x, this.angularVelocityRange.y));
                    if (this.rnd.NextFloat() < 0.5f) this.angularVelocities[this.angularVelocities.Length - 1] *= -1;

                    this.radii.Add(this.rnd.NextFloat(0.0f, radiusIncrease * 0.4f));
                    this.currentAngles.Add(this.rnd.NextFloat(0.0f, Constants.TAU));
                    this.positions.Add(pos);

                    var pointGO = new GameObject($"Point_{counter}");
                    pointGO.transform.parent = this.transform;
                    pointGO.transform.position = (Vector3)pos + Vector3.up * 0.02f;

                    var meshRenderer = pointGO.AddComponent<MeshRenderer>();
                    meshRenderer.material = this.ringMaterial;

                    var meshFilter = pointGO.AddComponent<MeshFilter>();
                    meshFilter.mesh = ringMesh;

                    this.pointsAccessArray.Add(pointGO.transform);
                    this.points.Add(pointGO);

                    currentAngle += anglePerPoint;
                    counter++;
                }
                currentRadius += radiusIncrease;
            }
        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {
            [NoAlias, ReadOnly]
            public NativeList<float3> ringPositions;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float2> positions;

            [NoAlias, ReadOnly]
            public NativeList<float> radii;

            [NoAlias, ReadOnly]
            public NativeList<float> angularVelocities;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float> currentAngles;

            public float deltaTime;

            public void Execute(int index, TransformAccess transform)
            {
                this.currentAngles[index] += this.angularVelocities[index] * this.deltaTime;

                float3 ringPos = this.ringPositions[index];
                float sin = math.sin(this.currentAngles[index]);
                float cos = math.cos(this.currentAngles[index]);

                ringPos.x += cos * this.radii[index];
                ringPos.z += sin * this.radii[index];

                this.positions[index] = ringPos.xz;

                transform.position = ringPos;
            }
        }

        private void CreateTriangles()
        {


            for (int i = 0; i < this.triangulation.Length; i++)
            {
                var triangleIndices = this.triangulation[i];

                float3 a = this.positions[triangleIndices.x].AsFloat3(CardinalPlane.XZ);
                float3 b = this.positions[triangleIndices.y].AsFloat3(CardinalPlane.XZ);
                float3 c = this.positions[triangleIndices.z].AsFloat3(CardinalPlane.XZ);

                var triangle = new NativeTriangle3D(c, b, a);

                var triangleMesh = MeshUtil.CreateTriangle(triangle);
                var triangleOutlineMesh = MeshUtil.CreateTriangleOutline(triangle, this.lineThickness);

                if (i < this.triangles.Count)
                {
                    this.triangles[i].SetActive(true);
                    this.triangleOutlines[i].SetActive(true);

                    var mf = this.triangleMFs[i];
                    Destroy(mf.sharedMesh);
                    mf.mesh = triangleMesh;

                    mf = this.triangleOutlineMFs[i];
                    Destroy(mf.sharedMesh);
                    mf.mesh = triangleOutlineMesh;
                }
                else
                {
                    var triangleGo = new GameObject($"Triangle_{i}");
                    triangleGo.transform.parent = this.transform;

                    var mf = triangleGo.AddComponent<MeshFilter>();
                    mf.mesh = triangleMesh;

                    var mr = triangleGo.AddComponent<MeshRenderer>();
                    var rndColor = this.triangleGradient.Evaluate(UnityEngine.Random.value);
                    this.colorBlock.SetColor(SHADER_COLOR, rndColor);

                    mr.material = this.triangleMaterial;
                    mr.SetPropertyBlock(this.colorBlock);

                    this.triangles.Add(triangleGo);
                    this.triangleMFs.Add(mf);

                    var triangleOutlineGo = new GameObject($"Triangle_Outline_{i}");
                    triangleOutlineGo.transform.parent = this.transform;
                    triangleOutlineGo.transform.position = Vector3.up * 0.01f;

                    mf = triangleOutlineGo.AddComponent<MeshFilter>();
                    mf.mesh = triangleOutlineMesh;

                    mr = triangleOutlineGo.AddComponent<MeshRenderer>();
                    mr.material = this.ringMaterial;

                    this.triangleOutlines.Add(triangleOutlineGo);
                    this.triangleOutlineMFs.Add(mf);
                }
            }

            for (int i = this.triangulation.Length; i < this.triangles.Count; i++)
            {
                this.triangles[i].SetActive(false);
                this.triangleOutlines[i].SetActive(false);
            }
        }

        private void Update()
        {

            var updatePointsJob = new UpdatePointsJob()
            {
                angularVelocities = this.angularVelocities,
                ringPositions = this.ringPositions,
                currentAngles = this.currentAngles,
                positions = this.positions,
                radii = this.radii,
                deltaTime = Time.deltaTime,
            };
            IJobParallelForTransformExtensions.Schedule(updatePointsJob, this.pointsAccessArray).Complete();

            delaunayMarker.Begin();

            var delaunayJob = Delaunay2D.CalculateDelaunay(this.positions.AsArray(), ref this.triangulation, out var allocations, Allocator.TempJob);
            delaunayJob.Complete();

            delaunayMarker.End();

            allocations.Dispose();

            this.CreateTriangles();

            if (this.delaunaySampler == null || !this.delaunaySampler.isValid)
            {
                this.delaunaySampler = Sampler.Get("Delaunay");
            }
        }


        private void Dispose()
        {
            if (this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }
            this.angularVelocities.DisposeIfCreated();
            this.positions.DisposeIfCreated();
            this.ringPositions.DisposeIfCreated();
            this.radii.DisposeIfCreated();
            this.currentAngles.DisposeIfCreated();
            this.triangulation.DisposeIfCreated();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
