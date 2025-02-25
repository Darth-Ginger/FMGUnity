using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class QuadtreeMovementSystem : MonoBehaviour
    {

        #region Public Variables

        public Color insideColor;
        public Color outsideColor;

        public float yOffset = 0.01f;
        public float pointSize = 0.1f;
        public float initialSearchRadiusPercentage = 0.1f;
        public float initialMovingPercentage = 0.1f;

        public int initialNumberOfPoints;
        public int initialQuadtreeDepth = 3;

        public GameObject point;

        public Material rectMaterial;
        public Material ringMaterial;

        public Vector2 maxVelocity;

        public Rect boundingRect;

        #endregion

        #region Private Variables

        private bool useSparseQuadtree = true;
        private bool doMultiQuery = false;

        private float searchRadius = 0.0f;
        private float movingPercentage = 0.0f;
        private float currentSearchRadiusPercentage = 0.0f;

        private GameObject rectangleGO;
        private GameObject ringGO;
        private GameObject[] multiQueryRings;

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private int positionCount = 0;
        private int currentQuadtreeDepth = 0;

        private List<MeshRenderer> pointRenderers = new List<MeshRenderer>();

        private IQuadtree<int> quadtree;

        private NativeList<bool> isPointMoving;

        private NativeList<float3> oldPositions;
        private NativeList<float3> positions;
        private NativeList<float2> velocities;

        private NativeList<uint> queryCellResults;
        private NativeParallelHashSet<uint> multiQueryCellResults;

        private NativeArray<float2> multiQueryCenters;
        private NativeArray<float> multiQueryRadii;


        private Rect bounds;

        private Sampler radiusQuerySampler = null;
        private Sampler updateQuadtreeSampler = null;

        private TransformAccessArray pointsAccessArray;

        private Unity.Mathematics.Random rnd;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker updateQuadtreeMarker = new ProfilerMarker("UpdateQuadtree");
        private static readonly ProfilerMarker radiusQueryMarker = new ProfilerMarker("RadiusQuery");

        public bool IsDoingMultiQueries() => this.doMultiQuery;
        public bool IsUsingSparseQuadtree() => this.useSparseQuadtree;

        public Sampler GetRadiusQuerySampler() => this.radiusQuerySampler;
        public Sampler GetUpdateQuadtreeSampler() => this.updateQuadtreeSampler;

        public int GetNrOfPoints() => this.positionCount;


        public int CurrentSearchDepth
        {
            get => this.currentQuadtreeDepth;
            set {
                this.currentQuadtreeDepth = value;
                this.quadtree.Dispose();
                this.RecreateQuadtree();
            }
        }

        public float CurrentSearchPercentage
        {
            get => this.currentSearchRadiusPercentage;
            set
            {
                this.currentSearchRadiusPercentage = value;
                this.searchRadius = this.currentSearchRadiusPercentage * this.boundingRect.width * 0.5f;
                this.ScaleRings();
            }
        }

        public float CurrentMovingPercentage
        {
            get => this.movingPercentage;
            set => this.movingPercentage = value;
        }

        public void EnableMultiQuery(bool enable)
        {
            this.doMultiQuery = enable;

            var mr = this.ringGO.GetComponent<MeshRenderer>();
            mr.enabled = !this.doMultiQuery;

            for(int i = 0; i < this.multiQueryRings.Length; i++)
            {
                mr = this.multiQueryRings[i].GetComponent<MeshRenderer>();
                mr.enabled = this.doMultiQuery;
            }
        }

        public void AddPoints(int nrOfPoints)
        {
            this.positionCount += nrOfPoints;
            float pointHalf = this.pointSize / 2.0f;
            for(int i = 0; i < nrOfPoints; i++)
            {
                float rndX = UnityEngine.Random.Range(this.bounds.xMin + pointHalf, this.bounds.xMax - pointHalf);
                float rndY = UnityEngine.Random.Range(this.bounds.yMin + pointHalf, this.bounds.yMax - pointHalf);

                var worldPos = new Vector3(rndX, this.yOffset, rndY);

                var point = GameObject.Instantiate(this.point);
                point.transform.parent = this.transform;
                point.transform.position = worldPos;

                var meshRenderer = point.GetComponentInChildren<MeshRenderer>();

                var pos = new float3(rndX, this.yOffset, rndY);
                this.positions.Add(pos);

                float2 flatPos = new float2(pos.x, pos.z);
                float distFromCenter = math.distance(flatPos, float2.zero);

                if(distFromCenter < this.searchRadius)
                {
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                } else
                {
                    meshRenderer.SetPropertyBlock(this.outsideMPB);
                }

                this.pointRenderers.Add(meshRenderer);

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);

                this.velocities.Add(new float2(velX, velY));

                this.pointsAccessArray.Add(point.transform);
                this.isPointMoving.Add(false);
            }

            this.quadtree.Dispose();
            this.RecreateQuadtree();
        }

        private void RecreateQuadtree()
        {
            if (this.useSparseQuadtree)
            {
                this.quadtree = new NativeSparseQuadtree<int>(float3.zero,
                    new float2(this.bounds.width, this.bounds.height),
                    this.currentQuadtreeDepth,
                    this.positionCount,
                    Allocator.Persistent);

            }
            else
            {
                this.quadtree = new NativeDenseQuadtree<int>(float3.zero,
                    new float2(this.bounds.width, this.bounds.height),
                    this.currentQuadtreeDepth,
                    this.positionCount,
                    Allocator.Persistent);
            }

            this.InsertAllPointsIntoTree();
        }

        public void ExchangeQuadtreeModel()
        {
            this.quadtree.Dispose();
            this.useSparseQuadtree = !this.useSparseQuadtree;

            this.RecreateQuadtree();
        }

        private void InsertAllPointsIntoTree()
        {
            for(int i = 0; i < this.positions.Length; i++)
            {
                this.quadtree.Insert(this.positions[i], i);
            }
        }

        private void ScaleRings()
        {
            if (this.doMultiQuery)
            {
                var newRingMesh = MeshUtil.CreateRing(this.searchRadius * 0.5f, 0.2f);

                for(int i = 0; i < this.multiQueryRings.Length; i++)
                {
                    var meshFilter = this.multiQueryRings[i].GetComponent<MeshFilter>();
                    meshFilter.sharedMesh = newRingMesh;
                }
            }
            else
            {
                var newRingMesh = MeshUtil.CreateRing(this.searchRadius, 0.2f);

                var meshFilter = this.ringGO.GetComponent<MeshFilter>();
                Destroy(meshFilter.sharedMesh);
                meshFilter.sharedMesh = newRingMesh;
            }
        }

        private void InitMultiyQueries()
        {
            this.multiQueryRings = new GameObject[4];

            float halfSize = this.boundingRect.width * 0.5f;

            var ringMesh = MeshUtil.CreateRing(this.searchRadius * 0.5f, 0.2f);

            for (int i = 0; i < 4; i++)
            {
                this.multiQueryRings[i] = new GameObject($"Ring_{i}");
                this.multiQueryRings[i].transform.parent = this.transform;

                var ringMeshFilter = this.multiQueryRings[i].AddComponent<MeshFilter>();
                ringMeshFilter.mesh = ringMesh;

                var ringMeshRenderer = this.multiQueryRings[i].AddComponent<MeshRenderer>();
                ringMeshRenderer.material = this.ringMaterial;
                ringMeshRenderer.enabled = false;
            }

            this.multiQueryRings[0].transform.position = new Vector3(-halfSize * 0.5f, 2 * this.yOffset, -halfSize * 0.5f);
            this.multiQueryRings[1].transform.position = new Vector3(-halfSize * 0.5f, 2 * this.yOffset, halfSize * 0.5f);
            this.multiQueryRings[2].transform.position = new Vector3(halfSize * 0.5f, 2 * this.yOffset, -halfSize * 0.5f);
            this.multiQueryRings[3].transform.position = new Vector3(halfSize * 0.5f, 2 * this.yOffset, halfSize * 0.5f);

            this.multiQueryCellResults = new NativeParallelHashSet<uint>(this.initialNumberOfPoints, Allocator.Persistent);

            this.multiQueryCenters = new NativeArray<float2>(this.multiQueryRings.Length, Allocator.Persistent);
            this.multiQueryRadii = new NativeArray<float>(this.multiQueryRings.Length, Allocator.Persistent);
        }

        void Start()
        {
            this.currentQuadtreeDepth = this.initialQuadtreeDepth;
            this.currentSearchRadiusPercentage = this.initialSearchRadiusPercentage;
            this.bounds = this.boundingRect;

            this.searchRadius = this.initialSearchRadiusPercentage * this.boundingRect.width * 0.5f;


            var rectMesh = MeshUtil.CreateRectangle(this.boundingRect);

            this.rectangleGO = new GameObject("Rectangle");
            this.rectangleGO.transform.parent = this.transform;
            this.rectangleGO.transform.position = new Vector3(0.0f, -this.yOffset, 0.0f);

            var rectMeshFilter = this.rectangleGO.AddComponent<MeshFilter>();
            rectMeshFilter.mesh = rectMesh;

            var rectMeshRenderer = this.rectangleGO.AddComponent<MeshRenderer>();
            rectMeshRenderer.material = this.rectMaterial;

            var ringMesh = MeshUtil.CreateRing(this.searchRadius, 0.2f);

            this.ringGO = new GameObject("Ring");
            this.ringGO.transform.parent = this.transform;
            this.ringGO.transform.position = new Vector3(0.0f, 2 * this.yOffset, 0.0f);

            var ringMeshFilter = this.ringGO.AddComponent<MeshFilter>();
            ringMeshFilter.mesh = ringMesh;

            var ringMeshRenderer = this.ringGO.AddComponent<MeshRenderer>();
            ringMeshRenderer.material = this.ringMaterial;

            this.InitMultiyQueries();

            this.insideMPB = new MaterialPropertyBlock();
            this.outsideMPB = new MaterialPropertyBlock();

            this.insideMPB.SetColor(SHADER_COLOR, this.insideColor);
            this.outsideMPB.SetColor(SHADER_COLOR, this.outsideColor);

            this.pointsAccessArray = new TransformAccessArray(this.initialNumberOfPoints);
            this.positions = new NativeList<float3>(this.initialNumberOfPoints, Allocator.Persistent);
            this.oldPositions = new NativeList<float3>(this.initialNumberOfPoints, Allocator.Persistent);
            this.velocities = new NativeList<float2>(this.initialNumberOfPoints, Allocator.Persistent);
            this.queryCellResults = new NativeList<uint>(this.initialNumberOfPoints, Allocator.Persistent);
            this.isPointMoving = new NativeList<bool>(this.initialNumberOfPoints, Allocator.Persistent);

            this.quadtree = new NativeSparseQuadtree<int>(float3.zero, 
                new float2(this.boundingRect.width, this.boundingRect.height), 
                this.currentQuadtreeDepth,
                this.initialNumberOfPoints, Allocator.Persistent);


            this.movingPercentage = this.initialMovingPercentage;

            this.rnd = new Unity.Mathematics.Random();
            this.rnd.InitState();

            this.AddPoints(this.initialNumberOfPoints);

        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {
            public Rect bounds;

            public float deltaTime;
            public float pointSize;
            public float movingPercentage;
            public float yOffset;

            [NoAlias]
            public NativeArray<bool> isPointMoving;

            [NoAlias]
            public NativeArray<float3> positions;

            [NoAlias]
            public NativeArray<float2> velocities;

            public Unity.Mathematics.Random rnd;

            public void Execute(int index, TransformAccess transform)
            {
                var pos = this.positions[index];

                var velocity = this.velocities[index];

                //Only move a percentage, to test the performance differences between sparse and dense octrees
                if (this.rnd.NextFloat() < this.movingPercentage)
                {
                    float nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                    float nextPosY = math.mad(velocity.y, this.deltaTime, pos.z);

                    float xMax = this.bounds.xMax - this.pointSize;
                    float xMin = this.bounds.xMin + this.pointSize;
                    float yMax = this.bounds.yMax - this.pointSize;
                    float yMin = this.bounds.yMin + this.pointSize;


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

                    this.velocities[index] = velocity;
                    this.positions[index] = new float3(nextPosX, this.yOffset, nextPosY);

                    transform.position = this.positions[index];
                    this.isPointMoving[index] = true;
                }
                else
                {
                    this.isPointMoving[index] = false;
                }
            }
        }

        [BurstCompile]
        private struct UpdateDenseQuadtreeJob : IJob
        {
            public NativeDenseQuadtree<int> quadtree;

            [NoAlias]
            public NativeList<bool> updateList;

            [NoAlias]
            public NativeList<float3> newPositions;

            [NoAlias]
            public NativeList<float3> oldPositions;

            public void Execute()
            {
                for(int i = 0; i < this.newPositions.Length; i++)
                {
                    if (this.updateList[i])
                    {
                        this.quadtree.Update(i, this.oldPositions[i], this.newPositions[i]);
                    }
                }
            }
        }

        [BurstCompile]
        private struct UpdateSparseQuadtreeJob : IJob
        {
            public NativeSparseQuadtree<int> quadtree;

            [NoAlias]
            public NativeList<bool> updateList;

            [NoAlias]
            public NativeList<float3> newPositions;
            [NoAlias]
            public NativeList<float3> oldPositions;

            public void Execute()
            {
                for(int i = 0; i < this.newPositions.Length; i++)
                {
                    if (this.updateList[i])
                    {
                        this.quadtree.Update(i, this.oldPositions[i], this.newPositions[i]);
                    }
                }
            }
        }

        [BurstCompile]
        private struct CollectIndicesJob : IJob
        {
            [NoAlias, ReadOnly]
            public NativeParallelMultiHashMap<uint, int> data;

            [NoAlias, ReadOnly]
            public NativeParallelHashSet<uint> cells;

            [NoAlias, WriteOnly]
            public NativeList<int> result;

            public void Execute()
            {
                foreach(uint cell in this.cells)
                {
                    if (this.data.TryGetFirstValue(cell, out int idx, out var it))
                    {
                        this.result.Add(idx);

                        while (this.data.TryGetNextValue(out idx, ref it))
                        {
                            this.result.Add(idx);
                        }
                    }
                }
            }
        }

        private void SetAllPointRendererMaterialsToOutside()
        { 
            for (int i = 0; i < this.pointRenderers.Count; i++)
            {
                var meshRenderer = this.pointRenderers[i];
                meshRenderer.SetPropertyBlock(this.outsideMPB);
            }
        }

        private void SetMultiQueryColors()
        {
            this.SetAllPointRendererMaterialsToOutside();

            NativeList<int> allIndices = new NativeList<int>(Allocator.TempJob);
            var collectIndicesJob = new CollectIndicesJob()
            {
                cells = this.multiQueryCellResults,
                data = this.quadtree.GetDataBuckets(),
                result = allIndices,
            };

            collectIndicesJob.Schedule().Complete();

            for(int i = 0; i < this.multiQueryRings.Length; i++)
            {
                float radius = this.multiQueryRadii[i];
                float2 center = this.multiQueryCenters[i];
                Vector3 centerPos = new Vector3(center.x, 0.0f, center.y);

                for(int j = 0; j < allIndices.Length; j++)
                {
                    int idx = allIndices[j];
                    var position = this.positions[idx];
                    if(Vector3.Distance(centerPos, position) < radius)
                    {
                        var meshRenderer = this.pointRenderers[idx];
                        meshRenderer.SetPropertyBlock(this.insideMPB);
                    }
                }
            }

            allIndices.Dispose();
        }

        private void SetColors()
        {
            var data = this.quadtree.GetDataBuckets();

            this.SetAllPointRendererMaterialsToOutside();

            for (int i = 0; i < this.queryCellResults.Length; i++)
            {
                if (data.TryGetFirstValue(this.queryCellResults[i], out int idx, out var it))
                {
                    if (Vector3.Distance(this.positions[idx], Vector3.zero) < this.searchRadius)
                    {
                        var meshRenderer = this.pointRenderers[idx];
                        meshRenderer.SetPropertyBlock(this.insideMPB);
                    }

                    while(data.TryGetNextValue(out idx, ref it))
                    {
                        if (Vector3.Distance(this.positions[idx], Vector3.zero) < this.searchRadius)
                        {
                            var meshRenderer = this.pointRenderers[idx];
                            meshRenderer.SetPropertyBlock(this.insideMPB);
                        }
                    }
                }
            }
        }

        private void DoQuery()
        {
            if (this.doMultiQuery)
            {

                for(int i = 0; i < this.multiQueryRings.Length; i++)
                {
                    var pos = this.multiQueryRings[i].transform.position;
                    this.multiQueryCenters[i] = new float2(pos.x, pos.z);
                    this.multiQueryRadii[i] = this.searchRadius * 0.5f;
                }

                //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                //increased automatically in a parallel job!
                if(this.multiQueryCellResults.Capacity < this.positionCount)
                {
                    this.multiQueryCellResults.Capacity = this.positionCount;
                }

                var queryJob = this.quadtree.GetCellsInRadii(this.multiQueryCenters, this.multiQueryRadii, ref this.multiQueryCellResults, default, 1);
                queryJob.Complete();
            }
            else
            {
                var queryJob = this.quadtree.GetCellsInRadius(float2.zero,
                    this.searchRadius,
                    ref this.queryCellResults,
                    default);

                queryJob.Complete();
            }
        }

        void Update()
        {
            this.oldPositions.CopyFrom(this.positions);

            var updatePointsJob = new UpdatePointsJob()
            {
                deltaTime = Time.deltaTime,
                bounds = this.bounds,
                positions = this.positions.AsArray(),
                velocities = this.velocities.AsArray(),
                movingPercentage = this.movingPercentage,
                pointSize = this.pointSize,
                rnd = this.rnd,
                yOffset = this.yOffset,
                isPointMoving = this.isPointMoving.AsArray(),
            };

            var updateMovementJob = IJobParallelForTransformExtensions.Schedule(updatePointsJob, this.pointsAccessArray);
            updateMovementJob.Complete();


            updateQuadtreeMarker.Begin();

            if (this.useSparseQuadtree)
            {
                var updateJob = new UpdateSparseQuadtreeJob()
                {
                    newPositions = this.positions,
                    oldPositions = this.oldPositions,
                    quadtree = (NativeSparseQuadtree<int>)this.quadtree,
                    updateList = this.isPointMoving,
                };

                updateJob.Schedule().Complete();

            } else
            {
                var updateJob = new UpdateDenseQuadtreeJob()
                {
                    newPositions = this.positions,
                    oldPositions = this.oldPositions,
                    quadtree = (NativeDenseQuadtree<int>)this.quadtree,
                    updateList = this.isPointMoving
                };

                updateJob.Schedule().Complete();
            }


            updateQuadtreeMarker.End();

            radiusQueryMarker.Begin();

            this.DoQuery();

            radiusQueryMarker.End();

            if (this.doMultiQuery)
            {
                this.SetMultiQueryColors();
            }
            else
            {
                this.SetColors();
            }

            if(this.radiusQuerySampler == null || !this.radiusQuerySampler.isValid)
            {
                this.radiusQuerySampler = Sampler.Get("RadiusQuery");
            }

            if(this.updateQuadtreeSampler == null || !this.updateQuadtreeSampler.isValid)
            {
                this.updateQuadtreeSampler = Sampler.Get("UpdateQuadtree");
            }
        }

        private void Dispose()
        {
            if(this.positions.IsCreated)
            {
                this.positions.Dispose();
            }

            if(this.oldPositions.IsCreated)
            {
                this.oldPositions.Dispose();
            }

            if(this.velocities.IsCreated)
            {
                this.velocities.Dispose();
            }

            if(this.pointsAccessArray.isCreated)
            {
                this.pointsAccessArray.Dispose();
            }

            if(this.quadtree.IsCreated)
            {
                this.quadtree.Dispose();
            }

            if(this.queryCellResults.IsCreated)
            {
                this.queryCellResults.Dispose();
            }

            if(this.isPointMoving.IsCreated)
            {
                this.isPointMoving.Dispose();
            }

            if(this.multiQueryCellResults.IsCreated)
            {
                this.multiQueryCellResults.Dispose();
            }

            if(this.multiQueryCenters.IsCreated)
            {
                this.multiQueryCenters.Dispose();
            }

            if(this.multiQueryRadii.IsCreated)
            {
                this.multiQueryRadii.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
