using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class DBSCAN2DSystem : MonoBehaviour
    {

        #region Public Fields

        public float yOffset = 0.01f;
        public float pointSize = 0.1f;
        public float perlinScale = 0.01f;
        public float gradientStrength = 1.0f;
        public float dampening = 0.1f;

        public GameObject point;

        public int initialPoints;
        public int minClusterPoints = 7;
        public int gradientResolution = 128;

        public Material boundsMaterial;
        public Material colorMaterial;

        public Vector2 maxVelocity;

        public Rect bounds;

        #endregion

        #region Private Fields

        private float epsilon = 0.5f;

        private int positionCount;

        private List<MeshRenderer> pointRenderers = new List<MeshRenderer>();
        
        private Dictionary<int, MaterialPropertyBlock> clusterColors = new();

        private NativeArray<float> noiseField;
        private NativeArray<float2> gradientField;

        private NativeList<int> clusteringResult;

        private NativeList<float2> positions;
        private NativeList<float2> velocities;

        NativeList<UnsafeList<int>> neighbors;

        private Sampler dbscanSampler = null;

        private TransformAccessArray pointsAccessArray;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";
        private static readonly ProfilerMarker dbscanMarker = new ProfilerMarker("DBSCAN");

        public float Epsilon
        {
            get => this.epsilon;
            set => this.epsilon = value;
        }

        public Sampler GetDBSCANSampler() => this.dbscanSampler;

        public int GetNrOfPoints() => this.positionCount;

        private void FillGradientGrid()
        {

            for(int x = 0; x < this.gradientResolution; x++)
            {
                for(int y = 0; y < this.gradientResolution; y++)
                {
                    int idx = y * this.gradientResolution + x;
                    this.noiseField[idx] = Mathf.PerlinNoise(x * this.perlinScale, y * this.perlinScale);
                }
            }

            for(int x = 0; x < this.gradientResolution; x++)
            {
                for(int y = 0; y < this.gradientResolution; y++)
                {
                    int xMinus = Mathf.Clamp(x - 1, 0, this.gradientResolution - 1);
                    int yMinus = Mathf.Clamp(y - 1, 0, this.gradientResolution - 1);
                    int xPlus = Mathf.Clamp(x + 1, 0, this.gradientResolution - 1);
                    int yPlus = Mathf.Clamp(y + 1, 0, this.gradientResolution - 1);

                    int xMinIdx = y * this.gradientResolution + xMinus;
                    int yMinIdx = yMinus * this.gradientResolution + x;
                    int xPlusIdx = y * this.gradientResolution + xPlus;
                    int yPlusIdx = yPlus * this.gradientResolution + x;

                    float xMinusVal = this.noiseField[xMinIdx];
                    float xPlusVal = this.noiseField[xPlusIdx];
                    float yMinusVal = this.noiseField[yMinIdx];
                    float yPlusVal = this.noiseField[yPlusIdx];

                    float xDir = xPlusVal - xMinusVal;
                    float yDir = yPlusVal - yMinusVal;

                    int idx = y * this.gradientResolution + x;
                    this.gradientField[idx] = new float2(xDir, yDir);
                }
            }
        }

        private void Start()
        {
            this.pointsAccessArray = new TransformAccessArray(this.initialPoints);
            this.positions = new NativeList<float2>(this.initialPoints, Allocator.Persistent);
            this.velocities = new NativeList<float2>(this.initialPoints, Allocator.Persistent);
            this.clusteringResult = new NativeList<int>(this.initialPoints, Allocator.Persistent);
            this.noiseField = new NativeArray<float>(this.gradientResolution * this.gradientResolution, Allocator.Persistent);
            this.gradientField = new NativeArray<float2>(this.gradientResolution * this.gradientResolution, Allocator.Persistent);
            this.neighbors = new NativeList<UnsafeList<int>>(this.initialPoints, Allocator.Persistent);

            this.FillGradientGrid();

            var boundsGo = new GameObject("Bounds");
            var mr = boundsGo.AddComponent<MeshRenderer>();
            var mf = boundsGo.AddComponent<MeshFilter>();

            mr.material = this.boundsMaterial;
            mf.mesh = MeshUtil.CreateRectangleOutline(this.bounds, 0.1f);

            var boundsGo2 = new GameObject("Bounds 2");
            mr = boundsGo2.AddComponent<MeshRenderer>();
            mf = boundsGo2.AddComponent<MeshFilter>();

            var outerRect = this.bounds;
            outerRect.Expand(0.5f);

            mr.material = this.boundsMaterial;
            mf.mesh = MeshUtil.CreateRectangleOutline(outerRect, 0.1f);

            this.AddPoints(this.initialPoints);
        }

        public void AddPoints(int nrOfPoints)
        {
            this.positionCount += nrOfPoints;
            float pointHalf = this.pointSize * 0.5f;
            for(int i = 0; i < nrOfPoints; i++)
            {
                float rndX = UnityEngine.Random.Range(this.bounds.xMin + pointHalf, this.bounds.xMax - pointHalf);
                float rndY = UnityEngine.Random.Range(this.bounds.yMin + pointHalf, this.bounds.yMax - pointHalf);

                var worldPos = new Vector3(rndX, this.yOffset, rndY);

                var point = GameObject.Instantiate(this.point);
                point.transform.parent = this.transform;
                point.transform.position = worldPos;
                point.transform.localScale = this.pointSize * Vector3.one;

                var meshRenderer = point.GetComponentInChildren<MeshRenderer>();
                this.pointRenderers.Add(meshRenderer);

                var pos2D = new Vector2(rndX, rndY);
                this.positions.Add(pos2D);
                this.clusteringResult.Add(DBSCAN.NOISE_LABEL);
                this.neighbors.Add(new UnsafeList<int>(32, Allocator.Persistent));

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);

                this.velocities.Add(new float2(velX, velY));

                this.pointsAccessArray.Add(point.transform);
                
            }


        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {
            public Rect bounds;

            public float deltaTime;
            public float pointSize;
            public float gradientStrength;
            public float dampening;

            public int gradientResolution;

            [NoAlias]
            public NativeArray<float2> positions;

            [NoAlias]
            public NativeArray<float2> velocities;

            [NoAlias, ReadOnly, NativeDisableParallelForRestriction]
            public NativeArray<float2> gradientField;

            public void Execute(int index, TransformAccess transform)
            {
                var pos = this.positions[index];

                var velocity = this.velocities[index];

                float nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                float nextPosY = math.mad(velocity.y, this.deltaTime, pos.y);

                float xMax = this.bounds.xMax - this.pointSize;
                float xMin = this.bounds.xMin + this.pointSize;
                float yMax = this.bounds.yMax - this.pointSize;
                float yMin = this.bounds.yMin + this.pointSize;

                float2 percent = (pos - (float2)this.bounds.min) / (float2)this.bounds.size;
                int2 coords = (int2)(percent * this.gradientResolution);
                int idx = math.mad(coords.y, this.gradientResolution, coords.x);
                idx = math.clamp(idx, 0, this.gradientResolution * this.gradientResolution);
                float2 gradient = this.gradientField[idx] * this.gradientStrength;

                velocity += gradient * this.deltaTime;

                if (Hint.Unlikely(nextPosX > xMax))
                {
                    velocity = math.reflect(velocity, new float2(-1.0f, 0.0f));
                    nextPosX -= (nextPosX - xMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosX < xMin))
                {
                    velocity = math.reflect(velocity, new float2(1.0f, 0.0f));
                    nextPosX += (xMin - nextPosX) * 2.0f;
                }

                if (Hint.Unlikely(nextPosY > yMax))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, -1.0f));
                    nextPosY -= (nextPosY - yMax) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosY < yMin))
                {
                    velocity = math.reflect(velocity, new float2(0.0f, 1.0f));
                    nextPosY += (yMin - nextPosY) * 2.0f;
                }

                velocity = velocity * (1.0f - this.dampening * this.deltaTime);

                this.velocities[index] = velocity;
                this.positions[index] = new float2(nextPosX, nextPosY);

                transform.position = new Vector3(nextPosX, transform.position.y, nextPosY);
            }
        }

        private void UpdateColor()
        {

            for(int i = 0; i < this.clusteringResult.Length; i++)
            {

                int cluster = this.clusteringResult[i];

                MaterialPropertyBlock mpb;
                if(this.clusterColors.ContainsKey(cluster))
                {
                    mpb = this.clusterColors[cluster];
                }
                else
                {
                    mpb = new MaterialPropertyBlock();
                    Color rndColor = new Color(UnityEngine.Random.Range(0.1f, 1.0f),
                        UnityEngine.Random.Range(0.1f, 1.0f),
                        UnityEngine.Random.Range(0.5f, 1.0f));

                    mpb.SetColor(SHADER_COLOR, rndColor);

                    this.clusterColors.Add(cluster, mpb);
                }

                this.pointRenderers[i].SetPropertyBlock(mpb);
            }
        }

        public void NegateGradients()
        {
            this.gradientStrength = -this.gradientStrength;
        }

        private void Update()
        {
            var updatePointsJob = new UpdatePointsJob()
            {
                bounds = this.bounds,
                deltaTime = Time.deltaTime,
                pointSize = this.pointSize,
                positions = this.positions.AsArray(),
                velocities = this.velocities.AsArray(),
                gradientField = this.gradientField,
                gradientResolution = this.gradientResolution,
                gradientStrength = this.gradientStrength,
                dampening = this.dampening,
            };

            updatePointsJob.Schedule(this.pointsAccessArray).Complete();

            dbscanMarker.Begin();

            var dbscanJob = DBSCAN.DBSCAN2D(this.positions, this.minClusterPoints, this.epsilon, ref this.neighbors, ref this.clusteringResult);
            dbscanJob.Complete();

            dbscanMarker.End();

            this.UpdateColor();

            if(this.dbscanSampler == null || !this.dbscanSampler.isValid)
            {
                this.dbscanSampler = Sampler.Get("DBSCAN");
            }
        }

        private void Dispose()
        {
            this.velocities.DisposeIfCreated();
            this.positions.DisposeIfCreated();
            this.clusteringResult.DisposeIfCreated();
            this.noiseField.DisposeIfCreated();
            this.gradientField.DisposeIfCreated();

            if(this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }

            if (this.neighbors.IsCreated)
            {
                for (int i = 0; i < this.neighbors.Length; i++)
                {
                    var list = this.neighbors[i];
                    list.Dispose();
                }
                this.neighbors.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
