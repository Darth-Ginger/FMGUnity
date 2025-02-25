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
    public class RStar2DSystem : MonoBehaviour
    {

        #region Public Fields

        public Color insideColor;
        public Color outsideColor;

        public float searchRadius = 1.0f;
        public float ringThickness = 0.02f;
        public float outlineThickness = 0.01f;
        public float lineThickness = 0.01f;
        public float rayThickness = 0.01f;
        public float searchRotationSpeed = 15.0f;
        public float yOffset = 0.01f;

        public float attractorStrength = 0.5f;
        [Range(-3, 3)]
        public float attractorA = 1.0f;
        [Range(-3, 3)]
        public float attractorB = 1.0f;
        [Range(-3, 3)]
        public float attractorC = 1.0f;

        public Gradient internalNodesGradient;

        public int initialPoints = 8;

        public LineRenderer boundaryRenderer;

        public Material searchRingMaterial;
        public Material rectMaterial;
        public Material treeMaterial;
        public Material rayMaterial;
        public Material polygonMaterial;

        public Rect bounds;

        public Vector2 rectWidthRange;
        public Vector2 rectHeightRange;
        public Vector2 maxVelocity;

        #endregion

        #region Private Fields

        private bool doOverlappingQuery = false;
        private bool doAttractorMovement = false;
        private bool doMultiQuery = false;
        private bool doRaycast = false;
        private bool doRectQuery = false;
        private bool doPolygonQuery = false;
        private bool doNearestNeighborQuery = false;

        private Dictionary<int, MeshRenderer> idToRenderer = new Dictionary<int, MeshRenderer>();

        private float searchRotation = 0.0f;

        private GameObject mouseSearchRing;
        private GameObject mouseSearchRect;
        private GameObject mouseSearchPolygon;

        private GameObject raycastLine;
        private GameObject neighborLine;
        private GameObject[] multiQuerySearchRings;
        private GameObject[] multiQuerySearchRects;

        private int rectCount = 0;

        private List<GameObject> rectPool = new List<GameObject>();

        private List<GameObject> rectObjects = new List<GameObject>();
        private List<MeshRenderer> rectMeshRenderers = new List<MeshRenderer>();

        private MaterialPropertyBlock insideMPB;
        private MaterialPropertyBlock outsideMPB;

        private MaterialPropertyBlock internalNodeBlock;

        private NativeArray<float> multiQueryRadii;
        private NativeArray<float2> multiQueryCenters;

        private NativeArray<Rect> multiQueryRects;

        private Native2DRStarTree<RStarRect> rTree;

        private NativeList<RStarRect> rectangles;
        private NativeList<RStarRect> queryRectanglesResults;
        private NativeList<float2> velocities;

        private NativeArray<float3> nearestNeighborQueryPos;
        private NativeArray<RStarRect> nearestNeighbor;

        private NativeList<IntersectionHit2D<RStarRect>> intersectionHits;

        private NativeParallelHashSet<RStarRect> multiQueryRectangleResult;

        private NativePolygon2D searchPolygon;

        private Sampler updateRectanglesSampler = null;
        private Sampler radiusQuerySampler = null;
        private Sampler optimizeSampler = null;
        private Sampler raycastSampler = null;

        private TransformAccessArray rectanglesAcessArray;

        private Vector2 rayStart;
        private Vector2 rayEnd;

        private Vector3 mouseHitPos;
        private Vector3[] searchRingOffsets;

        private float2 nearestNeighborPos;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";

        private static readonly ProfilerMarker radiusQueryMarker = new ProfilerMarker("RadiusQuery");
        private static readonly ProfilerMarker updateRectanglesMarker = new ProfilerMarker("UpdateRectangles");
        private static readonly ProfilerMarker optimizeMarker = new ProfilerMarker("Optimize");
        private static readonly ProfilerMarker raycastMarker = new ProfilerMarker("Raycast");

        public bool IsDoingRaycast() => this.doRaycast;
        public bool IsDoingMultiQuery() => this.doMultiQuery;
        public bool IsDoingRectQuery() => this.doRectQuery;
        public bool IsDoingPolygonQuery() => this.doPolygonQuery;
        public bool IsDoingNearestNeighborQuery() => this.doNearestNeighborQuery;
        public bool IsDoingOverlappingQuery() => this.doOverlappingQuery;

        public bool IsDoingAttractorMovement() => this.doAttractorMovement;

        public int GetNrOfRectangles() => this.rectObjects.Count;

        public Sampler GetUpdateRectanglesSampler() => this.updateRectanglesSampler;
        public Sampler GetRadiusQuerySampler() => this.radiusQuerySampler;
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


        public void RemoveRandomRectangles(int nrOfCircles)
        {
            NativeList<RStarRect> rectanglesToRemove = new NativeList<RStarRect>(Allocator.TempJob);
            for (int i = 0; i < nrOfCircles; i++)
            {
                if (this.rectObjects.Count <= 0) break;

                var rndCircle = UnityEngine.Random.Range(0, this.rectObjects.Count);
                var rectangle = this.rectangles[rndCircle];

                var rectangleObj = this.rectObjects[rndCircle];

                this.rectObjects.RemoveAtSwapBack(rndCircle);
                this.rectMeshRenderers.RemoveAtSwapBack(rndCircle);
                this.rectanglesAcessArray.RemoveAtSwapBack(rndCircle);

                this.rectangles.RemoveAtSwapBack(rndCircle);
                this.velocities.RemoveAtSwapBack(rndCircle);
                //this.gumowskiMiraW.RemoveAtSwapBack(rndCircle);

                rectanglesToRemove.Add(rectangle);

                this.idToRenderer.Remove(rectangle.ID);

                GameObject.Destroy(rectangleObj);
            }

            this.rTree.RemoveAll(rectanglesToRemove).Complete();
            rectanglesToRemove.Dispose();
        }



        public void AddRandomRectangles(int nrOfRectangles)
        {

            var min = this.bounds.min;
            var max = this.bounds.max;
            for (int i = 0; i < nrOfRectangles; i++)
            {
                float width = UnityEngine.Random.Range(this.rectWidthRange.x, this.rectWidthRange.y);
                float height = UnityEngine.Random.Range(this.rectHeightRange.x, this.rectHeightRange.y);

                float rndX = UnityEngine.Random.Range(min.x, max.x - width);
                float rndZ = UnityEngine.Random.Range(min.y, max.y - height);

                var worldPos = new Vector3(rndX, this.yOffset, rndZ);

                var rect = new RStarRect()
                {
                    Bounds = new Rect(rndX, rndZ, width, height),
                    ID = this.rectCount + i,
                };

                var rectObj = new GameObject($"Rectangle_{this.rectCount + i}$");
                rectObj.transform.parent = this.transform;
                rectObj.transform.position = worldPos;

                this.rectObjects.Add(rectObj);

                var meshRenderer = rectObj.AddComponent<MeshRenderer>();
                var meshFilter = rectObj.AddComponent<MeshFilter>();

                this.rectMeshRenderers.Add(meshRenderer);

                var outlineRect = new Rect(0, 0, width, height);
                var rectMesh = MeshUtil.CreateRectangleOutline(outlineRect, this.outlineThickness);

                meshFilter.sharedMesh = rectMesh;
                meshRenderer.sharedMaterial = this.rectMaterial;

                float velX = UnityEngine.Random.Range(-this.maxVelocity.x, this.maxVelocity.x);
                float velY = UnityEngine.Random.Range(-this.maxVelocity.y, this.maxVelocity.y);

                this.rectangles.Add(rect);
                this.velocities.Add(new float2(velX, velY));
                //this.gumowskiMiraW.Add(0.0f);

                this.rectanglesAcessArray.Add(rectObj.transform);

                this.rTree.Insert(rect);

                this.idToRenderer.Add(rect.ID, meshRenderer);
            }

            this.rectCount += nrOfRectangles;
        }

        public void UpdateMouseSearchRingRadius()
        {
            var newSearchRing = MeshUtil.CreateRing(this.searchRadius, this.ringThickness);

            var meshFilter = this.mouseSearchRing.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = newSearchRing;

            var newMultiQueryRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.ringThickness);

            for(int i = 0; i < this.multiQuerySearchRings.Length; i++)
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
            var newMultiQueryRect = MeshUtil.CreateRectangleOutline(multiQueryRect, this.ringThickness);

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

            for (int i = 0; i < 4; i++)
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


            this.multiQueryRectangleResult = new NativeParallelHashSet<RStarRect>(this.initialPoints, Allocator.Persistent);

            this.multiQueryRadii = new NativeArray<float>(this.multiQuerySearchRings.Length, Allocator.Persistent);
            this.multiQueryCenters = new NativeArray<float2>(this.multiQuerySearchRings.Length, Allocator.Persistent);

            this.multiQueryRects = new NativeArray<Rect>(this.multiQuerySearchRects.Length, Allocator.Persistent);
        }

        private Mesh CreateInternalNodeMesh()
        {
            var rectOutline = MeshUtil.CreateRectangleOutline(new Rect(0, 0, 1, 1), this.outlineThickness);

            return rectOutline;
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

        private void Start()
        {
            this.insideMPB = new MaterialPropertyBlock();
            this.outsideMPB = new MaterialPropertyBlock();
            this.internalNodeBlock = new MaterialPropertyBlock();

            this.insideMPB.SetColor(SHADER_COLOR, this.insideColor);
            this.outsideMPB.SetColor(SHADER_COLOR, this.outsideColor);

            this.InitBoundaryRenderer();

            this.rectanglesAcessArray = new TransformAccessArray(this.initialPoints);

            this.rectangles = new NativeList<RStarRect>(this.initialPoints, Allocator.Persistent);
            this.queryRectanglesResults = new NativeList<RStarRect>(this.initialPoints, Allocator.Persistent);
            this.velocities = new NativeList<float2>(this.initialPoints, Allocator.Persistent);

            this.rTree = new Native2DRStarTree<RStarRect>(this.initialPoints, Allocator.Persistent);

            this.intersectionHits = new NativeList<IntersectionHit2D<RStarRect>>(16, Allocator.Persistent);

            this.nearestNeighbor = new NativeArray<RStarRect>(1, Allocator.Persistent);
            this.nearestNeighborQueryPos = new NativeArray<float3>(1, Allocator.Persistent);

            this.CreateSearchMeshes();

            var boundsMin = this.bounds.min;
            var boundsMax = this.bounds.max;
            this.treeMaterial.SetVector("_Min", new Vector4(boundsMin.x, -1.0f, boundsMin.y, 0.0f));
            this.treeMaterial.SetVector("_Max", new Vector4(boundsMax.x, 1.0f, boundsMax.y, 0.0f));

            this.InitMultiQueries();

            this.AddRandomRectangles(this.initialPoints);
        }

        [BurstCompile]
        private struct UpdateRectanglesJob : IJobParallelForTransform
        {

            public bool doAttractor;

            public Rect bounds;

            public float deltaTime;
            public float yOffset;

            public float attractorStrength;
            public float attractorA;
            public float attractorB;
            public float attractorC;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<RStarRect> rectangles;

            [NoAlias, NativeDisableParallelForRestriction]
            public NativeList<float2> velocities;

            public void Execute(int index, TransformAccess transform)
            {
                var rectangle = this.rectangles[index];
                var pos = rectangle.Bounds.min;
                var velocity = this.velocities[index];

                float nextPosX, nextPosY;
                
                if (this.doAttractor)
                {
                    nextPosX = pos.y - 1.0f - math.sqrt(math.abs(this.attractorB * pos.x - 1.0f - this.attractorC)) * math.sign(pos.x - 1.0f);
                    nextPosY = this.attractorA - pos.x - 1.0f;

                    //Moving with the same velocity as before
                    float2 diff = new float2(nextPosX, nextPosY) - (float2)pos;
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

                float width = rectangle.Bounds.width;
                float height = rectangle.Bounds.height; 
                float xMax = this.bounds.xMax - width;
                float xMin = this.bounds.xMin;
                float yMax = this.bounds.yMax - height;
                float yMin = this.bounds.yMin;

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
                var bounds = rectangle.Bounds;
                bounds.min = new float2(nextPosX, nextPosY);
                bounds.max = (float2)bounds.min + new float2(width, height);
                rectangle.Bounds = bounds;
                this.rectangles[index] = rectangle;

                transform.position = new float3(nextPosX, this.yOffset, nextPosY);
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

        private void AddRectangleMeshToPool(RStarNode2D node)
        {
            var mesh = this.CreateInternalNodeMesh();

            var rectangleObj = new GameObject($"R*Tree2D_Rectangle");
            rectangleObj.transform.parent = this.transform;
            rectangleObj.transform.position = new Vector3(node.Bounds.xMin, 0.0f, node.Bounds.yMin);

            var meshRenderer = rectangleObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = this.treeMaterial;

            var meshFilter = rectangleObj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            this.rectPool.Add(rectangleObj);
        }

        private unsafe void DrawTreeRecursion(RStarNode2D node, ref int rectMeshIndex, int maxHeight, int height)
        {
            if (rectMeshIndex >= this.rectPool.Count)
            {
                this.AddRectangleMeshToPool(node);
            }
            var rectangle = this.rectPool[rectMeshIndex];
            rectangle.SetActive(true);

            rectangle.transform.position = new Vector3(node.Bounds.xMin, this.yOffset * height, node.Bounds.yMin);
            rectangle.transform.localScale = new Vector3(node.Bounds.width, 1.0f, node.Bounds.height);

            float heightPercentage = height / (float)(maxHeight + Mathf.Epsilon);
            Color color = this.internalNodesGradient.Evaluate(heightPercentage);
            this.internalNodeBlock.SetColor(SHADER_COLOR, color);

            var rectMeshRenderer = rectangle.GetComponent<MeshRenderer>();
            rectMeshRenderer.SetPropertyBlock(this.internalNodeBlock);

            rectMeshIndex++;

            if (node.left >= 0)
            {
                var leftNode = this.rTree.GetNode(node.left);
                this.DrawTreeRecursion(leftNode, ref rectMeshIndex, maxHeight, height + 1);
            }

            if (node.right >= 0)
            {
                var rightNode = this.rTree.GetNode(node.right);
                this.DrawTreeRecursion(rightNode, ref rectMeshIndex, maxHeight, height + 1);
            }
        }

        private void FindMaxHeightRecursion(RStarNode2D node, ref int maxHeight, int currentHeight)
        {
            maxHeight = math.max(currentHeight + 1, maxHeight);

            if(node.left >= 0)
            {
                var leftNode = this.rTree.GetNode(node.left);
                this.FindMaxHeightRecursion(leftNode, ref maxHeight, currentHeight + 1);
            }

            if(node.right >= 0)
            {
                var rightNode = this.rTree.GetNode(node.right);
                this.FindMaxHeightRecursion(rightNode, ref maxHeight, currentHeight + 1);
            }
        }

        private unsafe void DrawRTree()
        {
            int rectMeshIndex = 0;

            var root = this.rTree.GetRoot();
            int maxHeight = 0;
            this.FindMaxHeightRecursion(*root, ref maxHeight, 0);

            this.DrawTreeRecursion(*root, ref rectMeshIndex, maxHeight, 0);

            for(int i = rectMeshIndex; i < this.rectPool.Count; i++)
            {
                this.rectPool[i].SetActive(false);
            }
        }

        private void DrawRaycast()
        {
            if(this.rayStart != this.rayEnd)
            {
                var ls = new LineSegment2D()
                {
                    a = this.rayStart,
                    b = this.rayEnd,
                };

                var line = MeshUtil.CreateLine(ls, 0.05f);

                var meshFilter = this.raycastLine.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = line;
            }
        }

        private void DrawNearestNeighborLine()
        {
            this.nearestNeighborPos = this.nearestNeighbor[0].Bounds.center;
            var ls = new LineSegment2D()
            {
                a = new float2(this.nearestNeighborPos.x, this.nearestNeighborPos.y),
                b = new float2(this.mouseHitPos.x, this.mouseHitPos.z)
            };

            var mf = this.neighborLine.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.CreateLine(ls, this.lineThickness);
        }

        private void SetAllRectangleColorsToOutside()
        {
            for(int i = 0; i < this.rectObjects.Count; i++)
            {
                var meshRenderer = this.rectMeshRenderers[i];
                meshRenderer.SetPropertyBlock(this.outsideMPB);
            }
        }

        private void SetRaycastColors()
        {
            if(this.doRaycast)
            {
                for(int i = 0; i < this.intersectionHits.Length; i++)
                {
                    var intersection = this.intersectionHits[i];
                    var rect = intersection.boundingArea;

                    var meshRenderer = this.idToRenderer[rect.ID];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }

        private void SetColors()
        {
            this.SetAllRectangleColorsToOutside();
            this.SetRaycastColors();

            if (!this.doMultiQuery || this.doPolygonQuery)
            {

                for (int i = 0; i < this.queryRectanglesResults.Length; i++)
                {
                    var rect = this.queryRectanglesResults[i];
                    int id = rect.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
            else
            {
                foreach (var rect in this.multiQueryRectangleResult)
                {
                    int id = rect.ID;

                    var meshRenderer = this.idToRenderer[id];
                    meshRenderer.SetPropertyBlock(this.insideMPB);
                }
            }
        }

        private void Update()
        {
            this.GetMouseHitPos();

            this.nearestNeighborQueryPos[0] = this.mouseHitPos;

            this.searchRotation += Time.deltaTime * this.searchRotationSpeed;
            if(this.doRectQuery)
            {
                this.UpdateSearchRectangles();
            }
            else if(this.doPolygonQuery)
            {
                this.UpdateSearchPolygon();
            } else
            {
                this.UpdateSearchRings();
            }

            var updateRectangles = new UpdateRectanglesJob()
            {
                deltaTime = Time.deltaTime,
                bounds = this.bounds,
                rectangles = this.rectangles,
                velocities = this.velocities,
                yOffset = this.yOffset,
                attractorA = this.attractorA,
                attractorB = this.attractorB,
                attractorC = this.attractorC,
                attractorStrength = this.attractorStrength,
                doAttractor = this.doAttractorMovement
            };

            var updateRectanglesHandle = IJobParallelForTransformExtensions.Schedule(updateRectangles, this.rectanglesAcessArray);
            updateRectanglesHandle.Complete();

            updateRectanglesMarker.Begin();

            var updateAllJob = this.rTree.UpdateAll(this.rectangles);
            updateAllJob.Complete();

            updateRectanglesMarker.End();

            optimizeMarker.Begin();

            var optimizeJob = this.rTree.Optimize();
            optimizeJob.Complete();

            optimizeMarker.End();

            radiusQueryMarker.Begin();

            if(this.doPolygonQuery)
            {
                var flatPos = new Vector3();
                flatPos.x = this.mouseHitPos.x;
                flatPos.z = this.mouseHitPos.z;

                var polygonQuery = this.rTree.GetRectanglesInPolygon(this.searchPolygon,
                    Matrix4x4.TRS(flatPos, Quaternion.AngleAxis(this.searchRotation, Vector3.back), Vector3.one),
                    ref this.queryRectanglesResults);
                polygonQuery.Complete();
            
            }
            else if(this.doNearestNeighborQuery)
            {
                var nnQuery = this.rTree.GetNearestNeighbors(this.nearestNeighborQueryPos, ref this.nearestNeighbor);
                nnQuery.Complete();
            }
            else
            {
                if(this.doMultiQuery)
                {
                    //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                    //increased automatically in a parallel job!
                    if(this.multiQueryRectangleResult.Capacity < this.GetNrOfRectangles())
                    {
                        this.multiQueryRectangleResult.Capacity = this.GetNrOfRectangles();
                    }

                    if(this.doRectQuery)
                    {

                        if(this.doOverlappingQuery)
                        {
                            var rectQuery = this.rTree.GetOverlappingRectanglesInRectangles(this.multiQueryRects, ref this.multiQueryRectangleResult);
                            rectQuery.Complete();

                        } else
                        {
                            var rectQuery = this.rTree.GetRectanglesInRectangles(this.multiQueryRects, ref this.multiQueryRectangleResult);
                            rectQuery.Complete();
                        }

                    } else
                    {

                        if (this.doOverlappingQuery) 
                        {
                            var radiusQuery = this.rTree.GetOverlappingRectanglesInRadii(this.multiQueryCenters, this.multiQueryRadii,
                                ref this.multiQueryRectangleResult);
                            radiusQuery.Complete();

                        } else
                        {
                            var radiusQuery = this.rTree.GetRectanglesInRadii(this.multiQueryCenters, this.multiQueryRadii,
                                ref this.multiQueryRectangleResult);
                            radiusQuery.Complete(); 
                        }
                    }
                } else
                {
                    if (this.doRectQuery)
                    {

                        var rect = this.CalculateSearchRect();
                        var offset = new float2(this.mouseHitPos.x, this.mouseHitPos.z);
                        rect.center += (Vector2)offset;

                        if(this.doOverlappingQuery)
                        {
                            var rectQuery = this.rTree.GetOverlappingRectanglesInRectangle(rect, ref this.queryRectanglesResults);
                            rectQuery.Complete();
                        } else
                        {
                            var rectQuery = this.rTree.GetRectanglesInRectangle(rect, ref this.queryRectanglesResults);
                            rectQuery.Complete();
                        }

                    } else
                    {
                        if(this.doOverlappingQuery)
                        {
                            var radiusQuery = this.rTree.GetOverlappingRectanglesInRadius(new float2(this.mouseHitPos.x, this.mouseHitPos.z), this.searchRadius,
                                ref this.queryRectanglesResults);
                            radiusQuery.Complete();

                        } else
                        {
                            var radiusQuery = this.rTree.GetRectanglesInRadius(new float2(this.mouseHitPos.x, this.mouseHitPos.z), this.searchRadius,
                                ref this.queryRectanglesResults);
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
                    direction = direction.normalized,
                };

                var raycastJob = this.rTree.Raycast(ray, direction.magnitude, ref this.intersectionHits);
                raycastJob.Complete();

                raycastMarker.End();
            }

            this.SetColors();

            if (this.updateRectanglesSampler == null || !this.updateRectanglesSampler.isValid)
            {
                this.updateRectanglesSampler = Sampler.Get("UpdateRectangles");
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

            this.DrawRTree();
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
            this.rectangles.DisposeIfCreated();
            this.queryRectanglesResults.DisposeIfCreated();
            this.velocities.DisposeIfCreated();

            this.multiQueryRectangleResult.DisposeIfCreated();
            this.multiQueryRadii.DisposeIfCreated();
            this.multiQueryCenters.DisposeIfCreated();
            this.multiQueryRects.DisposeIfCreated();

            this.intersectionHits.DisposeIfCreated();

            this.nearestNeighbor.DisposeIfCreated();
            this.nearestNeighborQueryPos.DisposeIfCreated();    

            if(this.rTree.IsCreated)
            {
                this.rTree.Dispose();
            }

            if(this.rectanglesAcessArray.isCreated)
            {
                this.rectanglesAcessArray.Dispose();
            }

            this.searchPolygon.Dispose();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
