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
    public class OctreeMovementSystem : MonoBehaviour
    {

        #region Public Variables

        public Color insideColor;
        public Color outsideColor;

        public float yOffset = 0.01f;
        public float pointSize = 0.1f;
        public float initialSearchRadiusPercentage = 0.1f;
        public float initialMovingPercentage = 0.1f;

        public int initialNumberOfPoints;
        public int initialOctreeDepth = 3;

        public GameObject point;

        public Material boxMaterial;
        public Material ringMaterial;

        public Vector3 maxVelocity;

        public Bounds bounds;

        #endregion

        #region Private Variables

        private bool useSparseOctree = true;
        private bool doMultiQuery = false;

        private float searchRadius = 0.0f;
        private float movingPercentage = 0.0f;
        private float currentSearchRadiusPercentage = 0.0f;

        private GameObject boxGO;
        private GameObject ringGO;
        private GameObject[] multiQueryRings;

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private int positionCount = 0;
        private int currentOctreeDepth = 0;

        private List<MeshRenderer> pointRenderers = new List<MeshRenderer>();

        private IOctree<int> octree;

        private NativeArray<float3> multiQueryCenters;
        private NativeArray<float> multiQueryRadii;

        private NativeList<bool> isPointMoving;

        private NativeList<float3> oldPositions;
        private NativeList<float3> positions;
        private NativeList<float3> velocities;

        private NativeList<uint> queryCellResults;
        private NativeParallelHashSet<uint> multiQueryCellResults;

        private Sampler radiusQuerySampler = null;
        private Sampler updateOctreeSampler = null;

        private TransformAccessArray pointsAccessArray;

        private Unity.Mathematics.Random rnd;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker updateOctreeMarker = new ProfilerMarker("UpdateOctree");
        private static readonly ProfilerMarker radiusQueryMarker = new ProfilerMarker("RadiusQuery");

        public bool IsDoingMultiQueries() => this.doMultiQuery;
        public bool IsUsingSparseOctree() => this.useSparseOctree;

        public Sampler GetRadiusQuerySampler() => this.radiusQuerySampler;
        public Sampler GetUpdateOctreeSampler() => this.updateOctreeSampler;

        public int GetNrOfPoints() => this.positionCount;


        public int CurrentSearchDepth
        {
            get => this.currentOctreeDepth;
            set {
                this.currentOctreeDepth = value;
                this.octree.Dispose();
                this.RecreateOctree();
            }
        }

        public float CurrentSearchPercentage
        {
            get => this.currentSearchRadiusPercentage;
            set
            {
                this.currentSearchRadiusPercentage = value;
                this.searchRadius = this.currentSearchRadiusPercentage * this.bounds.size.x * 0.5f;
                this.ScaleRing();
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

            var boundsMin = this.bounds.min;
            var boundsMax = this.bounds.max;


            for (int i = 0; i < nrOfPoints; i++)
            {

                float rndX = UnityEngine.Random.Range(boundsMin.x + pointHalf, boundsMax.x - pointHalf);
                float rndY = UnityEngine.Random.Range(boundsMin.y + pointHalf, boundsMax.y - pointHalf);
                float rndZ = UnityEngine.Random.Range(boundsMin.z + pointHalf, boundsMax.z - pointHalf);

                var worldPos = new Vector3(rndX, rndY, rndZ);

                var point = GameObject.Instantiate(this.point);
                point.transform.parent = this.transform;
                point.transform.position = worldPos;

                var meshRenderer = point.GetComponentInChildren<MeshRenderer>();

                var pos = new float3(rndX, rndY, rndZ);
                this.positions.Add(pos);

                float distFromCenter = math.distance(pos, float3.zero);

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
                float velZ = UnityEngine.Random.Range(-this.maxVelocity.z, this.maxVelocity.z);

                this.velocities.Add(new float3(velX, velY, velZ));

                this.pointsAccessArray.Add(point.transform);
                this.isPointMoving.Add(false);
            }

            this.octree.Dispose();
            this.RecreateOctree();
        }

        private void RecreateOctree()
        {
            if (this.useSparseOctree)
            {
                this.octree = new NativeSparseOctree<int>(float3.zero,
                    this.bounds.extents * 2.0f,
                    this.currentOctreeDepth,
                    this.positionCount,
                    Allocator.Persistent);

            }
            else
            {
                this.octree = new NativeDenseOctree<int>(float3.zero,
                    this.bounds.extents * 2.0f,
                    this.currentOctreeDepth,
                    this.positionCount,
                    Allocator.Persistent);
            }

            this.InsertAllPointsIntoTree();
        }

        public void ExchangeOctreeModel()
        {
            this.octree.Dispose();
            this.useSparseOctree = !this.useSparseOctree;

            this.RecreateOctree();
        }

        private void InsertAllPointsIntoTree()
        {
            for(int i = 0; i < this.positions.Length; i++)
            {
                this.octree.Insert(this.positions[i], i);
            }
        }

        private void ScaleRing()
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
                meshFilter.sharedMesh = newRingMesh;
            }
        }

        private void InitMultiQueries()
        {
            this.multiQueryRings = new GameObject[8];

            float halfSize = this.bounds.extents.x;

            var ringMesh = MeshUtil.CreateRing(this.searchRadius * 0.5f, 0.2f);

            for(int i = 0; i < 8; i++)
            {
                this.multiQueryRings[i] = new GameObject($"Ring_{i}");
                this.multiQueryRings[i].transform.parent = this.transform;

                var ringMeshFilter = this.multiQueryRings[i].AddComponent<MeshFilter>();
                ringMeshFilter.mesh = ringMesh;

                var ringMeshRenderer = this.multiQueryRings[i].AddComponent<MeshRenderer>();
                ringMeshRenderer.material = this.ringMaterial;
                ringMeshRenderer.enabled = false;
            }

            this.multiQueryRings[0].transform.position = new Vector3(-halfSize * 0.5f, -halfSize * 0.5f, -halfSize * 0.5f);
            this.multiQueryRings[1].transform.position = new Vector3(-halfSize * 0.5f, -halfSize * 0.5f, halfSize * 0.5f);
            this.multiQueryRings[2].transform.position = new Vector3(halfSize * 0.5f, -halfSize * 0.5f, -halfSize * 0.5f);
            this.multiQueryRings[3].transform.position = new Vector3(halfSize * 0.5f, -halfSize * 0.5f, halfSize * 0.5f);
            this.multiQueryRings[4].transform.position = new Vector3(-halfSize * 0.5f, halfSize * 0.5f, -halfSize * 0.5f);
            this.multiQueryRings[5].transform.position = new Vector3(-halfSize * 0.5f, halfSize * 0.5f, halfSize * 0.5f);
            this.multiQueryRings[6].transform.position = new Vector3(halfSize * 0.5f, halfSize * 0.5f, -halfSize * 0.5f);
            this.multiQueryRings[7].transform.position = new Vector3(halfSize * 0.5f, halfSize * 0.5f, halfSize * 0.5f);

            this.multiQueryCellResults = new NativeParallelHashSet<uint>(this.initialNumberOfPoints, Allocator.Persistent);

            this.multiQueryCenters = new NativeArray<float3>(this.multiQueryRings.Length, Allocator.Persistent);
            this.multiQueryRadii = new NativeArray<float>(this.multiQueryRings.Length, Allocator.Persistent);
        }

        void Start()
        {
            this.currentOctreeDepth = this.initialOctreeDepth;
            this.currentSearchRadiusPercentage = this.initialSearchRadiusPercentage;

            this.searchRadius = this.initialSearchRadiusPercentage * this.bounds.size.x * 0.5f;

            
            var boxMesh = MeshUtil.CreateInvertedBox(this.bounds);

            this.boxGO = new GameObject("Box");
            this.boxGO.transform.parent = this.transform;
            this.boxGO.transform.position = new Vector3(0.0f, -this.yOffset, 0.0f);

            var boxMeshFilter = this.boxGO.AddComponent<MeshFilter>();
            boxMeshFilter.mesh = boxMesh;

            var boxMeshRenderer = this.boxGO.AddComponent<MeshRenderer>();
            boxMeshRenderer.material = this.boxMaterial;




            var ringMesh = MeshUtil.CreateRing(this.searchRadius, 0.2f);

            this.ringGO = new GameObject("Ring");
            this.ringGO.transform.parent = this.transform;
            this.ringGO.transform.position = new Vector3(0.0f, 2 * this.yOffset, 0.0f);

            var ringMeshFilter = this.ringGO.AddComponent<MeshFilter>();
            ringMeshFilter.mesh = ringMesh;

            var ringMeshRenderer = this.ringGO.AddComponent<MeshRenderer>();
            ringMeshRenderer.material = this.ringMaterial;

            this.InitMultiQueries();


            this.insideMPB = new MaterialPropertyBlock();
            this.outsideMPB = new MaterialPropertyBlock();

            this.insideMPB.SetColor(SHADER_COLOR, this.insideColor);
            this.outsideMPB.SetColor(SHADER_COLOR, this.outsideColor);

            this.pointsAccessArray = new TransformAccessArray(this.initialNumberOfPoints);
            this.positions = new NativeList<float3>(this.initialNumberOfPoints, Allocator.Persistent);
            this.oldPositions = new NativeList<float3>(this.initialNumberOfPoints, Allocator.Persistent);
            this.velocities = new NativeList<float3>(this.initialNumberOfPoints, Allocator.Persistent);
            this.queryCellResults = new NativeList<uint>(this.initialNumberOfPoints, Allocator.Persistent);
            this.isPointMoving = new NativeList<bool>(this.initialNumberOfPoints, Allocator.Persistent);

            this.octree = new NativeSparseOctree<int>(float3.zero, 
                this.bounds.max - this.bounds.min, 
                this.currentOctreeDepth,
                this.initialNumberOfPoints, Allocator.Persistent);


            this.movingPercentage = this.initialMovingPercentage;

            this.rnd = new Unity.Mathematics.Random();
            this.rnd.InitState();

            this.AddPoints(this.initialNumberOfPoints);

        }

        [BurstCompile]
        private struct UpdatePointsJob : IJobParallelForTransform
        {
            public Bounds bounds;

            public float deltaTime;
            public float pointSize;
            public float movingPercentage;
            public float yOffset;

            [NoAlias]
            public NativeArray<bool> isPointMoving;

            [NoAlias]
            public NativeArray<float3> positions;

            [NoAlias]
            public NativeArray<float3> velocities;

            public Unity.Mathematics.Random rnd;

            public void Execute(int index, TransformAccess transform)
            {
                var pos = this.positions[index];

                var velocity = this.velocities[index];

                var max = this.bounds.max;
                var min = this.bounds.min;

                //Only move a percentage, to test the performance differences between sparse and dense octrees
                if (this.rnd.NextFloat() < this.movingPercentage)
                {
                    float nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                    float nextPosY = math.mad(velocity.y, this.deltaTime, pos.y);
                    float nextPosZ = math.mad(velocity.z, this.deltaTime, pos.z);


                    float xMax = max.x - this.pointSize;
                    float xMin = min.x + this.pointSize;
                    float yMax = max.y - this.pointSize;
                    float yMin = min.y + this.pointSize;
                    float zMax = max.z - this.pointSize;
                    float zMin = min.z + this.pointSize;

                    if (Hint.Unlikely(nextPosX > xMax))
                    {
                        velocity = math.reflect(velocity, new float3(-1.0f, 0.0f, 0.0f));
                        nextPosX -= (nextPosX - xMax) * 2.0f;
                    }
                    else if (Hint.Unlikely(nextPosX < xMin))
                    {
                        velocity = math.reflect(velocity, new float3(1.0f, 0.0f, 0.0f));
                        nextPosX += (xMin - nextPosX) * 2.0f;
                    }

                    if (Hint.Unlikely(nextPosY > yMax))
                    {
                        velocity = math.reflect(velocity, new float3(0.0f, -1.0f, 0.0f));
                        nextPosY -= (nextPosY - yMax) * 2.0f;
                    }
                    else if (Hint.Unlikely(nextPosY < yMin))
                    {
                        velocity = math.reflect(velocity, new float3(0.0f, 1.0f, 0.0f));
                        nextPosY += (yMin - nextPosY) * 2.0f;
                    }

                    if(Hint.Unlikely(nextPosZ > zMax))
                    {
                        velocity = math.reflect(velocity, new float3(0.0f, 0.0f, -1.0f));
                        nextPosZ -= (nextPosZ - zMax) * 2.0f;
                    } 
                    else if(Hint.Unlikely(nextPosZ < zMin))
                    {
                        velocity = math.reflect(velocity, new float3(0.0f, 0.0f, 1.0f));
                        nextPosZ += (zMin - nextPosZ) * 2.0f;
                    }

                    this.velocities[index] = velocity;
                    this.positions[index] = new float3(nextPosX, nextPosY, nextPosZ);

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
        private struct UpdateDenseOctreeJob : IJob
        {
            public NativeDenseOctree<int> octree;

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
                        this.octree.Update(i, this.oldPositions[i], this.newPositions[i]);
                    }
                }
            }
        }

        [BurstCompile]
        private struct UpdateSparseOctreeJob : IJob
        {
            public NativeSparseOctree<int> octree;

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
                        this.octree.Update(i, this.oldPositions[i], this.newPositions[i]);
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
                foreach (uint cell in this.cells)
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
                data = this.octree.GetDataBuckets(),
                result = allIndices,
            };

            collectIndicesJob.Schedule().Complete();

            for (int i = 0; i < this.multiQueryRings.Length; i++)
            {
                float radius = this.multiQueryRadii[i];
                float3 center = this.multiQueryCenters[i];

                for (int j = 0; j < allIndices.Length; j++)
                {
                    int idx = allIndices[j];
                    var position = this.positions[idx];
                    if (Vector3.Distance(center, position) < radius)
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
            var data = this.octree.GetDataBuckets();

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
            if(this.doMultiQuery)
            {

                for (int i = 0; i < this.multiQueryRings.Length; i++)
                {
                    var pos = this.multiQueryRings[i].transform.position;
                    this.multiQueryCenters[i] = pos;
                    this.multiQueryRadii[i] = this.searchRadius * 0.5f;
                }

                //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                //increased automatically in a parallel job!
                if (this.multiQueryCellResults.Capacity < this.positionCount)
                {
                    this.multiQueryCellResults.Capacity = this.positionCount;
                }

                var queryJob = this.octree.GetCellsInRadii(this.multiQueryCenters, this.multiQueryRadii, ref this.multiQueryCellResults, default, 1);

                queryJob.Complete();

            } else
            {
                var queryJob = this.octree.GetCellsInRadius(float3.zero,
                    this.searchRadius,
                    ref this.queryCellResults,
                    default);

                queryJob.Complete();
            }
        }

        private void FaceRingsTowardsCamera()
        {
            var camera = Camera.main;
            this.ringGO.transform.up = -camera.transform.forward;
            for(int i = 0; i < this.multiQueryRings.Length; i++)
            {
                this.multiQueryRings[i].transform.up = -camera.transform.forward;
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


            updateOctreeMarker.Begin();

            if (this.useSparseOctree)
            {
                var updateJob = new UpdateSparseOctreeJob()
                {
                    newPositions = this.positions,
                    oldPositions = this.oldPositions,
                    octree = (NativeSparseOctree<int>)this.octree,
                    updateList = this.isPointMoving,
                };

                updateJob.Schedule().Complete();

            } else
            {
                var updateJob = new UpdateDenseOctreeJob()
                {
                    newPositions = this.positions,
                    oldPositions = this.oldPositions,
                    octree = (NativeDenseOctree<int>)this.octree,
                    updateList = this.isPointMoving
                };

                updateJob.Schedule().Complete();
            }


            updateOctreeMarker.End();

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

            if(this.updateOctreeSampler == null || !this.updateOctreeSampler.isValid)
            {
                this.updateOctreeSampler = Sampler.Get("UpdateOctree");
            }

            this.FaceRingsTowardsCamera();
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

            if(this.octree.IsCreated)
            {
                this.octree.Dispose();
            }

            if(this.queryCellResults.IsCreated)
            {
                this.queryCellResults.Dispose();
            }

            if(this.isPointMoving.IsCreated)
            {
                this.isPointMoving.Dispose();
            }

            if (this.multiQueryCellResults.IsCreated)
            {
                this.multiQueryCellResults.Dispose();
            }

            if (this.multiQueryCenters.IsCreated)
            {
                this.multiQueryCenters.Dispose();
            }

            if (this.multiQueryRadii.IsCreated)
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
