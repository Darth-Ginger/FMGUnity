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
    public class BallStar2DSystem : MonoBehaviour
    {

        #region Public Variables


        public Color insideColor;
        public Color outsideColor;

        public float searchRadius = 1.0f;
        public float ringThickness = 0.02f;
        public float circleThickness = 0.01f;
        public float lineThickness = 0.01f;
        public float rayThickness = 0.01f;
        public float searchRotationSpeed = 15.0f;
        public float yOffset = 0.01f;
        public float trailsPercentage = 0.15f;

        public float attractorStrength = 0.5f;
        [Range(-1, 1)]
        public float attractorA = 0.06268f;
        [Range(-1, 1)]
        public float attractorB = 0.43671f;

        public GameObject trailPrefab;

        public Gradient internalNodesGradient;

        public int initialPoints = 8;

        public LineRenderer boundaryRenderer;

        public Material searchRingMaterial;
        public Material circleMaterial;
        public Material regressionLineMaterial;
        public Material treeMaterial;
        public Material rayMaterial;
        public Material polygonMaterial;

        public Rect bounds;

        public Vector2 circleRadiusRange;
        public Vector2 maxVelocity;

        #endregion

        #region Private Variables

        private bool doOverlappingQuery = false;
        private bool doAttractorMovement = false;
        private bool doMultiQuery = false;
        private bool doNearestNeighborQuery = false;
        private bool doRaycast = false;
        private bool drawRegressionLines = false;
        private bool trailRenderersEnabled = true;
        private bool doRectQuery = false;
        private bool doPolygonQuery = false;

        private Dictionary<int, MeshRenderer> idToRenderer = new Dictionary<int, MeshRenderer>();
        private Dictionary<int, TrailRenderer> idToTrailRenderer = new Dictionary<int, TrailRenderer>();

        private float searchRotation = 0.0f;
        private float innerRingRadius = 0.0f;

        private GameObject mouseSearchRing;
        private GameObject mouseSearchRect;
        private GameObject mouseSearchPolygon;

        private GameObject raycastLine;
        private GameObject neighborLine;
        private GameObject[] multiQuerySearchRings;
        private GameObject[] multiQuerySearchRects;

        private int circleCount = 0;

        private List<GameObject> rLinePool = new List<GameObject>();
        private List<GameObject> circlePool = new List<GameObject>();

        private List<GameObject> circleObjects = new List<GameObject>();
        private List<MeshRenderer> circleMeshRenderer = new List<MeshRenderer>();

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private MaterialPropertyBlock internalNodeBlock;

        private NativeArray<float> multiQueryRadii;
        private NativeArray<float2> multiQueryCenters;

        private NativeArray<Rect> multiQueryRects;

        private Native2DBallStarTree<Circle> ballTree;

        private NativeList<float2> childPositionBuffer;
        private NativeList<Circle> childrenBuffer;

        private NativeArray<float3> nearestNeighborQueryPos;
        private NativeArray<Circle> nearestNeighbor;

        private NativeList<Circle> circles;
        private NativeList<Circle> queryCircleResults;
        private NativeList<float2> velocities;
        private NativeList<float> gumowskiMiraW;

        private NativeList<IntersectionHit2D<Circle>> intersectionHits;

        private NativeParallelHashSet<Circle> multiQueryCircleResults;

        private NativePolygon2D searchPolygon;

        private Sampler updateCirclesSampler = null;
        private Sampler radiusQuerySampler = null;
        private Sampler optimizeSampler = null;
        private Sampler raycastSampler = null;

        private TransformAccessArray circleAccessArray;

        private Vector2 rayStart;
        private Vector2 rayEnd;

        private Vector3 mouseHitPos;
        private Vector3[] searchRingOffsets;

        private float2 nearestNeighborPos;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker radiusQueryMarker = new ProfilerMarker("RadiusQuery");
        private static readonly ProfilerMarker updateCirclesMarker = new ProfilerMarker("UpdateCircles");
        private static readonly ProfilerMarker optimizeMarker = new ProfilerMarker("Optimize");
        private static readonly ProfilerMarker raycastMarker = new ProfilerMarker("Raycast");

        public bool IsDoingRaycast() => this.doRaycast;
        public bool IsDoingMultiQuery() => this.doMultiQuery;
        public bool IsDoingRectQuery() => this.doRectQuery;
        public bool IsDoingPolygonQuery() => this.doPolygonQuery;
        public bool IsDoingNearestNeighborQuery() => this.doNearestNeighborQuery;
        public bool IsDrawingRegressionLines() => this.drawRegressionLines;
        public bool IsDoingOverlappingQuery() => this.doOverlappingQuery;

        public bool IsDoingAttractorMovement() => this.doAttractorMovement;

        public int GetNrOfCircles() => this.circleObjects.Count;

        public Sampler GetUpdateCirclesSampler() => this.updateCirclesSampler;
        public Sampler GetQuerySampler() => this.radiusQuerySampler;
        public Sampler GetOptimizeSampler() => this.optimizeSampler;
        public Sampler GetRaycastSampler() => this.raycastSampler;


        public void SetRaycastParameters(Vector2 rayStart, Vector2 rayEnd)
        {
            this.rayStart = rayStart;
            this.rayEnd = rayEnd;
        }

        public void EnableRaycast(bool enable)
        {
            this.doRaycast = enable;
        }

        public void EnableOverlappingQuery(bool enable)
        {
            this.doOverlappingQuery = enable;
        }

        public unsafe void EnableAttractor(bool enable)
        {
            this.doAttractorMovement = enable;
            if(this.doAttractorMovement)
            {
                UnsafeUtility.MemSet(this.gumowskiMiraW.GetUnsafePtr(), 0, this.gumowskiMiraW.Length * sizeof(float));
            }


        }

        private void HandleSearchMeshesState()
        {
            var mr = this.mouseSearchRing.GetComponent<MeshRenderer>();
            mr.enabled = !this.doRectQuery && !this.doMultiQuery && !this.doPolygonQuery && !this.doNearestNeighborQuery;

            mr = this.mouseSearchRect.GetComponent<MeshRenderer>();
            mr.enabled = this.doRectQuery && !this.doMultiQuery && !this.doPolygonQuery && !this.doNearestNeighborQuery;

            mr = this.mouseSearchPolygon.GetComponent<MeshRenderer>();
            mr.enabled = this.doPolygonQuery && !this.doNearestNeighborQuery;

            mr = this.neighborLine.GetComponent<MeshRenderer>();
            mr.enabled = this.doNearestNeighborQuery;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                mr = this.multiQuerySearchRings[i].GetComponent<MeshRenderer>();
                mr.enabled = !this.doRectQuery && this.doMultiQuery && !this.doPolygonQuery && !this.doNearestNeighborQuery;
            }

            for (int i = 0; i < this.multiQuerySearchRects.Length; i++)
            {
                mr = this.multiQuerySearchRects[i].GetComponent<MeshRenderer>();
                mr.enabled = this.doRectQuery && this.doMultiQuery && !this.doPolygonQuery && !this.doNearestNeighborQuery;
            }
        }

        public void EnableNearestNeighborQuery(bool enable)
        {
            this.doNearestNeighborQuery = enable;

            this.HandleSearchMeshesState();
        }

        public void EnablePolygonQuery(bool enable)
        {
            this.doPolygonQuery = enable;

            this.HandleSearchMeshesState();
        }

        public void EnableRectQuery(bool enable)
        {
            this.doRectQuery = enable;

            this.HandleSearchMeshesState();
        }

        public void EnableMultiQuery(bool enable)
        {
            this.doMultiQuery = enable;

            this.HandleSearchMeshesState();
        }



        public void DrawRegressionLines(bool enable)
        {
            this.drawRegressionLines = enable;
        }

        public void RemoveRandomCircles(int nrOfCircles)
        {
            NativeList<Circle> circlesToRemove = new NativeList<Circle>(Allocator.TempJob);
            for(int i = 0; i < nrOfCircles; i++)
            {
                if (this.circleObjects.Count <= 0) break;

                var rndCircle = UnityEngine.Random.Range(0, this.circleObjects.Count);
                var circle = this.circles[rndCircle];

                var circleObj = this.circleObjects[rndCircle];

                this.circleObjects.RemoveAtSwapBack(rndCircle);
                this.circleMeshRenderer.RemoveAtSwapBack(rndCircle);
                this.circleAccessArray.RemoveAtSwapBack(rndCircle);

                this.circles.RemoveAtSwapBack(rndCircle);
                this.velocities.RemoveAtSwapBack(rndCircle);
                this.gumowskiMiraW.RemoveAtSwapBack(rndCircle);

                circlesToRemove.Add(circle);

                this.idToRenderer.Remove(circle.ID);
                if(this.idToTrailRenderer.ContainsKey(circle.ID))
                {
                    this.idToTrailRenderer.Remove(circle.ID);
                }

                GameObject.Destroy(circleObj);
            }

            this.ballTree.RemoveAll(circlesToRemove).Complete();
            circlesToRemove.Dispose();
        }

        public void AddRandomCircles(int nrOfCircles)
        {

            var min = this.bounds.min;
            var max = this.bounds.max;
            for(int i = 0; i < nrOfCircles; i++)
            {
                float radius = UnityEngine.Random.Range(this.circleRadiusRange.x, this.circleRadiusRange.y);
                float rndX = UnityEngine.Random.Range(min.x + radius, max.x - radius);
                float rndZ = UnityEngine.Random.Range(min.y + radius, max.y - radius);

                var worldPos = new Vector3(rndX, this.yOffset, rndZ);

                var circle = new Circle()
                {
                    Center = new float2(worldPos.x, worldPos.z),
                    RadiusSq = radius * radius,
                    ID = this.circleCount + i,
                };

                var circleObj = new GameObject($"Circle_{this.circleCount + i}$");
                circleObj.transform.parent = this.transform;
                circleObj.transform.position = worldPos;

                if(UnityEngine.Random.value < this.trailsPercentage)
                {
                    var trail = GameObject.Instantiate(this.trailPrefab);
                    trail.transform.parent = circleObj.transform;
                    trail.transform.localPosition = Vector3.zero;

                    var trailRenderer = trail.GetComponent<TrailRenderer>();
                    trailRenderer.enabled = this.trailRenderersEnabled;
                    this.idToTrailRenderer.Add(circle.ID, trailRenderer);
                }

                this.circleObjects.Add(circleObj);

                var meshRenderer = circleObj.AddComponent<MeshRenderer>();
                var meshFilter = circleObj.AddComponent<MeshFilter>();

                this.circleMeshRenderer.Add(meshRenderer);

                var circleMesh = this.CreateMeshFromCircle(circle);

                meshFilter.sharedMesh = circleMesh;
                meshRenderer.sharedMaterial = this.circleMaterial;

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);

                this.circles.Add(circle);
                this.velocities.Add(new float2(velX, velY));
                this.gumowskiMiraW.Add(0.0f);

                this.circleAccessArray.Add(circleObj.transform);

                this.ballTree.Insert(circle);

                this.idToRenderer.Add(circle.ID, meshRenderer);
            }

            this.circleCount += nrOfCircles;
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

        public void UpdateMouseSearchRingRadius()
        {
            var newSearchRing = MeshUtil.CreateRing(this.searchRadius, this.ringThickness);

            var meshFilter = this.mouseSearchRing.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = newSearchRing;

            var newMultiQueryRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.ringThickness);

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var ring = this.multiQuerySearchRings[i];
                meshFilter = ring.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = newMultiQueryRing;
                this.searchRingOffsets[i] = this.searchRingOffsets[i].normalized * this.searchRadius;
            }
        }

        private Rect CalculateSearchRect()
        {
            return new Rect(-this.searchRadius, -this.searchRadius, 2.0f * this.searchRadius, 2.0f * this.searchRadius);
        }

        public void UpdateMouseSearchRectSize()
        {
            var rect = this.CalculateSearchRect();
            var newSearchRect = MeshUtil.CreateRectangleOutline(rect, this.ringThickness);

            var meshFilter = this.mouseSearchRect.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = newSearchRect;

            var multiQueryRect = new Rect(rect.xMin * 0.5f, rect.yMin * 0.5f, rect.width * 0.5f, rect.height * 0.5f);
            var newMultiQueryRect= MeshUtil.CreateRectangleOutline(multiQueryRect, this.ringThickness);

            for (int i = 0; i < this.multiQuerySearchRects.Length; i++)
            {
                var searchRect = this.multiQuerySearchRects[i];
                meshFilter = searchRect.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = newMultiQueryRect;
                this.searchRingOffsets[i] = this.searchRingOffsets[i].normalized * this.searchRadius;
            }
        }

        public void UpdateMouseSearchPolygonSize()
        {
            this.searchPolygon.Dispose();
            this.searchPolygon = Polygon2DGeneration.Star(Allocator.Persistent, 5, Vector2.zero, this.searchRadius * 0.3f, this.searchRadius);

            var triangulation = new NativeList<int>(Allocator.TempJob);
            Polygon2DTriangulation.EarClippingTriangulationJob(this.searchPolygon, ref triangulation).Complete();
            var searchPolygonMesh = MeshUtil.CreatePolygonMesh(this.searchPolygon, triangulation.AsArray());

            var meshFilter = this.mouseSearchPolygon.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = searchPolygonMesh;

            triangulation.Dispose();
        }

        private void InitMultiQueries()
        {
            this.multiQuerySearchRings = new GameObject[4];
            this.multiQuerySearchRects = new GameObject[4];
            this.searchRingOffsets = new Vector3[4];

            var searchRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.ringThickness);

            var rect = this.CalculateSearchRect();
            var multiQueryRect = new Rect(rect.xMin * 0.5f, rect.yMin * 0.5f, rect.width * 0.5f, rect.height * 0.5f);
            var searchRect = MeshUtil.CreateRectangleOutline(multiQueryRect, this.ringThickness);

            for (int i = 0; i < 4; i++)
            {
                this.multiQuerySearchRings[i] = new GameObject($"Ring_{i}");
                this.multiQuerySearchRings[i].transform.parent = this.transform;

                var ringMeshFilter = this.multiQuerySearchRings[i].AddComponent<MeshFilter>();
                ringMeshFilter.mesh = searchRing;

                var ringMeshRenderer = this.multiQuerySearchRings[i].AddComponent<MeshRenderer>();
                ringMeshRenderer.material = this.searchRingMaterial;
                ringMeshRenderer.enabled = false;
            }

            for(int i = 0; i < 4; i++)
            {
                this.multiQuerySearchRects[i] = new GameObject($"Rect_{i}");
                this.multiQuerySearchRects[i].transform.parent = this.transform;

                var rectMeshFilter = this.multiQuerySearchRects[i].AddComponent<MeshFilter>();
                rectMeshFilter.mesh = searchRect;

                var rectMeshRenderer = this.multiQuerySearchRects[i].AddComponent<MeshRenderer>();
                rectMeshRenderer.material = this.searchRingMaterial;
                rectMeshRenderer.enabled = false;
            }

            this.searchRingOffsets[0] = new Vector3(this.searchRadius, 0.0f, 0.0f);
            this.searchRingOffsets[1] = new Vector3(0.0f, 0.0f, -this.searchRadius);
            this.searchRingOffsets[2] = new Vector3(-this.searchRadius, 0.0f, 0.0f);
            this.searchRingOffsets[3] = new Vector3(0.0f, 0.0f, this.searchRadius);

            this.multiQuerySearchRings[0].transform.position = this.transform.position + this.searchRingOffsets[0];
            this.multiQuerySearchRings[1].transform.position = this.transform.position + this.searchRingOffsets[1];
            this.multiQuerySearchRings[2].transform.position = this.transform.position + this.searchRingOffsets[2];
            this.multiQuerySearchRings[3].transform.position = this.transform.position + this.searchRingOffsets[3];

            this.multiQuerySearchRects[0].transform.position = this.transform.position + this.searchRingOffsets[0];
            this.multiQuerySearchRects[1].transform.position = this.transform.position + this.searchRingOffsets[1];
            this.multiQuerySearchRects[2].transform.position = this.transform.position + this.searchRingOffsets[2];
            this.multiQuerySearchRects[3].transform.position = this.transform.position + this.searchRingOffsets[3];


            this.multiQueryCircleResults = new NativeParallelHashSet<Circle>(this.initialPoints, Allocator.Persistent);

            this.multiQueryRadii = new NativeArray<float>(this.multiQuerySearchRings.Length, Allocator.Persistent);
            this.multiQueryCenters = new NativeArray<float2>(this.multiQuerySearchRings.Length, Allocator.Persistent);

            this.multiQueryRects = new NativeArray<Rect>(this.multiQuerySearchRects.Length, Allocator.Persistent);
        }

        private Mesh CreateInternalNodeMesh()
        {
            var outerRing = MeshUtil.CreateRing(1.0f, this.lineThickness, 64);

            return outerRing;
        }

        private Mesh CreateMeshFromCircle(Circle circle)
        {
            float radius = Mathf.Sqrt(circle.RadiusSq);

            var outerRing = MeshUtil.CreateRing(radius, this.lineThickness);
            var innerRing = MeshUtil.CreateRing(this.innerRingRadius, this.lineThickness, 16);

            float angle = UnityEngine.Random.value * Constants.TAU;

            float2 lineDir = new float2(Mathf.Cos(angle), Mathf.Sin(angle));
            float2 perpDir = new float2(-lineDir.y, lineDir.x);

            var segmentA = new LineSegment2D()
            {
                a = - lineDir * radius,
                b = + lineDir * radius,
            };

            var segmentB = new LineSegment2D()
            {
                a = - perpDir * radius,
                b = + perpDir * radius
            };

            var lineA = MeshUtil.CreateLine(segmentA, this.lineThickness);
            var lineB = MeshUtil.CreateLine(segmentB, this.lineThickness);

            var combineInstances = new CombineInstance[4];
            combineInstances[0].mesh = outerRing;
            combineInstances[1].mesh = innerRing;
            combineInstances[2].mesh = lineA;
            combineInstances[3].mesh = lineB;

            var circleMesh = new Mesh();
            circleMesh.CombineMeshes(combineInstances, true, false);

            return circleMesh;
        }

        private void InitBoundaryRenderer()
        {
            this.boundaryRenderer.positionCount = 5;

            var min = this.bounds.min;
            var max = this.bounds.max;

            this.boundaryRenderer.SetPositions(new Vector3[]
            {
                new Vector3(min.x, 0.0f, min.y),
                new Vector3(max.x, 0.0f, min.y),
                new Vector3(max.x, 0.0f, max.y),
                new Vector3(min.x, 0.0f, max.y),
                new Vector3(min.x, 0.0f, min.y)
            });
        }

        private void CreateSearchMeshes()
        {

            this.mouseSearchRing = new GameObject("Mouse Search Ring");
            this.mouseSearchRing.transform.parent = this.transform;

            var searchRing = MeshUtil.CreateRing(this.searchRadius, this.ringThickness);

            var meshFilter = this.mouseSearchRing.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchRing;

            var meshRenderer = this.mouseSearchRing.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;




            this.mouseSearchRect = new GameObject("Mouse Search Rect");
            this.mouseSearchRect.transform.parent = this.transform;

            var searchRect = MeshUtil.CreateRectangleOutline(this.CalculateSearchRect(), this.ringThickness);

            meshFilter = this.mouseSearchRect.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchRect;

            meshRenderer = this.mouseSearchRect.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;
            meshRenderer.enabled = false;



            this.mouseSearchPolygon = new GameObject("Mouse Search Polygon");
            this.mouseSearchPolygon.transform.parent = this.transform;

            this.searchPolygon = Polygon2DGeneration.Star(Allocator.Persistent, 5, Vector2.zero, this.searchRadius * 0.3f, this.searchRadius);

            var triangulation = new NativeList<int>(Allocator.TempJob);
            Polygon2DTriangulation.EarClippingTriangulationJob(this.searchPolygon, ref triangulation).Complete();
            var searchPolygonMesh = MeshUtil.CreatePolygonMesh(this.searchPolygon, triangulation.AsArray());

            meshFilter = this.mouseSearchPolygon.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchPolygonMesh;

            meshRenderer = this.mouseSearchPolygon.AddComponent<MeshRenderer>();
            meshRenderer.material = this.polygonMaterial;
            meshRenderer.enabled = false;

            triangulation.Dispose();




            this.raycastLine = new GameObject("Raycast Line");
            this.raycastLine.transform.parent = this.transform;

            this.raycastLine.AddComponent<MeshFilter>();

            meshRenderer = this.raycastLine.AddComponent<MeshRenderer>();
            meshRenderer.material = this.rayMaterial;


            this.neighborLine = new GameObject("Neighbor Line");
            this.neighborLine.transform.parent = this.transform;

            var line = MeshUtil.CreateLine(new LineSegment2D() { a = new float2(0, 0), b = new float2(1, 1) }, this.lineThickness);

            meshFilter = this.neighborLine.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = line;

            meshRenderer = this.neighborLine.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;
            meshRenderer.enabled = false;


        }

        void Start()
        {
            this.innerRingRadius = this.circleRadiusRange.x * 0.5f;

            this.insideMPB = new MaterialPropertyBlock();
            this.outsideMPB = new MaterialPropertyBlock();
            this.internalNodeBlock = new MaterialPropertyBlock();

            this.insideMPB.SetColor(SHADER_COLOR, this.insideColor);
            this.outsideMPB.SetColor(SHADER_COLOR, this.outsideColor);

            this.InitBoundaryRenderer();

            this.circleAccessArray = new TransformAccessArray(this.initialPoints);

            this.circles = new NativeList<Circle>(this.initialPoints, Allocator.Persistent);
            this.queryCircleResults = new NativeList<Circle>(this.initialPoints, Allocator.Persistent);
            this.velocities = new NativeList<float2>(this.initialPoints, Allocator.Persistent);

            this.ballTree = new Native2DBallStarTree<Circle>(this.initialPoints, Allocator.Persistent);

            this.childPositionBuffer = new NativeList<float2>(this.ballTree.MaxChildren(), Allocator.Persistent);
            this.childrenBuffer = new NativeList<Circle>(this.ballTree.MaxChildren(), Allocator.Persistent);

            this.intersectionHits = new NativeList<IntersectionHit2D<Circle>>(16, Allocator.Persistent);

            this.gumowskiMiraW = new NativeList<float>(this.initialPoints, Allocator.Persistent);

            this.nearestNeighbor = new NativeArray<Circle>(1, Allocator.Persistent);
            this.nearestNeighborQueryPos = new NativeArray<float3>(1, Allocator.Persistent);

            this.CreateSearchMeshes();

            var boundsMin = this.bounds.min;
            var boundsMax = this.bounds.max;
            this.treeMaterial.SetVector("_Min", new Vector4(boundsMin.x, -1.0f, boundsMin.y, 0.0f));
            this.treeMaterial.SetVector("_Max", new Vector4(boundsMax.x, 1.0f, boundsMax.y, 0.0f));

            this.InitMultiQueries();

            this.AddRandomCircles(this.initialPoints);

        }

        [BurstCompile]
        private struct UpdateCirclesJob : IJobParallelForTransform
        {
            //https://paulbourke.net/fractals/GumowskiMira/
            //https://softologyblog.wordpress.com/2017/03/04/2d-strange-attractors/
            public bool gumowskiMiraAttractor;

            public Rect bounds;

            public float deltaTime;
            public float yOffset;

            public float attractorA;
            public float attractorB;
            public float attractorStrength;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<Circle> circles;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float2> velocities;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float> attractorW;

            public void Execute(int index, TransformAccess transform)
            {
                var circle = this.circles[index];
                var pos = circle.Center;
                var velocity = this.velocities[index];

                float nextPosX, nextPosY;
                if (this.gumowskiMiraAttractor)
                {
                    nextPosX = this.attractorB * pos.y + this.attractorW[index];
                    this.attractorW[index] = this.attractorA * pos.x + (((1.0f - this.attractorA) * 2.0f * pos.x * pos.x) / (1.0f + pos.x * pos.x));
                    nextPosY = this.attractorW[index] - pos.x;

                    //Moving with the same velocity as before
                    float2 diff = new float2(nextPosX, nextPosY) - pos;
                    diff = math.normalize(diff) * math.length(velocity) * this.deltaTime * this.attractorStrength;
                    diff += velocity * this.deltaTime * (1.0f - this.attractorStrength);
                    
                    nextPosX = pos.x + diff.x;
                    nextPosY = pos.y + diff.y;
                }
                else
                {
                    nextPosX = math.mad(velocity.x, this.deltaTime, pos.x);
                    nextPosY = math.mad(velocity.y, this.deltaTime, pos.y);
                }

                float radius = math.sqrt(circle.RadiusSq);
                float xMax = this.bounds.xMax - radius;
                float xMin = this.bounds.xMin + radius;
                float yMax = this.bounds.yMax - radius;
                float yMin = this.bounds.yMin + radius;

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
                circle.Center = new float2(nextPosX, nextPosY);
                this.circles[index] = circle;

                transform.position = new float3(circle.Center.x, this.yOffset, circle.Center.y);
            }
            
        }

        private void GetMouseHitPos()
        {
            var mousePos = Input.mousePosition;

            var ray = Camera.main.ScreenPointToRay(mousePos);

            var plane = new Plane(Vector3.up, Vector3.zero);

            if (plane.Raycast(ray, out float distance))
            {
                this.mouseHitPos = ray.origin + ray.direction * distance;
            }
        }


        private void UpdateSearchRectangles()
        {
            this.mouseSearchRect.transform.position = this.mouseHitPos;


            for (int i = 0; i < this.multiQuerySearchRects.Length; i++)
            {
                var offset = Quaternion.AngleAxis(this.searchRotation, Vector3.up) * this.searchRingOffsets[i];
                var pos = this.mouseHitPos + offset;

                var rect = this.CalculateSearchRect();
                rect.width *= 0.5f;
                rect.height *= 0.5f;
                rect.center = new float2(pos.x, pos.z);

                this.multiQuerySearchRects[i].transform.position = pos;
                this.multiQueryRects[i] = rect;
            }
        }

        private void UpdateSearchRings()
        {
            this.mouseSearchRing.transform.position = this.mouseHitPos;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var offset = Quaternion.AngleAxis(this.searchRotation, Vector3.up) * this.searchRingOffsets[i];
                var pos = this.mouseHitPos + offset;

                this.multiQuerySearchRings[i].transform.position = pos;

                this.multiQueryRadii[i] = this.searchRadius * 0.5f;
                this.multiQueryCenters[i] = new float2(pos.x, pos.z);
            }
        }

        private void UpdateSearchPolygon()
        {
            this.mouseSearchPolygon.transform.position = this.mouseHitPos;
            this.mouseSearchPolygon.transform.rotation = Quaternion.AngleAxis(this.searchRotation, Vector3.up);
        }

        private unsafe void AddLineMeshToPool()
        {
            var lineSegment = new LineSegment2D()
            {
                a = float2.zero,
                b = new float2(1.0f, 0.0f)
            };

            var mesh = MeshUtil.CreateLine(lineSegment, this.lineThickness);

            var lineObj = new GameObject($"Ball*Tree2D_RegressionLine");
            lineObj.transform.parent = this.transform;
            lineObj.transform.position = Vector3.zero;

            var meshRenderer = lineObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = this.regressionLineMaterial;

            var meshFilter = lineObj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            this.rLinePool.Add(lineObj);
        }

        private void AddCircleMeshToPool(BallStarNode2D node)
        {
            var mesh = this.CreateInternalNodeMesh();

            var circleObj = new GameObject($"Ball*Tree2D_Circle");
            circleObj.transform.parent = this.transform;
            circleObj.transform.position = new Vector3(node.Center.x, 0.0f, node.Center.y);

            var meshRenderer = circleObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = this.treeMaterial;

            var meshFilter = circleObj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            this.circlePool.Add(circleObj);
        }

        private void DrawNearestNeighborLine()
        {
            this.nearestNeighborPos = this.nearestNeighbor[0].Center;
            var ls = new LineSegment2D()
            {
                a = new float2(this.nearestNeighborPos.x, this.nearestNeighborPos.y),
                b = new float2(this.mouseHitPos.x, this.mouseHitPos.z)
            };

            var mf = this.neighborLine.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.CreateLine(ls, this.lineThickness);
        }

        //A little bit slow, as the tree is not made for these things
        //But it is only for drawing things, so it is fine
        private unsafe void DrawRegressionLine(ref int poolIndex, BallStarNode2D node)
        {
            var line = this.rLinePool[poolIndex];

            if (node.children >= 0)
            {
                this.ballTree.GetLeafChildren(node, ref this.childrenBuffer);
                if (this.childrenBuffer.Length > 0)
                {
                    this.childPositionBuffer.Length = this.childrenBuffer.Length;

                    //Center average is wrong if not repartitioned just now
                    float2 actualAverage = float2.zero;
                    for (int i = 0; i < this.childPositionBuffer.Length; i++)
                    {
                        this.childPositionBuffer[i] = this.childrenBuffer[i].Center;
                        actualAverage += this.childPositionBuffer[i];
                    }
                    actualAverage /= this.childPositionBuffer.Length;

                    var regressionLine = StatisticsUtil.EstimateRegressionLine2D(actualAverage, this.childPositionBuffer.AsArray());
                    float radius = math.sqrt(node.RadiusSq);

                    var dir = regressionLine.direction;
                    float2 start = new float2(actualAverage.x, actualAverage.x * dir.y);

                    line.transform.position = new Vector3(start.x, this.yOffset, start.y);
                    line.transform.rotation = Quaternion.LookRotation(new Vector3(-dir.y, 0.0f, dir.x), Vector3.up);
                    line.transform.localScale = new Vector3(radius, 1.0f, 1.0f);
                    line.SetActive(true);

                    poolIndex++;
                }
            }
        }

        private unsafe void DrawTreeRecursion(BallStarNode2D node, ref int circleMeshIndex, ref int lineMeshIndex, int maxHeight, int height)
        {
            if (circleMeshIndex >= this.circlePool.Count)
            {
                this.AddCircleMeshToPool(node);
            }
            var circle = this.circlePool[circleMeshIndex];
            circle.SetActive(true);
            float2 center = node.Center;
            circle.transform.position = new Vector3(center.x, this.yOffset * height, center.y);
            circle.transform.localScale = Vector3.one * Mathf.Sqrt(node.RadiusSq);

            float heightPercentage = height / (float)(maxHeight + Mathf.Epsilon);
            Color color = this.internalNodesGradient.Evaluate(heightPercentage);
            this.internalNodeBlock.SetColor(SHADER_COLOR, color);

            var circleMeshRenderer = circle.GetComponent<MeshRenderer>();
            circleMeshRenderer.SetPropertyBlock(this.internalNodeBlock);

            if (lineMeshIndex >= this.rLinePool.Count)
            {
                this.AddLineMeshToPool();
            }

            if (this.drawRegressionLines)
            {
                this.DrawRegressionLine(ref lineMeshIndex, node);
            }
            circleMeshIndex++;

            if(node.left >= 0)
            {
                var leftNode = this.ballTree.GetNode(node.left);
                this.DrawTreeRecursion(leftNode, ref circleMeshIndex, ref lineMeshIndex, maxHeight, height + 1);
            }

            if(node.right >= 0)
            {
                var rightNode = this.ballTree.GetNode(node.right);
                this.DrawTreeRecursion(rightNode, ref circleMeshIndex, ref lineMeshIndex, maxHeight, height + 1);
            }
        }

        private void FindMaxHeightRecursion(BallStarNode2D node, ref int maxHeight, int currentHeight)
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
            int circleMeshIndex = 0;
            int lineMeshIndex = 0;

            var root = this.ballTree.GetRoot();
            int maxHeight = 0;
            this.FindMaxHeightRecursion(*root, ref maxHeight, 0);

            this.DrawTreeRecursion(*root, ref circleMeshIndex, ref lineMeshIndex, maxHeight, 0);

            for (int i = circleMeshIndex; i < this.circlePool.Count; i++)
            {
                this.circlePool[i].SetActive(false);
            }

            for (int i = lineMeshIndex; i < this.rLinePool.Count; i++)
            {
                this.rLinePool[i].SetActive(false);
            }
        }

        private void DrawRaycast()
        {
            if (this.rayStart != this.rayEnd)
            {
                var lineSegment = new LineSegment2D()
                {
                    a = this.rayStart,
                    b = this.rayEnd
                };
                var line = MeshUtil.CreateLine(lineSegment, 0.05f);

                var meshFilter = this.raycastLine.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = line;
            }
        }

        private void SetAllCircleColorsToOutside()
        {
            for(int i = 0; i < this.circleObjects.Count; i++)
            {
                var meshRenderer = this.circleMeshRenderer[i];
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
                    var circle = intersection.boundingArea;

                    var meshRenderer = this.idToRenderer[circle.ID];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }

        private void SetColors()
        {
            this.SetAllCircleColorsToOutside();
            this.SetRaycastColors();

            if (!this.doMultiQuery || this.doPolygonQuery)
            {

                for (int i = 0; i < this.queryCircleResults.Length; i++)
                {
                    var circle = this.queryCircleResults[i];
                    int id = circle.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
            else
            {
                foreach(var circle in this.multiQueryCircleResults)
                {
                    int id = circle.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }

        }

        void Update()
        {
            this.GetMouseHitPos();

            this.nearestNeighborQueryPos[0] = this.mouseHitPos;
            this.searchRotation += Time.deltaTime * this.searchRotationSpeed;
            if (this.doRectQuery)
            {
                this.UpdateSearchRectangles();
            }
            else if(this.doPolygonQuery)
            {
                this.UpdateSearchPolygon();
            }
            else {
                this.UpdateSearchRings();
            }

            var updateCirclesJob = new UpdateCirclesJob()
            {
                deltaTime = Time.deltaTime,
                bounds = this.bounds,
                circles = this.circles,
                velocities = this.velocities,
                yOffset = this.yOffset,
                attractorA = this.attractorA,
                attractorB = this.attractorB,
                attractorW = this.gumowskiMiraW,
                gumowskiMiraAttractor = this.doAttractorMovement,
                attractorStrength = this.attractorStrength
            };

            var updateCirclesHandle = IJobParallelForTransformExtensions.Schedule(updateCirclesJob, this.circleAccessArray);
            updateCirclesHandle.Complete();

            updateCirclesMarker.Begin();

            var updateAllJob = this.ballTree.UpdateAll(this.circles);
            updateAllJob.Complete();
            

            updateCirclesMarker.End();

            optimizeMarker.Begin();

            var optimizeJob = this.ballTree.Optimize();
            optimizeJob.Complete();

            optimizeMarker.End();

            radiusQueryMarker.Begin();

            if (this.doPolygonQuery)
            {
                var flatPos = new Vector3();
                flatPos.x = this.mouseHitPos.x;
                flatPos.y = this.mouseHitPos.z;

                var polygonQuery = this.ballTree.GetCirclesInPolygon(this.searchPolygon,
                    Matrix4x4.TRS(flatPos, Quaternion.AngleAxis(this.searchRotation, Vector3.back), Vector3.one),
                    ref this.queryCircleResults);
                polygonQuery.Complete();
            }
            else if(this.doNearestNeighborQuery)
            {
                var nnQuery = this.ballTree.GetNearestNeighbors(this.nearestNeighborQueryPos, ref this.nearestNeighbor);
                nnQuery.Complete();
            }
            else
            {

                if (this.doMultiQuery)
                {
                    //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                    //increased automatically in a parallel job!
                    if (this.multiQueryCircleResults.Capacity < this.GetNrOfCircles())
                    {
                        this.multiQueryCircleResults.Capacity = this.GetNrOfCircles();
                    }

                    if (this.doRectQuery)
                    {
                        if (this.doOverlappingQuery)
                        {
                            var rectQuery = this.ballTree.GetOverlappingCirclesInRectangles(this.multiQueryRects, ref this.multiQueryCircleResults, default, 1);
                            rectQuery.Complete();
                        }
                        else
                        {
                            var rectQuery = this.ballTree.GetCirclesInRectangles(this.multiQueryRects, ref this.multiQueryCircleResults, default, 1);
                            rectQuery.Complete();
                        }
                    }
                    else
                    {
                        if (this.doOverlappingQuery)
                        {
                            var radiusQuery = this.ballTree.GetOverlappingCirclesInRadii(this.multiQueryCenters, this.multiQueryRadii,
                                ref this.multiQueryCircleResults, default, 1);
                            radiusQuery.Complete(); 
                        }
                        else
                        {
                            var radiusQuery = this.ballTree.GetCirclesInRadii(this.multiQueryCenters, this.multiQueryRadii, ref this.multiQueryCircleResults, default, 1);
                            radiusQuery.Complete();
                        }
                    }
                }
                else
                {
                    if (this.doRectQuery)
                    {
                        var rect = this.CalculateSearchRect();
                        var offset = new float2(this.mouseHitPos.x, this.mouseHitPos.z);
                        rect.center += (Vector2)offset;

                        if (this.doOverlappingQuery)
                        {
                            var rectQuery = this.ballTree.GetOverlappingCirclesInRectangle(rect, ref this.queryCircleResults);
                            rectQuery.Complete();
                        }
                        else
                        {
                            var rectQuery = this.ballTree.GetCirclesInRectangle(rect, ref this.queryCircleResults);
                            rectQuery.Complete();
                        }
                    }
                    else
                    {
                        if (this.doOverlappingQuery)
                        {
                            var radiusQuery = this.ballTree.GetOverlappingCirclesInRadius(new float2(this.mouseHitPos.x, this.mouseHitPos.z), this.searchRadius,
                                ref this.queryCircleResults);
                            radiusQuery.Complete();
                        }
                        else
                        {
                            var radiusQuery = this.ballTree.GetCirclesInRadius(new float2(this.mouseHitPos.x, this.mouseHitPos.z), this.searchRadius,
                                ref this.queryCircleResults);
                            radiusQuery.Complete();
                        }
                    }
                }
            }

            radiusQueryMarker.End();

            if(this.doRaycast)
            {
                raycastMarker.Begin();

                var direction = (this.rayEnd - this.rayStart);
                var ray = new Ray2D()
                {
                    origin = this.rayStart,
                    direction = direction.normalized
                };
                var raycastJob = this.ballTree.Raycast(ray, direction.magnitude, ref this.intersectionHits);
                raycastJob.Complete();

                raycastMarker.End();
            }

            this.SetColors();

            if(this.updateCirclesSampler == null || !this.updateCirclesSampler.isValid)
            {
                this.updateCirclesSampler = Sampler.Get("UpdateCircles");
            }

            if(this.radiusQuerySampler == null || !this.radiusQuerySampler.isValid)
            {
                this.radiusQuerySampler = Sampler.Get("RadiusQuery");
            }

            if(this.optimizeSampler == null || !this.optimizeSampler.isValid)
            {
                this.optimizeSampler = Sampler.Get("Optimize");
            }

            if(this.raycastSampler == null || !this.raycastSampler.isValid)
            {
                this.raycastSampler = Sampler.Get("Raycast");
            }

            this.DrawBallTree();
            if(this.doRaycast)
            {
                this.raycastLine.SetActive(true);
                this.DrawRaycast();
            } else
            {
                this.raycastLine.SetActive(false);
            }

            if(this.doNearestNeighborQuery)
            {
                this.DrawNearestNeighborLine();
            }
        }

        private void Dispose()
        {
            this.circles.DisposeIfCreated();
            this.queryCircleResults.DisposeIfCreated();
            this.velocities.DisposeIfCreated();

            this.childPositionBuffer.DisposeIfCreated();
            this.childrenBuffer.DisposeIfCreated();

            this.multiQueryCircleResults.DisposeIfCreated();
            this.multiQueryRadii.DisposeIfCreated();
            this.multiQueryCenters.DisposeIfCreated();
            this.multiQueryRects.DisposeIfCreated();

            this.intersectionHits.DisposeIfCreated();

            this.gumowskiMiraW.DisposeIfCreated();

            this.nearestNeighbor.DisposeIfCreated();
            this.nearestNeighborQueryPos.DisposeIfCreated();

            if (this.ballTree.IsCreated)
            {
                this.ballTree.Dispose();
            }

            if(this.circleAccessArray.isCreated)
            {
                this.circleAccessArray.Dispose();
            }

            this.searchPolygon.Dispose();
        }


        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
