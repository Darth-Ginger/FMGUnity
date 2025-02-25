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
    public class BallStar3DSystem : MonoBehaviour
    {
        #region Public Variables

        public enum RaycastType
        {
            LINE = 0,
            SPHERE = 1,
        };

        public Bounds bounds;

        public Camera frustumCamera;

        public Color insideColor;
        public Color outsideColor;

        public float ringThickness = 0.02f;
        public float rayThickness = 0.01f;
        public float trailsPercentage = 0.15f;

        public float moveSpeed = 2.0f;

        public float searchRadius = 1.0f;
        public float searchRotationSpeed = 15.0f;
        public float attractorStrength = 0.5f;
        public float sphereCastRadius = 0.1f;

        //Lorenz84: https://www.dynamicmath.xyz/calculus/velfields/Lorenz84/
        public float attractorA = 0.95f;
        public float attractorB = 7.91f;
        public float attractorF = 4.83f;
        public float attractorG = 4.66f;

        public GameObject trailPrefab;

        public Gradient internalNodesGradient;

        public int initialSpheres = 8;

        public Material sphereMaterial = null;
        public Material regressionLineMaterial = null;
        public Material rayMaterial = null;
        public Material capsuleMaterial = null;
        public Material searchRingMaterial = null;
        public Material treeMaterial = null;

        public RotatingCamera rotationCam;

        public Vector2 sphereRadiusRange;
        public Vector2 sphereCastRadiusRange;
        public Vector3 maxVelocity;

        #endregion

        #region Private Variables

        private bool doOverlappingQuery = false;
        private bool doFrustumQuery = false;
        private bool doAttractorMovement = false;
        private bool doMultiQuery = false;
        private bool doRaycast = false;
        private bool doBoundsQuery = false;
        private bool doNearestNeighborQuery = false;
        private bool showQuery = true;
        private bool trailRenderersEnabled = true;

        private Dictionary<int, MeshRenderer> idToRenderer = new Dictionary<int, MeshRenderer>();
        private Dictionary<int, TrailRenderer> idToTrailRenderer = new Dictionary<int, TrailRenderer>();

        private float searchRotation = 0.0f;

        private GameObject raycastLine;
        private GameObject neighborLine;
        private GameObject sphereCastCapsule;
        private GameObject[] frustumLines;
        private GameObject searchRing;
        private GameObject searchBounds;
        private GameObject[] multiQuerySearchRings;
        private GameObject[] multiQuerySearchBounds;

        private int sphereCount = 0;

        private List<GameObject> spherePool = new List<GameObject>();

        private List<GameObject> sphereObjects = new List<GameObject>();
        private List<MeshRenderer> sphereMeshRenderer = new List<MeshRenderer>();

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private MaterialPropertyBlock internalNodeBlock;

        private NativeArray<float> multiQueryRadii;
        private NativeArray<float3> multiQueryCenters;
        private NativeArray<Bounds> multiQueryBounds;

        private Native3DBallStarTree<Sphere> ballTree;

        private NativeList<float3> childPositionBuffer;
        private NativeList<Sphere> childrenBuffer;

        private NativeArray<float3> nearestNeighborQueryPos;
        private NativeArray<Sphere> nearestNeighbor;

        private NativeList<Sphere> spheres;
        private NativeList<Sphere> querySphereResults;
        private NativeList<Sphere> frustumQuerySphereResults;
        private NativeList<Sphere> sphereCastResults;
        private NativeList<float3> velocities;

        private NativeList<IntersectionHit3D<Sphere>> intersectionHits;

        private NativeParallelHashSet<Sphere> multiQuerySphereResults;

        private RaycastType raycastType = RaycastType.LINE;

        private Sampler updateSpheresSampler = null;
        private Sampler radiusQuerySampler = null;
        private Sampler optimizeSampler = null;
        private Sampler raycastSampler = null;
        private Sampler frustumQuerySampler = null;
        private Sampler nearestNeighborSampler = null;

        private TransformAccessArray sphereAccessArray;

        private Vector3 rayStart;
        private Vector3 rayEnd;

        private Vector3 currentPos;
        private Vector3[] searchRingOffsets;

        private float3 nearestNeighborPos;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker radiusQueryMarker = new ProfilerMarker("RadiusQuery");
        private static readonly ProfilerMarker updateSpheresMarker = new ProfilerMarker("UpdateSpheres");
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

        public int GetNrOfSpheres() => this.spheres.Length;

        public RaycastType CurrentRaycastType() => this.raycastType;

        public Sampler GetUpdateSphereSampler() => this.updateSpheresSampler;
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

        public void AddRandomSpheres(int nrOfSpheres)
        {
            var min = this.bounds.min;
            var max = this.bounds.max;
            for(int i = 0; i < nrOfSpheres; i++)
            {
                float radius = UnityEngine.Random.Range(this.sphereRadiusRange.x, this.sphereRadiusRange.y);
                float rndX = UnityEngine.Random.Range(min.x + radius, max.x - radius);
                float rndY = UnityEngine.Random.Range(min.y + radius, max.y - radius);
                float rndZ = UnityEngine.Random.Range(min.z + radius, max.z - radius);

                var worldPos = new Vector3(rndX, rndY, rndZ);

                var sphere = new Sphere()
                {
                    Center = worldPos,
                    ID = this.sphereCount + i,
                    RadiusSq = radius * radius,
                };

                var sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereObj.name = $"Sphere_{this.sphereCount + i}$";
                sphereObj.transform.parent = this.transform;
                sphereObj.transform.position = worldPos;
                sphereObj.transform.localScale = new Vector3(radius, radius, radius) * 2;

                var meshRenderer = sphereObj.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = this.sphereMaterial;

                var collider = sphereObj.GetComponent<Collider>();
                if(collider != null)
                {
                    Object.Destroy(collider);
                }

                if (UnityEngine.Random.value < this.trailsPercentage)
                {
                    var trail = GameObject.Instantiate(this.trailPrefab);
                    trail.transform.parent = sphereObj.transform;
                    trail.transform.localPosition = Vector3.zero;

                    var trailRenderer = trail.GetComponent<TrailRenderer>();
                    trailRenderer.enabled = this.trailRenderersEnabled;
                    this.idToTrailRenderer.Add(sphere.ID, trailRenderer);
                }

                this.sphereObjects.Add(sphereObj);

                this.sphereMeshRenderer.Add(meshRenderer);

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);
                float velZ = UnityEngine.Random.Range(-this.maxVelocity.z, this.maxVelocity.z);

                this.spheres.Add(sphere);
                this.velocities.Add(new float3(velX, velY, velZ));

                this.sphereAccessArray.Add(sphereObj.transform);

                this.ballTree.Insert(sphere);

                this.idToRenderer.Add(sphere.ID, meshRenderer);
            }

            this.sphereCount += nrOfSpheres;
        }

        public void RemoveRandomSpheres(int nrOfSpheres)
        {
            NativeList<Sphere> spheresToRemove = new NativeList<Sphere>(Allocator.TempJob);
            for (int i = 0; i < nrOfSpheres; i++)
            {
                if (this.sphereObjects.Count <= 0) break;

                var rndSphere = UnityEngine.Random.Range(0, this.sphereObjects.Count);
                var sphere = this.spheres[rndSphere];

                var sphereObj = this.sphereObjects[rndSphere];

                this.sphereObjects.RemoveAtSwapBack(rndSphere);
                this.sphereMeshRenderer.RemoveAtSwapBack(rndSphere);
                this.sphereAccessArray.RemoveAtSwapBack(rndSphere);

                this.spheres.RemoveAtSwapBack(rndSphere);
                this.velocities.RemoveAtSwapBack(rndSphere);

                spheresToRemove.Add(sphere);

                this.idToRenderer.Remove(sphere.ID);
                if(this.idToTrailRenderer.ContainsKey(sphere.ID))
                {
                    this.idToTrailRenderer.Remove(sphere.ID);
                }

                GameObject.Destroy(sphereObj);
            }

            this.ballTree.RemoveAll(spheresToRemove).Complete();
            spheresToRemove.Dispose();
        }

        [BurstCompile]
        private struct UpdateSpheresJob : IJobParallelForTransform
        {

            public bool lorenzAttractor;

            public Bounds bounds;

            public float deltaTime;

            public float attractorA;
            public float attractorB;
            public float attractorF;
            public float attractorG;
            public float attractorStrength;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<Sphere> spheres;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<float3> velocities;


            public void Execute(int index, TransformAccess transform)
            {
                var sphere = this.spheres[index];
                var pos = sphere.Center;
                var velocity = this.velocities[index];

                float nextPosX, nextPosY, nextPosZ;
                if(this.lorenzAttractor)
                {
                    float attrX = -this.attractorA * pos.x - pos.y * pos.y - pos.z * pos.z + this.attractorA * this.attractorF;
                    float attrY = -pos.y + pos.x * pos.y - this.attractorB * pos.x * pos.z + this.attractorG;
                    float attrZ = -pos.z + this.attractorB * pos.x * pos.y + pos.x * pos.z;

                    float3 diff = new float3(attrX, attrY, attrZ) - pos;
                    diff = math.normalize(diff) * math.length(velocity) * this.deltaTime * this.attractorStrength;
                    diff += velocity * this.deltaTime * (1.0f - this.attractorStrength);

                    nextPosX = pos.x + diff.x;
                    nextPosY = pos.y + diff.y;
                    nextPosZ = pos.z + diff.z;
                } else
                {
                    nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                    nextPosY = math.mad(velocity.y, this.deltaTime, pos.y);
                    nextPosZ = math.mad(velocity.z, this.deltaTime, pos.z);
                }

                float radius = math.sqrt(sphere.RadiusSq);
                float3 min = (float3)this.bounds.min + radius;
                float3 max = (float3)this.bounds.max - radius;

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
                sphere.Center = new float3(nextPosX, nextPosY, nextPosZ);
                this.spheres[index] = sphere;

                transform.position = sphere.Center;
            }
        }


        public void SetRaycastType(RaycastType type)
        {
            this.raycastType = type;
        }

        public void EnableRaycast(bool enable)
        {
            this.doRaycast = enable;
        }

        public void EnableFrustumQuery(bool enable)
        {
            this.doFrustumQuery = enable;
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

            for(int i = 0; i < this.multiQuerySearchBounds.Length; i++)
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

        public void EnableOverlappingQuery(bool enable)
        {
            this.doOverlappingQuery = enable;
        }

        private Bounds CalculateSearchBounds()
        {
            return new Bounds(Vector3.zero, Vector3.one * this.searchRadius * 2);
        }

        public void ToggleTrailRenderers()
        {
            this.trailRenderersEnabled = !this.trailRenderersEnabled;
            foreach(var entry in this.idToTrailRenderer)
            {
                var trailRenderer = entry.Value;
                trailRenderer.enabled = this.trailRenderersEnabled;
            }
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

            for(int i = 0; i < 8; i++)
            {
                this.multiQuerySearchRings[i] = new GameObject($"Ring_{i}");
                this.multiQuerySearchRings[i].transform.parent = this.transform;

                var ringMeshFilter = this.multiQuerySearchRings[i].AddComponent<MeshFilter>();
                ringMeshFilter.mesh = searchRing;

                var ringMeshRenderer = this.multiQuerySearchRings[i].AddComponent<MeshRenderer>();
                ringMeshRenderer.material = this.searchRingMaterial;
                ringMeshRenderer.enabled = false;
            }

            for(int i = 0; i < 8; i++)
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

            this.multiQuerySphereResults = new NativeParallelHashSet<Sphere>(this.initialSpheres, Allocator.Persistent);

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

            this.sphereCastCapsule = new GameObject("Sphere Cast Capsule");
            this.sphereCastCapsule.transform.parent = this.transform;

            this.sphereCastCapsule.AddComponent<MeshFilter>();

            meshRenderer = this.sphereCastCapsule.AddComponent<MeshRenderer>();
            meshRenderer.material = this.capsuleMaterial;

            this.frustumLines = new GameObject[4];
            for(int i = 0; i < this.frustumLines.Length; i++)
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

            this.sphereAccessArray = new TransformAccessArray(this.initialSpheres);

            this.spheres = new NativeList<Sphere>(this.initialSpheres, Allocator.Persistent);
            this.querySphereResults = new NativeList<Sphere>(this.initialSpheres, Allocator.Persistent);
            this.frustumQuerySphereResults = new NativeList<Sphere>(this.initialSpheres, Allocator.Persistent);
            this.sphereCastResults = new NativeList<Sphere>(this.initialSpheres, Allocator.Persistent);
            this.velocities = new NativeList<float3>(this.initialSpheres, Allocator.Persistent);

            this.ballTree = new Native3DBallStarTree<Sphere>(this.initialSpheres, Allocator.Persistent);

            this.childPositionBuffer = new NativeList<float3>(this.ballTree.MaxChildren(), Allocator.Persistent);
            this.childrenBuffer = new NativeList<Sphere>(this.ballTree.MaxChildren(), Allocator.Persistent);

            this.intersectionHits = new NativeList<IntersectionHit3D<Sphere>>(16, Allocator.Persistent);

            this.nearestNeighbor = new NativeArray<Sphere>(1, Allocator.Persistent);
            this.nearestNeighborQueryPos = new NativeArray<float3>(1, Allocator.Persistent);

            this.CreateSearchMeshes();

            var boundsMin = this.bounds.min;
            var boundsMax = this.bounds.max;
            this.treeMaterial.SetVector("_Min", new Vector4(boundsMin.x, boundsMin.y, boundsMin.z, 0.0f));
            this.treeMaterial.SetVector("_Max", new Vector4(boundsMax.x, boundsMax.y, boundsMax.z, 0.0f));


            this.InitMultiQueries();

            this.AddRandomSpheres(this.initialSpheres);
        }

        private void FaceRingsTowardsCamera()
        {
            this.searchRing.transform.position = this.currentPos;
            this.searchRing.transform.up = -this.rotationCam.transform.forward;

            for(int i = 0; i < this.multiQuerySearchRings.Length; i++)
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

            for(int i = 0; i < this.multiQuerySearchBounds.Length; i++)
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

            for(int i = 0; i < this.multiQuerySearchRings.Length; i++)
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

            this.currentPos = Vector3.Max(this.currentPos, this.bounds.min);
            this.currentPos = Vector3.Min(this.currentPos, this.bounds.max);
        }

        private void AddSphereMeshToPool(BallStarNode3D node)
        {
            var sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            var collider = sphereObj.GetComponent<Collider>();
            if(collider != null)
            {
                Object.Destroy(collider);
            }

            sphereObj.transform.parent = this.transform;
            sphereObj.transform.position = node.Center;

            var meshRenderer = sphereObj.GetComponent<MeshRenderer>();
            meshRenderer.material = this.treeMaterial;

            this.spherePool.Add(sphereObj);
        }

        private unsafe void DrawTreeRecursion(BallStarNode3D node, ref int sphereMeshIndex, int maxHeight, int height)
        {
            if(sphereMeshIndex >= this.spherePool.Count)
            {
                this.AddSphereMeshToPool(node);
            }
            var sphere = this.spherePool[sphereMeshIndex];
            sphere.SetActive(true);
            float3 center = node.Center;
            sphere.transform.position = center;
            sphere.transform.localScale = Vector3.one * Mathf.Sqrt(node.RadiusSq) * 2;

            float heightPercentage = height / (float)(maxHeight + Mathf.Epsilon);
            Color color = this.internalNodesGradient.Evaluate(heightPercentage);
            this.internalNodeBlock.SetColor(SHADER_COLOR, color);

            var sphereRenderer = sphere.GetComponent<MeshRenderer>();
            sphereRenderer.SetPropertyBlock(this.internalNodeBlock);

            sphereMeshIndex++;

            if(node.left >= 0)
            {
                var leftNode = this.ballTree.GetNode(node.left);
                this.DrawTreeRecursion(leftNode, ref sphereMeshIndex, maxHeight, height + 1);
            }

            if(node.right >= 0)
            {
                var rightNode = this.ballTree.GetNode(node.right);
                this.DrawTreeRecursion(rightNode, ref sphereMeshIndex, maxHeight, height + 1);
            }
        }

        private void FindMaxHeightRecursion(BallStarNode3D node, ref int maxHeight, int currentHeight)
        {
            maxHeight = math.max(currentHeight + 1, maxHeight);

            if (node.left >= 0)
            {
                var leftNode = this.ballTree.GetNode(node.left);
                this.FindMaxHeightRecursion(leftNode, ref maxHeight, currentHeight + 1);
            }

            if (node.right >= 0)
            {
                var rightNode = this.ballTree.GetNode(node.right);
                this.FindMaxHeightRecursion(rightNode, ref maxHeight, currentHeight + 1);
            }
        }

        private unsafe void DrawBallTree()
        {
            int sphereMeshIndex = 0;

            var root = this.ballTree.GetRoot();
            int maxHeight = 0;
            this.FindMaxHeightRecursion(*root, ref maxHeight, 0);

            this.DrawTreeRecursion(*root, ref sphereMeshIndex, maxHeight, 0);

            for(int i = sphereMeshIndex; i < this.spherePool.Count; i++)
            {
                this.spherePool[i].SetActive(false);
            }

        }

        private void DrawNearestNeighborLine()
        {
            this.nearestNeighborPos = this.nearestNeighbor[0].Center;
            var ls = new LineSegment3D()
            {
                a = this.nearestNeighborPos,
                b = this.currentPos,
            };

            var mf = this.neighborLine.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.CreateLine(ls, this.rayThickness);
            this.neighborLine.SetActive(true);
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
                    if (this.raycastType == RaycastType.LINE)
                    {
                        var line = MeshUtil.CreateLine(lineSegment, 0.02f);

                        var meshFilter = this.raycastLine.GetComponent<MeshFilter>();
                        GameObject.Destroy(meshFilter.sharedMesh);
                        meshFilter.sharedMesh = line;

                        this.sphereCastCapsule.SetActive(false);
                        this.raycastLine.SetActive(true);
                    } else
                    {
                        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        var meshFilter = this.sphereCastCapsule.GetComponent<MeshFilter>();

                        var capsuleMeshFilter = capsule.GetComponent<MeshFilter>();
                        var capsuleMesh = capsuleMeshFilter.mesh;

                        GameObject.Destroy(meshFilter.sharedMesh);
                        GameObject.Destroy(capsule);

                        this.ScaleCapsuleMesh(ref capsuleMesh, (dir.magnitude + 2.0f * this.sphereCastRadius) * 0.5f * (0.5f / this.sphereCastRadius));
                        meshFilter.sharedMesh = capsuleMesh;
                        this.sphereCastCapsule.transform.up = dir.normalized;
                        this.sphereCastCapsule.transform.position = this.rayStart + dir * 0.5f;
                        this.sphereCastCapsule.transform.localScale = Vector3.one * this.sphereCastRadius * 2.0f;

                        this.sphereCastCapsule.SetActive(true);
                        this.raycastLine.SetActive(false);
                    }
                }
            }
        }

        private void ScaleCapsuleMesh(ref Mesh mesh, float halfLength)
        {
            var vertices = mesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].y > 0.0f)
                {
                    vertices[i].y += (halfLength - 1);
                } else
                {
                    vertices[i].y -= (halfLength - 1);
                }
            }

            mesh.SetVertices(vertices);
        }

        private void DrawFrustumQuery()
        {
            Vector3[] frustumCorners = new Vector3[4];
            this.frustumCamera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), this.bounds.size.x, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
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

        private void SetAllSphereColorsToOutside()
        {
            for(int i = 0; i < this.sphereObjects.Count; i++)
            {
                var meshRenderer = this.sphereMeshRenderer[i];
                meshRenderer.SetPropertyBlock(this.outsideMPB);
            }
        }

        private void SetRaycastColors()
        {
            if(this.doRaycast)
            {
                if (this.raycastType == RaycastType.LINE)
                {
                    for (int i = 0; i < this.intersectionHits.Length; i++)
                    {
                        var intersection = this.intersectionHits[i];
                        var sphere = intersection.boundingVolume;

                        var meshRenderer = this.idToRenderer[sphere.ID];
                        meshRenderer.SetPropertyBlock(this.insideMPB);
                    }
                } else
                {
                    for(int i = 0; i < this.sphereCastResults.Length; i++)
                    {
                        var sphere = this.sphereCastResults[i];

                        var meshRenderer = this.idToRenderer[sphere.ID];
                        meshRenderer.SetPropertyBlock(this.insideMPB);
                    }
                }
            }
        }

        private void SetFrustumQueryColors()
        {
            if(this.doFrustumQuery)
            {
                for (int i = 0; i < this.frustumQuerySphereResults.Length; i++)
                {
                    var sphere = this.frustumQuerySphereResults[i];
                    int id = sphere.ID;

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

            for(int i = 0; i < this.multiQuerySearchBounds.Length; i++)
            {
                this.multiQuerySearchBounds[i].SetActive(enable);
            }

        }

        private void SetColors()
        {
            this.SetAllSphereColorsToOutside();
            this.SetRaycastColors();
            this.SetFrustumQueryColors();

            if(this.doMultiQuery)
            {
                foreach(var sphere in this.multiQuerySphereResults)
                {
                    int id = sphere.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }

            } else
            {
                for(int i = 0; i < this.querySphereResults.Length; i++)
                {
                    var sphere = this.querySphereResults[i];
                    int id = sphere.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }



        private void Update()
        {
            this.querySphereResults.Clear();
            this.multiQuerySphereResults.Clear();

            if (this.showQuery)
            {
                this.FaceRingsTowardsCamera();
                this.UpdateSearchBoundsPosition();

                this.UpdateMultiQuerySearch();
            }

            this.nearestNeighborQueryPos[0] = this.currentPos;

            var updateSpheresJob = new UpdateSpheresJob()
            {
                deltaTime = Time.deltaTime,
                attractorA = this.attractorA,
                attractorB = this.attractorB,
                attractorF = this.attractorF,
                attractorG = this.attractorG,
                attractorStrength = this.attractorStrength,
                lorenzAttractor = this.doAttractorMovement,
                bounds = this.bounds,
                spheres = this.spheres.AsArray(),
                velocities = this.velocities.AsArray(),
            };

            var updateSpheresHandle = IJobParallelForTransformExtensions.Schedule(updateSpheresJob, this.sphereAccessArray);
            updateSpheresHandle.Complete();

            updateSpheresMarker.Begin();

            var updateAllJob = this.ballTree.UpdateAll(this.spheres);
            updateAllJob.Complete();

            updateSpheresMarker.End();

            radiusQueryMarker.Begin();

            if (this.showQuery)
            {
                if (this.doMultiQuery)
                {
                    //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                    //increased automatically in a parallel job!
                    if (this.multiQuerySphereResults.Capacity < this.GetNrOfSpheres())
                    {
                        this.multiQuerySphereResults.Capacity = this.GetNrOfSpheres();
                    }

                    if (this.doBoundsQuery)
                    {
                        if (this.doOverlappingQuery)
                        {
                            var boundsQuery = this.ballTree.GetOverlappingSpheresInMultipleBounds(this.multiQueryBounds,
                                ref this.multiQuerySphereResults, default, 1);
                            boundsQuery.Complete();
                        }
                        else
                        {
                            var boundsQuery = this.ballTree.GetSpheresInMultipleBounds(this.multiQueryBounds, ref this.multiQuerySphereResults, default, 1);
                            boundsQuery.Complete();
                        }
                    }
                    else
                    {
                        if (this.doOverlappingQuery)
                        {
                            var radiusQuery = this.ballTree.GetOverlappingSpheresInRadii(this.multiQueryCenters, this.multiQueryRadii,
                                ref this.multiQuerySphereResults, default, 1);
                            radiusQuery.Complete();
                        }
                        else
                        {
                            var radiusQuery = this.ballTree.GetSpheresInRadii(this.multiQueryCenters, this.multiQueryRadii, ref this.multiQuerySphereResults, default, 1);
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
                            var boundsQuery = this.ballTree.GetOverlappingSpheresInBounds(bounds, ref this.querySphereResults);
                            boundsQuery.Complete();
                        }
                        else
                        {
                            var boundsQuery = this.ballTree.GetSpheresInBounds(bounds, ref this.querySphereResults);
                            boundsQuery.Complete();
                        }
                    }
                    else
                    {
                        if (this.doOverlappingQuery)
                        {
                            var radiusQuery = this.ballTree.GetOverlappingSpheresInRadius(this.currentPos, this.searchRadius,
                                ref this.querySphereResults);
                            radiusQuery.Complete();
                        }
                        else
                        {
                            var radiusQuery = this.ballTree.GetSpheresInRadius(this.currentPos, this.searchRadius, ref this.querySphereResults);
                            radiusQuery.Complete();
                        }
                    }
                }
            }

            radiusQueryMarker.End();

            optimizeMarker.Begin();

            var optimizeJob = this.ballTree.Optimize(128, 64);
            optimizeJob.Complete();

            optimizeMarker.End();

            if(this.doRaycast)
            {
                raycastMarker.Begin();

                if (this.raycastType == RaycastType.LINE)
                {
                    var direction = (this.rayEnd - this.rayStart);
                    var ray = new Ray()
                    {
                        direction = direction.normalized,
                        origin = this.rayStart,
                    };
                    var raycastJob = this.ballTree.Raycast(ray, direction.magnitude, ref this.intersectionHits);
                    raycastJob.Complete();
                } else
                {
                    var capsule = new Capsule()
                    {
                        a = this.rayStart,
                        b = this.rayEnd,
                        radius = this.sphereCastRadius,
                    };
                    var raycastJob = this.ballTree.SphereCast(capsule, ref this.sphereCastResults);
                    raycastJob.Complete();
                }

                raycastMarker.End();
            }

            if(this.doFrustumQuery)
            {
                frustumMarker.Begin();

                var frustumQueryJob = this.ballTree.FrustumQuery(this.frustumCamera, ref this.frustumQuerySphereResults);
                frustumQueryJob.Complete();

                frustumMarker.End();
            }

            if(this.doNearestNeighborQuery)
            {
                nearestNeighborMarker.Begin();

                var nnQueryJob = this.ballTree.GetNearestNeighbors(this.nearestNeighborQueryPos, ref this.nearestNeighbor);
                nnQueryJob.Complete();

                nearestNeighborMarker.End();
            }

            this.SetColors();

            if (this.updateSpheresSampler == null || !this.updateSpheresSampler.isValid)
            {
                this.updateSpheresSampler = Sampler.Get("UpdateSpheres");
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

            this.DrawBallTree();
            if(this.doRaycast)
            {
                this.DrawRaycast();
            } else
            {
                this.raycastLine.SetActive(false);
                this.sphereCastCapsule.SetActive(false);
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
            this.spheres.DisposeIfCreated();
            this.querySphereResults.DisposeIfCreated();
            this.frustumQuerySphereResults.DisposeIfCreated();
            this.sphereCastResults.DisposeIfCreated();
            this.velocities.DisposeIfCreated();

            this.childPositionBuffer.DisposeIfCreated();
            this.childrenBuffer.DisposeIfCreated();

            this.multiQuerySphereResults.DisposeIfCreated();
            this.multiQueryRadii.DisposeIfCreated();
            this.multiQueryCenters.DisposeIfCreated();
            this.multiQueryBounds.DisposeIfCreated();

            this.intersectionHits.DisposeIfCreated();

            this.nearestNeighbor.DisposeIfCreated();
            this.nearestNeighborQueryPos.DisposeIfCreated();

            if (this.ballTree.IsCreated)
            {
                this.ballTree.Dispose();
            }

            if(this.sphereAccessArray.isCreated)
            {
                this.sphereAccessArray.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
