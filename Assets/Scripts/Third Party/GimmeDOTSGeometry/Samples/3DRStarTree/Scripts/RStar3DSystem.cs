using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class RStar3DSystem : MonoBehaviour
    {
        #region Public Fields

        public Bounds worldBounds;

        public Camera frustumCamera;

        public Color insideColor;
        public Color outsideColor;

        public float ringThickness = 0.02f;
        public float outlineThickness = 0.02f;
        public float rayThickness = 0.01f;

        public float moveSpeed = 2.0f;

        public float searchRadius = 1.0f;
        public float searchRotationSpeed = 15.0f;
        public float attractorStrength = 0.5f;

        public float attractorA = 0.95f;
        public float attractorB = 7.91f;
        public float attractorF = 4.83f;
        public float attractorG = 4.66f;

        public Gradient internalNodesGradient;

        public int initialBounds = 8;

        public Material boundsMaterial = null;
        public Material rayMaterial = null;
        public Material searchRingMaterial = null;
        public Material treeMaterial = null;

        public RotatingCamera rotationCam;

        public Vector2 boundsWidthRange;
        public Vector2 boundsHeightRange;
        public Vector2 boundsDepthRange;

        public Vector3 maxVelocity;

        #endregion

        #region Private Fields

        private bool doOverlappingQuery = false;
        private bool doFrustumQuery = false;
        private bool doAttractorMovement = false;
        private bool doMultiQuery = false;
        private bool doRaycast = false;
        private bool doBoundsQuery = false;
        private bool doNearestNeighborQuery = false;
        private bool showQuery = true;

        private Dictionary<int, MeshRenderer> idToRenderer = new Dictionary<int, MeshRenderer>();

        private float searchRotation = 0.0f;

        private GameObject raycastLine;
        private GameObject neighborLine;
        private GameObject[] frustumLines;
        private GameObject searchRing;
        private GameObject searchBounds;
        private GameObject[] multiQuerySearchRings;
        private GameObject[] multiQuerySearchBounds;

        private int boundsCount = 0;

        private List<GameObject> boundsPool = new List<GameObject>();

        private List<GameObject> boundsObjects = new List<GameObject>();
        private List<MeshRenderer> boundsMeshRenderer = new List<MeshRenderer>();

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private MaterialPropertyBlock internalNodeBlock;

        private NativeArray<float> multiQueryRadii;
        private NativeArray<float3> multiQueryCenters;
        private NativeArray<Bounds> multiQueryBounds;

        private Native3DRStarTree<RStarBounds> rTree;

        private NativeArray<float3> nearestNeighborQueryPos;
        private NativeArray<RStarBounds> nearestNeighbor;

        private NativeList<RStarBounds> bounds;
        private NativeList<RStarBounds> queryBoundsResults;
        private NativeList<RStarBounds> frustumQueryBoundsResults;
        private NativeList<float3> velocities;

        private NativeList<IntersectionHit3D<RStarBounds>> intersectionHits;

        private NativeParallelHashSet<RStarBounds> multiQueryBoundsResults;


        private Sampler updateBoundsSampler = null;
        private Sampler radiusQuerySampler = null;
        private Sampler optimizeSampler = null;
        private Sampler raycastSampler = null;
        private Sampler frustumQuerySampler = null;
        private Sampler nearestNeighborSampler = null;

        private TransformAccessArray boundsAccessArray;

        private Vector3 rayStart;
        private Vector3 rayEnd;

        private Vector3 currentPos;
        private Vector3[] searchRingOffsets;

        private float3 nearestNeighborPos;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker radiusQueryMarker = new ProfilerMarker("RadiusQuery");
        private static readonly ProfilerMarker updateBoundsMarker = new ProfilerMarker("UpdateBounds");
        private static readonly ProfilerMarker optimizeMarker = new ProfilerMarker("Optimize");
        private static readonly ProfilerMarker raycastMarker = new ProfilerMarker("Raycast");
        private static readonly ProfilerMarker frustumMarker = new ProfilerMarker("FrustumQuery");
        private static readonly ProfilerMarker nearestNeighborMarker = new ProfilerMarker("NearestNeighbor");

        public bool IsDoingRaycast() => this.doRaycast;
        public bool IsDoingMultiQuery() => this.doMultiQuery;
        public bool IsDoingBoundsQuery() => this.doBoundsQuery;
        public bool IsDoingNearestNeighborQuery() => this.doNearestNeighborQuery;
        public bool IsShowingQuery() => this.showQuery;
        public bool IsDoingOverlappingQuery() => this.doOverlappingQuery;

        public bool IsDoingFrustumQuery() => this.doFrustumQuery;

        public bool IsDoingAttractorMovement() => this.doAttractorMovement;


        public int GetNrOfBounds() => this.bounds.Length;

        public Sampler GetUpdateBoundsSampler() => this.updateBoundsSampler;
        public Sampler GetRadiusQuerySampler() => this.radiusQuerySampler;
        public Sampler GetOptimizeSampler() => this.optimizeSampler;
        public Sampler GetRaycastSampler() => this.raycastSampler;
        public Sampler GetFrustumQuerySampler() => this.frustumQuerySampler;
        public Sampler GetNearestNeighborSampler() => this.nearestNeighborSampler;

        public void SetRaycastParameters(Vector3 rayStart, Vector3 rayEnd)
        {
            this.rayStart = rayStart;
            this.rayEnd = rayEnd;
        }



        public void AddRandomBounds(int nrOfBounds)
        {
            var min = this.worldBounds.min;
            var max = this.worldBounds.max;
            for (int i = 0; i < nrOfBounds; i++)
            {

                float width = UnityEngine.Random.Range(this.boundsWidthRange.x, this.boundsWidthRange.y);
                float height = UnityEngine.Random.Range(this.boundsHeightRange.x, this.boundsHeightRange.y);
                float depth = UnityEngine.Random.Range(this.boundsDepthRange.x, this.boundsDepthRange.y);

                float rndX = UnityEngine.Random.Range(min.x, max.x - width);
                float rndY = UnityEngine.Random.Range(min.y, max.y - height);
                float rndZ = UnityEngine.Random.Range(min.z, max.z - depth);

                var worldPos = new Vector3(rndX, rndY, rndZ);

                Vector3 size = new Vector3(width, height, depth);
                var bounds = new RStarBounds()
                {
                    Bounds = new Bounds(worldPos, size),
                    ID = this.boundsCount + i,
                };

                var boundsObj = new GameObject($"Bounds_{this.boundsCount + i}");
                boundsObj.transform.parent = this.transform;
                boundsObj.transform.position = worldPos;
 
                var meshRenderer = boundsObj.AddComponent<MeshRenderer>();
                var meshFilter = boundsObj.AddComponent<MeshFilter>();

                var outlineBounds = new Bounds(Vector3.zero, size);
                var boundsMesh = MeshUtil.CreateBoxOutline(outlineBounds, this.outlineThickness);

                meshFilter.sharedMesh = boundsMesh;
                meshRenderer.sharedMaterial = this.boundsMaterial;


                this.boundsObjects.Add(boundsObj);

                this.boundsMeshRenderer.Add(meshRenderer);

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);
                float velZ = UnityEngine.Random.Range(-this.maxVelocity.z, this.maxVelocity.z);

                this.bounds.Add(bounds);
                this.velocities.Add(new float3(velX, velY, velZ));

                this.boundsAccessArray.Add(boundsObj.transform);

                this.rTree.Insert(bounds);

                this.idToRenderer.Add(bounds.ID, meshRenderer);
            }

            this.boundsCount += nrOfBounds;
        }

        public void RemoveRandomBounds(int nrOfBounds)
        {
            NativeList<RStarBounds> boundsToRemove = new NativeList<RStarBounds>(Allocator.TempJob);
            for (int i = 0; i < nrOfBounds; i++)
            {
                if (this.boundsObjects.Count <= 0) break;

                var rndBounds = UnityEngine.Random.Range(0, this.boundsObjects.Count);
                var bounds = this.bounds[rndBounds];

                var boundsObj = this.boundsObjects[rndBounds];

                this.boundsObjects.RemoveAtSwapBack(rndBounds);
                this.boundsMeshRenderer.RemoveAtSwapBack(rndBounds);
                this.boundsAccessArray.RemoveAtSwapBack(rndBounds);

                this.bounds.RemoveAtSwapBack(rndBounds);
                this.velocities.RemoveAtSwapBack(rndBounds);

                boundsToRemove.Add(bounds);

                this.idToRenderer.Remove(bounds.ID);

                GameObject.Destroy(boundsObj);
            }

            this.rTree.RemoveAll(boundsToRemove).Complete();
            boundsToRemove.Dispose();
        }



        [BurstCompile]
        private struct UpdateBoundsJob : IJobParallelForTransform
        {

            public bool lorenzAttractor;

            public Bounds worldBounds;

            public float deltaTime;

            public float attractorA;
            public float attractorB;
            public float attractorF;
            public float attractorG;
            public float attractorStrength;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<RStarBounds> bounds;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<float3> velocities;


            public void Execute(int index, TransformAccess transform)
            {
                var bounds = this.bounds[index];
                var pos = transform.position;
                var velocity = this.velocities[index];

                float nextPosX, nextPosY, nextPosZ;
                if (this.lorenzAttractor)
                {
                    float attrX = -this.attractorA * pos.x - pos.y * pos.y - pos.z * pos.z + this.attractorA * this.attractorF;
                    float attrY = -pos.y + pos.x * pos.y - this.attractorB * pos.x * pos.z + this.attractorG;
                    float attrZ = -pos.z + this.attractorB * pos.x * pos.y + pos.x * pos.z;

                    float3 diff = new float3(attrX, attrY, attrZ) - (float3)pos;
                    diff = math.normalize(diff) * math.length(velocity) * this.deltaTime * this.attractorStrength;
                    diff += velocity * this.deltaTime * (1.0f - this.attractorStrength);

                    nextPosX = pos.x + diff.x;
                    nextPosY = pos.y + diff.y;
                    nextPosZ = pos.z + diff.z;
                }
                else
                {
                    nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                    nextPosY = math.mad(velocity.y, this.deltaTime, pos.y);
                    nextPosZ = math.mad(velocity.z, this.deltaTime, pos.z);
                }

                float3 max = this.worldBounds.max - bounds.Bounds.size;
                float3 min = this.worldBounds.min;

                if (Hint.Unlikely(nextPosX > max.x))
                {
                    velocity = math.reflect(velocity, new float3(-1.0f, 0.0f, 0.0f));
                    nextPosX -= (nextPosX - max.x) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosX < min.x))
                {
                    velocity = math.reflect(velocity, new float3(1.0f, 0.0f, 0.0f));
                    nextPosX += (min.x - nextPosX) * 2.0f;
                }

                if (Hint.Unlikely(nextPosY > max.y))
                {
                    velocity = math.reflect(velocity, new float3(0.0f, -1.0f, 0.0f));
                    nextPosY -= (nextPosY - max.y) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosY < min.y))
                {
                    velocity = math.reflect(velocity, new float3(0.0f, 1.0f, 0.0f));
                    nextPosY += (min.y - nextPosY) * 2.0f;
                }

                if (Hint.Unlikely(nextPosZ > max.z))
                {
                    velocity = math.reflect(velocity, new float3(0.0f, 0.0f, -1.0f));
                    nextPosZ -= (nextPosZ - max.z) * 2.0f;
                }
                else if (Hint.Unlikely(nextPosZ < min.z))
                {
                    velocity = math.reflect(velocity, new float3(0.0f, 0.0f, 1.0f));
                    nextPosZ += (min.z - nextPosZ) * 2.0f;
                }

                this.velocities[index] = velocity;

                var b = bounds.Bounds;
                b.center = new float3(nextPosX, nextPosY, nextPosZ);
                bounds.Bounds = b;
                this.bounds[index] = bounds;

                transform.position = new float3(nextPosX, nextPosY, nextPosZ);
            }
        }

        public void EnableRaycast(bool enable)
        {
            this.doRaycast = enable;
        }

        public void EnableAttractor(bool enable)
        {
            this.doAttractorMovement = enable;
        }

        private void HandleSearchMeshesState()
        {
            var mr = this.searchRing.GetComponent<MeshRenderer>();
            mr.enabled = !this.doBoundsQuery && !this.doMultiQuery;

            mr = this.searchBounds.GetComponent<MeshRenderer>();
            mr.enabled = this.doBoundsQuery && !this.doMultiQuery;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                mr = this.multiQuerySearchRings[i].GetComponent<MeshRenderer>();
                mr.enabled = !this.doBoundsQuery && this.doMultiQuery;
            }

            for (int i = 0; i < this.multiQuerySearchBounds.Length; i++)
            {
                mr = this.multiQuerySearchBounds[i].GetComponent<MeshRenderer>();
                mr.enabled = this.doBoundsQuery && this.doMultiQuery;
            }
        }

        public void EnableNearestNeighborQuery(bool enable)
        {
            this.doNearestNeighborQuery = enable;
        }

        public void EnableBoundsQuery(bool enable)
        {
            this.doBoundsQuery = enable;

            this.HandleSearchMeshesState();
        }

        public void EnableMultiQuery(bool enable)
        {
            this.doMultiQuery = enable;

            this.HandleSearchMeshesState();
        }

        public void EnableFrustumQuery(bool enable)
        {
            this.doFrustumQuery = enable;
        }

        public void EnableOverlappingQuery(bool enable)
        {
            this.doOverlappingQuery = enable;
        }

        private Bounds CalculateSearchBounds()
        {
            return new Bounds(Vector3.zero, Vector3.one * this.searchRadius * 2);
        }


        private void InitMultiQueries()
        {
            this.multiQuerySearchRings = new GameObject[8];
            this.multiQuerySearchBounds = new GameObject[8];
            this.searchRingOffsets = new Vector3[8];

            var searchRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.ringThickness, 64);

            var bounds = CalculateSearchBounds();
            bounds.extents *= 0.5f;
            var searchBounds = MeshUtil.CreateBoxOutline(bounds, this.ringThickness);

            for (int i = 0; i < 8; i++)
            {
                this.multiQuerySearchRings[i] = new GameObject($"Ring_{i}");
                this.multiQuerySearchRings[i].transform.parent = this.transform;

                var ringMeshFilter = this.multiQuerySearchRings[i].AddComponent<MeshFilter>();
                ringMeshFilter.mesh = searchRing;

                var ringMeshRenderer = this.multiQuerySearchRings[i].AddComponent<MeshRenderer>();
                ringMeshRenderer.material = this.searchRingMaterial;
                ringMeshRenderer.enabled = false;
            }

            for (int i = 0; i < 8; i++)
            {
                this.multiQuerySearchBounds[i] = new GameObject($"Bounds_{i}");
                this.multiQuerySearchBounds[i].transform.parent = this.transform;

                var boundsMeshFilter = this.multiQuerySearchBounds[i].AddComponent<MeshFilter>();
                boundsMeshFilter.mesh = searchBounds;

                var boundsMeshRenderer = this.multiQuerySearchBounds[i].AddComponent<MeshRenderer>();
                boundsMeshRenderer.material = this.searchRingMaterial;
                boundsMeshRenderer.enabled = false;
            }


            this.searchRingOffsets[0] = new Vector3(this.searchRadius, -this.searchRadius, 0.0f);
            this.searchRingOffsets[1] = new Vector3(0.0f, -this.searchRadius, -this.searchRadius);
            this.searchRingOffsets[2] = new Vector3(-this.searchRadius, -this.searchRadius, 0.0f);
            this.searchRingOffsets[3] = new Vector3(0.0f, -this.searchRadius, this.searchRadius);

            this.searchRingOffsets[4] = new Vector3(this.searchRadius, this.searchRadius, 0.0f);
            this.searchRingOffsets[5] = new Vector3(0.0f, this.searchRadius, -this.searchRadius);
            this.searchRingOffsets[6] = new Vector3(-this.searchRadius, this.searchRadius, 0.0f);
            this.searchRingOffsets[7] = new Vector3(0.0f, this.searchRadius, this.searchRadius);

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                this.multiQuerySearchRings[i].transform.position = this.transform.position + this.searchRingOffsets[i];
                this.multiQuerySearchBounds[i].transform.position = this.transform.position + this.searchRingOffsets[i];
            }

            this.multiQueryBoundsResults = new NativeParallelHashSet<RStarBounds>(this.initialBounds, Allocator.Persistent);

            this.multiQueryRadii = new NativeArray<float>(8, Allocator.Persistent);
            this.multiQueryCenters = new NativeArray<float3>(8, Allocator.Persistent);
            this.multiQueryBounds = new NativeArray<Bounds>(8, Allocator.Persistent);
        }

        private void CreateSearchMeshes()
        {

            this.searchRing = new GameObject("Search Ring");
            this.searchRing.transform.parent = this.transform;

            var searchRing = MeshUtil.CreateRing(this.searchRadius, this.ringThickness, 64);

            var meshFilter = this.searchRing.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchRing;

            var meshRenderer = this.searchRing.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;



            this.searchBounds = new GameObject("Search Bounds");
            this.searchBounds.transform.parent = this.transform;

            var bounds = this.CalculateSearchBounds();
            var searchBounds = MeshUtil.CreateBoxOutline(bounds, this.ringThickness);

            meshFilter = this.searchBounds.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchBounds;

            meshRenderer = this.searchBounds.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;
            meshRenderer.enabled = false;


            this.raycastLine = new GameObject("Raycast Line");
            this.raycastLine.transform.parent = this.transform;

            this.raycastLine.AddComponent<MeshFilter>();

            meshRenderer = this.raycastLine.AddComponent<MeshRenderer>();
            meshRenderer.material = this.rayMaterial;

            this.frustumLines = new GameObject[4];
            for (int i = 0; i < this.frustumLines.Length; i++)
            {
                this.frustumLines[i] = new GameObject($"Frustum Line {i}");
                this.frustumLines[i].transform.parent = this.transform;
                this.frustumLines[i].AddComponent<MeshFilter>();

                meshRenderer = this.frustumLines[i].AddComponent<MeshRenderer>();
                meshRenderer.material = this.rayMaterial;
            }

            this.neighborLine = new GameObject("Neighbor Line");
            this.neighborLine.transform.parent = this.transform;

            var line = MeshUtil.CreateLine(new LineSegment3D() { a = float3.zero, b = new float3(1, 1, 1) }, this.rayThickness);

            meshFilter = this.neighborLine.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = line;

            meshRenderer = this.neighborLine.AddComponent<MeshRenderer>();
            meshRenderer.material = this.rayMaterial;
        }

        private void Start()
        {
            this.insideMPB = new MaterialPropertyBlock();
            this.outsideMPB = new MaterialPropertyBlock();
            this.internalNodeBlock = new MaterialPropertyBlock();

            this.insideMPB.SetColor(SHADER_COLOR, this.insideColor);
            this.outsideMPB.SetColor(SHADER_COLOR, this.outsideColor);

            this.boundsAccessArray = new TransformAccessArray(this.initialBounds);

            this.bounds = new NativeList<RStarBounds>(this.initialBounds, Allocator.Persistent);
            this.queryBoundsResults = new NativeList<RStarBounds>(this.initialBounds, Allocator.Persistent);
            this.frustumQueryBoundsResults = new NativeList<RStarBounds>(this.initialBounds, Allocator.Persistent);
            this.velocities = new NativeList<float3>(this.initialBounds, Allocator.Persistent);

            this.rTree = new Native3DRStarTree<RStarBounds>(this.initialBounds, Allocator.Persistent);

            this.intersectionHits = new NativeList<IntersectionHit3D<RStarBounds>>(16, Allocator.Persistent);

            this.nearestNeighbor = new NativeArray<RStarBounds>(1, Allocator.Persistent);
            this.nearestNeighborQueryPos = new NativeArray<float3>(1, Allocator.Persistent);

            this.CreateSearchMeshes();

            var boundsMin = this.worldBounds.min;
            var boundsMax = this.worldBounds.max;
            this.treeMaterial.SetVector("_Min", new Vector4(boundsMin.x, boundsMin.y, boundsMin.z, 0.0f));
            this.treeMaterial.SetVector("_Max", new Vector4(boundsMax.x, boundsMax.y, boundsMax.z, 0.0f));


            this.InitMultiQueries();

            this.AddRandomBounds(this.initialBounds);
        }


        private void FaceRingsTowardsCamera()
        {
            this.searchRing.transform.position = this.currentPos;
            this.searchRing.transform.up = -this.rotationCam.transform.forward;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                this.multiQuerySearchRings[i].transform.up = -this.rotationCam.transform.forward;
            }
        }
        private void UpdateSearchBoundsPosition()
        {
            this.searchBounds.transform.position = this.currentPos;
        }


        public void UpdateSearchBounds()
        {
            var bounds = this.CalculateSearchBounds();
            var newSearchBounds = MeshUtil.CreateBoxOutline(bounds, this.ringThickness);

            var meshFilter = this.searchBounds.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = newSearchBounds;

            bounds.extents *= 0.5f;

            var newMultiQueryBounds = MeshUtil.CreateBoxOutline(bounds, this.ringThickness);

            for (int i = 0; i < this.multiQuerySearchBounds.Length; i++)
            {
                var searchBounds = this.multiQuerySearchBounds[i];
                meshFilter = searchBounds.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = newMultiQueryBounds;
                this.searchRingOffsets[i] = this.searchRingOffsets[i].normalized * this.searchRadius;
            }
        }


        public void UpdateSearchRingRadius()
        {
            var newSearchRing = MeshUtil.CreateRing(this.searchRadius, this.ringThickness, 64);
            var meshFilter = this.searchRing.GetComponent<MeshFilter>();

            meshFilter.sharedMesh = newSearchRing;

            var newMultiQueryRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.ringThickness, 64);

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var ring = this.multiQuerySearchRings[i];
                meshFilter = ring.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = newMultiQueryRing;
                this.searchRingOffsets[i] = this.searchRingOffsets[i].normalized * this.searchRadius;
            }
        }


        private void UpdateMultiQuerySearch()
        {
            this.searchRotation += Time.deltaTime * this.searchRotationSpeed;

            var bounds = this.CalculateSearchBounds();
            bounds.extents *= 0.5f;
            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var offset = Quaternion.AngleAxis(this.searchRotation, Vector3.up) * this.searchRingOffsets[i];
                var pos = this.currentPos + offset;

                this.multiQuerySearchRings[i].transform.position = pos;
                this.multiQuerySearchBounds[i].transform.position = pos;

                this.multiQueryRadii[i] = this.searchRadius * 0.5f;
                this.multiQueryCenters[i] = pos;

                bounds.center = pos;
                this.multiQueryBounds[i] = bounds;
            }
        }

        private void HandleInput()
        {
            if (Input.GetKey(KeyCode.W))
            {
                this.currentPos.z += this.moveSpeed * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                this.currentPos.z -= this.moveSpeed * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.A))
            {
                this.currentPos.x -= this.moveSpeed * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.D))
            {
                this.currentPos.x += this.moveSpeed * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.E))
            {
                this.currentPos.y += this.moveSpeed * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.Q))
            {
                this.currentPos.y -= this.moveSpeed * Time.deltaTime;
            }

            this.currentPos = Vector3.Max(this.currentPos, this.worldBounds.min);
            this.currentPos = Vector3.Min(this.currentPos, this.worldBounds.max);
        }

        private Mesh CreateInternalNodeMesh()
        {
            var boundsOutline = MeshUtil.CreateBoxOutline(new Bounds(Vector3.one * 0.5f, Vector3.one), this.outlineThickness);

            return boundsOutline;
        }

        private void AddBoundsMeshToPool(RStarNode3D node)
        {
            var mesh = this.CreateInternalNodeMesh();

            var boundsObj = new GameObject($"R*Tree3D_Bounds");
            boundsObj.transform.parent = this.transform;
            boundsObj.transform.position = node.Bounds.center;

            var meshRenderer = boundsObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = this.treeMaterial;

            var meshFilter = boundsObj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            this.boundsPool.Add(boundsObj);
        }

        private unsafe void DrawTreeRecursion(RStarNode3D node, ref int boundsMeshIndex, int maxHeight, int height)
        {
            if (boundsMeshIndex >= this.boundsPool.Count)
            {
                this.AddBoundsMeshToPool(node);
            }
            var bounds = this.boundsPool[boundsMeshIndex];
            bounds.SetActive(true);
            bounds.transform.position = node.Bounds.center;
            bounds.transform.localScale = node.Bounds.size;

            float heightPercentage = height / (float)(maxHeight + Mathf.Epsilon);
            Color color = this.internalNodesGradient.Evaluate(heightPercentage);
            this.internalNodeBlock.SetColor(SHADER_COLOR, color);

            var boundsRenderer = bounds.GetComponent<MeshRenderer>();
            boundsRenderer.SetPropertyBlock(this.internalNodeBlock);

            boundsMeshIndex++;

            if (node.left >= 0)
            {
                var leftNode = this.rTree.GetNode(node.left);
                this.DrawTreeRecursion(leftNode, ref boundsMeshIndex, maxHeight, height + 1);
            }

            if (node.right >= 0)
            {
                var rightNode = this.rTree.GetNode(node.right);
                this.DrawTreeRecursion(rightNode, ref boundsMeshIndex, maxHeight, height + 1);
            }
        }

        private void FindMaxHeightRecursion(RStarNode3D node, ref int maxHeight, int currentHeight)
        {
            maxHeight = math.max(currentHeight + 1, maxHeight);

            if (node.left >= 0)
            {
                var leftNode = this.rTree.GetNode(node.left);
                this.FindMaxHeightRecursion(leftNode, ref maxHeight, currentHeight + 1);
            }

            if (node.right >= 0)
            {
                var rightNode = this.rTree.GetNode(node.right);
                this.FindMaxHeightRecursion(rightNode, ref maxHeight, currentHeight + 1);
            }
        }

        private unsafe void DrawRTree()
        {
            int boundsMeshIndex = 0;

            var root = this.rTree.GetRoot();
            int maxHeight = 0;
            this.FindMaxHeightRecursion(*root, ref maxHeight, 0);

            this.DrawTreeRecursion(*root, ref boundsMeshIndex, maxHeight, 0);

            for (int i = boundsMeshIndex; i < this.boundsPool.Count; i++)
            {
                this.boundsPool[i].SetActive(false);
            }

        }


        private void DrawRaycast()
        {
            if (this.rayStart != this.rayEnd)
            {
                var lineSegment = new LineSegment3D()
                {
                    a = this.rayStart,
                    b = this.rayEnd,
                };

                var dir = this.rayEnd - this.rayStart;
                if (dir != Vector3.zero)
                {
                    var line = MeshUtil.CreateLine(lineSegment, 0.02f);

                    var meshFilter = this.raycastLine.GetComponent<MeshFilter>();
                    GameObject.Destroy(meshFilter.sharedMesh);
                    meshFilter.sharedMesh = line;
                }
            }
        }

        private void DrawNearestNeighborLine()
        {
            this.nearestNeighborPos = this.nearestNeighbor[0].Bounds.center;
            var ls = new LineSegment3D()
            {
                a = this.nearestNeighborPos,
                b = this.currentPos,
            };

            var mf = this.neighborLine.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.CreateLine(ls, this.rayThickness);
            this.neighborLine.SetActive(true);
        }

        private void DrawFrustumQuery()
        {
            Vector3[] frustumCorners = new Vector3[4];
            this.frustumCamera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), this.worldBounds.size.x, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
            var cameraPos = this.frustumCamera.transform.position;
            
            for(int i = 0; i < frustumCorners.Length; i++)
            {
                var worldSpaceCorner = this.frustumCamera.transform.TransformVector(frustumCorners[i]);
                var ls = new LineSegment3D(cameraPos, worldSpaceCorner);

                var lineMesh = MeshUtil.CreateLine(ls, 0.02f);

                var meshFilter = this.frustumLines[i].GetComponent<MeshFilter>();
                GameObject.Destroy(meshFilter.sharedMesh);
                meshFilter.sharedMesh = lineMesh;
            }
        }
        private void SetAllBoundsColorsToOutside()
        {
            for (int i = 0; i < this.boundsObjects.Count; i++)
            {
                var meshRenderer = this.boundsMeshRenderer[i];
                meshRenderer.SetPropertyBlock(this.outsideMPB);
            }
        }

        private void SetRaycastColors()
        {
            if (this.doRaycast)
            {
                for (int i = 0; i < this.intersectionHits.Length; i++)
                {
                    var intersection = this.intersectionHits[i];
                    var bounds = intersection.boundingVolume;

                    var meshRenderer = this.idToRenderer[bounds.ID];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }

        private void SetFrustumQueryColors()
        {
            if(this.doFrustumQuery)
            {
                for(int i = 0; i <this.frustumQueryBoundsResults.Length; i++)
                {
                    var bounds = this.frustumQueryBoundsResults[i];
                    int id = bounds.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }


        public void ShowQuery(bool enable)
        {
            this.showQuery = enable;


            this.searchRing.SetActive(enable);
            this.searchBounds.SetActive(enable);

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                this.multiQuerySearchRings[i].SetActive(enable);
            }

            for (int i = 0; i < this.multiQuerySearchBounds.Length; i++)
            {
                this.multiQuerySearchBounds[i].SetActive(enable);
            }

        }


        private void SetColors()
        {
            this.SetAllBoundsColorsToOutside();
            this.SetRaycastColors();
            this.SetFrustumQueryColors();

            if (this.doMultiQuery)
            {
                foreach (var bounds in this.multiQueryBoundsResults)
                {
                    int id = bounds.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }

            }
            else
            {
                for (int i = 0; i < this.queryBoundsResults.Length; i++)
                {
                    var bounds = this.queryBoundsResults[i];
                    int id = bounds.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }

        private void Update()
        {
            this.queryBoundsResults.Clear();
            this.multiQueryBoundsResults.Clear();

            if(this.showQuery)
            {
                this.FaceRingsTowardsCamera();
                this.UpdateSearchBoundsPosition();

                this.UpdateMultiQuerySearch();
            }

            this.nearestNeighborQueryPos[0] = this.currentPos;

            var updateBoundsJob = new UpdateBoundsJob()
            {
                deltaTime = Time.deltaTime,
                attractorA = this.attractorA,
                attractorB = this.attractorB,
                attractorF = this.attractorF,
                attractorG = this.attractorG,
                attractorStrength = this.attractorStrength,
                lorenzAttractor = this.doAttractorMovement,
                bounds = this.bounds.AsArray(),
                velocities = this.velocities.AsArray(),
                worldBounds = this.worldBounds,
            };

            var updateBoundsHandle = IJobParallelForTransformExtensions.Schedule(updateBoundsJob, this.boundsAccessArray);
            updateBoundsHandle.Complete();

            updateBoundsMarker.Begin();

            var updateAllJob = this.rTree.UpdateAll(this.bounds);
            updateAllJob.Complete();

            updateBoundsMarker.End();

            radiusQueryMarker.Begin();

            if (this.showQuery)
            {
                if (this.doMultiQuery)
                {
                    //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                    //increased automatically in a parallel job!
                    if (this.multiQueryBoundsResults.Capacity < this.GetNrOfBounds())
                    {
                        this.multiQueryBoundsResults.Capacity = this.GetNrOfBounds();
                    }

                    if (this.doBoundsQuery)
                    {
                        if (this.doOverlappingQuery)
                        {
                            var boundsQuery = this.rTree.GetOverlappingBoundsInMultipleBounds(this.multiQueryBounds,
                                ref this.multiQueryBoundsResults, default, 1);
                            boundsQuery.Complete();
                        }
                        else
                        {
                            var boundsQuery = this.rTree.GetBoundsInMultipleBounds(this.multiQueryBounds, ref this.multiQueryBoundsResults,
                                default, 1);
                            boundsQuery.Complete();
                        }
                    }
                    else
                    {
                        if (this.doOverlappingQuery)
                        {
                            var radiusQuery = this.rTree.GetOverlappingBoundsInRadii(this.multiQueryCenters, this.multiQueryRadii,
                                ref this.multiQueryBoundsResults, default, 1);
                            radiusQuery.Complete();
                        }
                        else
                        {
                            var radiusQuery = this.rTree.GetBoundsInRadii(this.multiQueryCenters, this.multiQueryRadii, 
                                ref this.multiQueryBoundsResults, default, 1);
                            radiusQuery.Complete();
                        }
                    }
                }
                else
                {
                    if (this.doBoundsQuery)
                    {
                        var bounds = this.CalculateSearchBounds();
                        bounds.center += this.currentPos;

                        if (this.doOverlappingQuery)
                        {
                            var boundsQuery = this.rTree.GetOverlappingBoundsInBounds(bounds, ref this.queryBoundsResults);
                            boundsQuery.Complete();
                        }
                        else
                        {
                            var boundsQuery = this.rTree.GetBoundsInBounds(bounds, ref this.queryBoundsResults);
                            boundsQuery.Complete();
                        }
                    }
                    else
                    {
                        if (this.doOverlappingQuery)
                        {
                            var radiusQuery = this.rTree.GetOverlappingBoundsInRadius(this.currentPos, this.searchRadius,
                                ref this.queryBoundsResults);
                            radiusQuery.Complete();
                        }
                        else
                        {
                            var radiusQuery = this.rTree.GetBoundsInRadius(this.currentPos, this.searchRadius, ref this.queryBoundsResults);
                            radiusQuery.Complete();
                        }
                    }
                }
            }

            radiusQueryMarker.End();

            optimizeMarker.Begin();

            var optimizeJob = this.rTree.Optimize(128, 64);
            optimizeJob.Complete();

            optimizeMarker.End();

            if (this.doRaycast)
            {
                raycastMarker.Begin();

                var direction = (this.rayEnd - this.rayStart);
                var ray = new Ray()
                {
                    direction = direction.normalized,
                    origin = this.rayStart,
                };
                var raycastJob = this.rTree.Raycast(ray, direction.magnitude, ref this.intersectionHits);
                raycastJob.Complete();

                raycastMarker.End();
            }

            if(this.doFrustumQuery)
            {
                frustumMarker.Begin();

                var frustumQueryJob = this.rTree.FrustumQuery(this.frustumCamera, ref this.frustumQueryBoundsResults);
                frustumQueryJob.Complete();

                frustumMarker.End();
            }

            if(this.doNearestNeighborQuery)
            {
                nearestNeighborMarker.Begin();

                var nnQueryJob = this.rTree.GetNearestNeighbors(this.nearestNeighborQueryPos, ref this.nearestNeighbor);
                nnQueryJob.Complete();

                nearestNeighborMarker.End();
            }

            this.SetColors();

            if (this.updateBoundsSampler == null || !this.updateBoundsSampler.isValid)
            {
                this.updateBoundsSampler = Sampler.Get("UpdateBounds");
            }

            if (this.radiusQuerySampler == null || !this.radiusQuerySampler.isValid)
            {
                this.radiusQuerySampler = Sampler.Get("RadiusQuery");
            }

            if (this.optimizeSampler == null || !this.optimizeSampler.isValid)
            {
                this.optimizeSampler = Sampler.Get("Optimize");
            }

            if (this.raycastSampler == null || !this.raycastSampler.isValid)
            {
                this.raycastSampler = Sampler.Get("Raycast");
            }

            if (this.frustumQuerySampler == null || !this.frustumQuerySampler.isValid)
            {
                this.frustumQuerySampler = Sampler.Get("FrustumQuery");
            }

            if(this.nearestNeighborSampler == null || !this.nearestNeighborSampler.isValid)
            {
                this.nearestNeighborSampler = Sampler.Get("NearestNeighbor");
            }

            this.DrawRTree();
            if (this.doRaycast)
            {
                this.raycastLine.SetActive(true);
                this.DrawRaycast();
            }
            else
            {
                this.raycastLine.SetActive(false);
            }

            if(this.doFrustumQuery)
            {
                this.frustumCamera.gameObject.SetActive(true);
                this.DrawFrustumQuery();
            } else
            {
                this.frustumCamera.gameObject.SetActive(false);
            }

            if(this.doNearestNeighborQuery)
            {
                this.DrawNearestNeighborLine();
            } else
            {
                this.neighborLine.SetActive(false);
            }

            this.rotationCam.SetTargetPoint(this.currentPos);
            this.HandleInput();
        }

        private void Dispose()
        {
            this.bounds.DisposeIfCreated();
            this.queryBoundsResults.DisposeIfCreated();
            this.frustumQueryBoundsResults.DisposeIfCreated();
            this.velocities.DisposeIfCreated();

            this.multiQueryBoundsResults.DisposeIfCreated();
            this.multiQueryRadii.DisposeIfCreated();
            this.multiQueryCenters.DisposeIfCreated();
            this.multiQueryBounds.DisposeIfCreated();

            this.intersectionHits.DisposeIfCreated();

            this.nearestNeighbor.DisposeIfCreated();
            this.nearestNeighborQueryPos.DisposeIfCreated();

            if (this.rTree.IsCreated)
            {
                this.rTree.Dispose();
            }

            if (this.boundsAccessArray.isCreated)
            {
                this.boundsAccessArray.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
