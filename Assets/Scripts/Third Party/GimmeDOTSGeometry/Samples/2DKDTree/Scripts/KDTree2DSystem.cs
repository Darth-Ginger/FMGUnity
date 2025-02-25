using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class KDTree2DSystem : MonoBehaviour
    {

        #region Public Variables

        public float searchRadius = 1.0f;
        public float ringRadius = 0.1f;
        public float ringThickness = 0.02f;
        public float lineThickness = 0.005f;
        public float searchRotationSpeed = 10.0f;

        public int nrOfPoints = 1000;

        public Material lineMaterial = null;
        public Material ringMaterial = null;
        public Material searchRingMaterial = null;
        public Material polygonMaterial = null;

        public Vector2 min;
        public Vector2 max;

        #endregion

        #region Private Variables

        private bool doPolygonQuery = false;
        private bool doMultiQuery = false;
        private bool doNearestNeighborQuery = false;

        private Camera mainCamera = null;

        private float searchRotation = 0.0f;
        private float3 nearestNeighborPos;

        private GameObject mouseSearchRing;
        private GameObject mouseSearchPolygon;
        private GameObject[] multiQuerySearchRings;

        private List<GameObject> rings = new List<GameObject>();
        private GameObject neighborLine;

        private NativeArray<Position3D> points;
        private NativeArray<float3> multiQueryCenters;
        private NativeArray<float> multiQueryRadii;

        private NativeArray<float3> nearestNeighborQueryPos;
        private NativeArray<Position3D> nearestNeighbor;

        private NativeParallelHashSet<Position3D> multiQuerySearchResults;
        private NativeList<Position3D> searchResults;
        private NativePolygon2D searchPolygon;

        private Native2DKDTree<Position3D> kdTree;

        private static readonly ProfilerMarker kdTreeMarker = new ProfilerMarker("KDTreeSearch");

        private Sampler kdTreeSampler = null;

        private Vector3 mouseHitPos;
        private Vector3[] searchRingOffsets;

        #endregion

        public bool IsDoingPolygonQuery() => this.doPolygonQuery;

        public bool IsDoingMultiQuery() => this.doMultiQuery;
        public bool IsDoingNearestNeighborQuery() => this.doNearestNeighborQuery;
        public Sampler GetKDTreeSampler() => this.kdTreeSampler;

        public void EnableNearestNeighborQuery(bool enable)
        {
            this.doNearestNeighborQuery = enable;

            var mr = this.mouseSearchRing.GetComponent<MeshRenderer>();
            mr.enabled = !this.doNearestNeighborQuery;

            mr = this.mouseSearchPolygon.GetComponent<MeshRenderer>();
            mr.enabled = !this.doNearestNeighborQuery;

            mr = this.neighborLine.GetComponent<MeshRenderer>();
            mr.enabled = this.doNearestNeighborQuery;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                mr = this.multiQuerySearchRings[i].GetComponent<MeshRenderer>();
                mr.enabled = !this.doNearestNeighborQuery;
            }


        }

        public void EnablePolygonQuery(bool enable)
        {
            this.doPolygonQuery = enable;

            var mr = this.mouseSearchRing.GetComponent<MeshRenderer>();
            mr.enabled = !this.doPolygonQuery;

            mr = this.mouseSearchPolygon.GetComponent<MeshRenderer>();
            mr.enabled = this.doPolygonQuery;


            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                mr = this.multiQuerySearchRings[i].GetComponent<MeshRenderer>();
                mr.enabled = !this.doPolygonQuery;
            }
        }

        public void EnableMultiQuery(bool enable)
        {
            this.doMultiQuery = enable;

            var mr = this.mouseSearchRing.GetComponent<MeshRenderer>();
            mr.enabled = !this.doMultiQuery;




            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                mr = this.multiQuerySearchRings[i].GetComponent<MeshRenderer>();
                mr.enabled = this.doMultiQuery;
            }
        }

        private void CreatePoints()
        {
            this.points = new NativeArray<Position3D>(this.nrOfPoints, Allocator.Persistent);

            for(int i = 0; i < this.nrOfPoints; i++)
            {
                var position =  float3.zero;

                position.x = UnityEngine.Random.Range(this.min.x, this.max.x);
                position.z = UnityEngine.Random.Range(this.min.y, this.max.y);

                this.points[i] = new Position3D(position);
            }
        }


        private void CreateRings()
        {
            var ringMesh = MeshUtil.CreateRing(this.ringRadius, this.ringThickness * 0.25f);

            for(int i = 0; i < this.points.Length; i++)
            {
                var ringObj = new GameObject($"Ring_{i}");
                ringObj.SetActive(false);
                ringObj.transform.parent = this.transform;

                var meshFilter = ringObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = ringMesh;

                var meshRenderer = ringObj.AddComponent<MeshRenderer>();
                meshRenderer.material = this.ringMaterial;

                this.rings.Add(ringObj);
            }

        }

        private unsafe void CreateKDTreeLinesRecursion(int currentNodeIdx, Rect currentBounds, int depth)
        {
            Vector2 start = Vector2.zero;
            Vector2 end = Vector2.zero;

            float splitPlane;

            var nodes = this.kdTree.GetNodes();
            var currentNode = nodes[currentNodeIdx];

            if (depth % 2 == 0)
            {
                start.x = currentNode.Position.x;
                start.y = currentBounds.yMin;

                end.x = currentNode.Position.x;
                end.y = currentBounds.yMax;

                splitPlane = currentNode.Position.x;

            } else
            {
                start.x = currentBounds.xMin;
                start.y = currentNode.Position.z;

                end.x = currentBounds.xMax;
                end.y = currentNode.Position.z;

                splitPlane = currentNode.Position.z;
            }

            var segment = new LineSegment2D()
            {
                a = start,
                b = end,
            };

            var lineMesh = MeshUtil.CreateLine(segment, this.lineThickness);

            var lineObject = new GameObject();

            var meshFilter = lineObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = lineMesh;

            var meshRenderer = lineObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = this.lineMaterial;

            lineObject.transform.parent = this.transform;

            var bounds0 = currentBounds;
            var bounds1 = currentBounds;

            if (depth % 2 == 0)
            {
                bounds0.xMax = splitPlane;
                bounds1.xMin = splitPlane;
            }
            else
            {
                bounds0.yMax = splitPlane;
                bounds1.yMin = splitPlane;
            }

            int left = currentNodeIdx * 2 + 1;
            int right = currentNodeIdx * 2 + 2;
            if (left < nodes.Length)
            {
                this.CreateKDTreeLinesRecursion(left, bounds0, depth + 1);
            }

            if(right < nodes.Length)
            {
                this.CreateKDTreeLinesRecursion(right, bounds1, depth + 1);
            }
        }

        private unsafe void CreateKDTreeLines()
        {

            var bounds = this.kdTree.GetBounds();
            this.CreateKDTreeLinesRecursion(0, bounds, 0);
            
        }

        private void InitMultiQueries()
        {
            this.multiQuerySearchRings = new GameObject[4];
            this.searchRingOffsets = new Vector3[4];

            var searchRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.ringThickness);

            for(int i = 0; i < 4; i++)
            {
                this.multiQuerySearchRings[i] = new GameObject($"Ring_{i}");
                this.multiQuerySearchRings[i].transform.parent = this.transform;

                var ringMeshFilter = this.multiQuerySearchRings[i].AddComponent<MeshFilter>();
                ringMeshFilter.mesh = searchRing;

                var ringMeshRenderer = this.multiQuerySearchRings[i].AddComponent<MeshRenderer>();
                ringMeshRenderer.material = this.searchRingMaterial;
                ringMeshRenderer.enabled = false;
            }

            this.searchRingOffsets[0] = new Vector3(this.searchRadius, 0.0f, 0.0f);
            this.searchRingOffsets[1] = new Vector3(0.0f, 0.0f, -this.searchRadius );
            this.searchRingOffsets[2] = new Vector3(-this.searchRadius, 0.0f, 0.0f);
            this.searchRingOffsets[3] = new Vector3(0.0f, 0.0f, this.searchRadius);

            this.multiQuerySearchRings[0].transform.position = this.transform.position + this.searchRingOffsets[0];
            this.multiQuerySearchRings[1].transform.position = this.transform.position + this.searchRingOffsets[1];
            this.multiQuerySearchRings[2].transform.position = this.transform.position + this.searchRingOffsets[2];
            this.multiQuerySearchRings[3].transform.position = this.transform.position + this.searchRingOffsets[3];

            this.multiQuerySearchResults = new NativeParallelHashSet<Position3D>(this.nrOfPoints, Allocator.Persistent);

            this.multiQueryRadii = new NativeArray<float>(this.multiQuerySearchRings.Length, Allocator.Persistent);
            this.multiQueryCenters = new NativeArray<float3>(this.multiQuerySearchRings.Length, Allocator.Persistent);

        }

        void Start()
        {
            this.CreatePoints();
            this.CreateRings();

            this.searchResults = new NativeList<Position3D>(Allocator.Persistent);

            this.mainCamera = Object.FindAnyObjectByType<Camera>();
            this.kdTree = new Native2DKDTree<Position3D>(this.points, CardinalPlane.XZ, Allocator.Persistent);

            this.CreateKDTreeLines();

            this.mouseSearchRing = new GameObject("Mouse Search Ring");
            this.mouseSearchRing.transform.parent = this.transform;

            this.mouseSearchPolygon = new GameObject("Mouse Search Polygon");
            this.mouseSearchPolygon.transform.parent = this.transform;

            this.neighborLine = new GameObject("Neighbor Line");
            this.neighborLine.transform.parent = this.transform;


            this.InitMultiQueries();

            var searchRing = MeshUtil.CreateRing(this.searchRadius, this.ringThickness);

            var meshFilter = this.mouseSearchRing.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchRing;

            var meshRenderer = this.mouseSearchRing.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;

            this.searchPolygon = Polygon2DGeneration.Star(Allocator.Persistent, 5, Vector2.zero, this.searchRadius * 0.3f, this.searchRadius);

            var triangulation = new NativeList<int>(Allocator.TempJob);
            Polygon2DTriangulation.EarClippingTriangulationJob(this.searchPolygon, ref triangulation).Complete();
            var searchPolygonMesh = MeshUtil.CreatePolygonMesh(this.searchPolygon, triangulation.AsArray());

            meshFilter = this.mouseSearchPolygon.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchPolygonMesh;

            meshRenderer = this.mouseSearchPolygon.AddComponent<MeshRenderer>();
            meshRenderer.material = this.polygonMaterial;
            meshRenderer.enabled = false;

            var line = MeshUtil.CreateLine(new LineSegment2D() { a = new float2(0, 0), b = new float2(1, 1) }, this.ringThickness);

            meshFilter = this.neighborLine.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = line;

            meshRenderer = this.neighborLine.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;
            meshRenderer.enabled = false;

            this.nearestNeighbor = new NativeArray<Position3D>(1, Allocator.Persistent);
            this.nearestNeighborQueryPos = new NativeArray<float3>(1, Allocator.Persistent);

            triangulation.Dispose();
        }

        private void GetMouseHitPos()
        {
            var mousePos = Input.mousePosition;

            var ray = this.mainCamera.ScreenPointToRay(mousePos);

            var plane = new Plane(Vector3.up, Vector3.zero);

            if(plane.Raycast(ray, out float distance))
            {
                this.mouseHitPos = ray.origin + ray.direction * distance;
            }
        }



        public void UpdateMouseSearchPolygon()
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

        private void DeactivateAllRings()
        {
            for (int i = 0; i < this.points.Length; i++)
            {
                this.rings[i].SetActive(false);
            }
        }

        private void UpdateSearchRings()
        {
            this.mouseSearchPolygon.transform.position = this.mouseHitPos + Vector3.down * 0.02f;
            this.mouseSearchPolygon.transform.rotation = Quaternion.AngleAxis(this.searchRotation, Vector3.up);

            this.mouseSearchRing.transform.position = this.mouseHitPos;

            this.searchRotation += Time.deltaTime * this.searchRotationSpeed;

            for(int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var offset = Quaternion.AngleAxis(this.searchRotation, Vector3.up) * this.searchRingOffsets[i];
                var pos = this.mouseHitPos + offset;

                this.multiQuerySearchRings[i].transform.position = pos;

                this.multiQueryRadii[i] = this.searchRadius * 0.5f;
                this.multiQueryCenters[i] = pos;
            }
        }

        private void ActivateRingsInSearchRadii()
        {
            if (!this.doMultiQuery || this.doPolygonQuery)
            {
                for (int i = 0; i < this.searchResults.Length; i++)
                {
                    var ring = this.rings[i];

                    var position = this.searchResults[i];

                    ring.transform.position = position.Position;
                    ring.SetActive(true);
                }
            }
            else
            {
                int counter = 0;
                foreach(var position in this.multiQuerySearchResults)
                {
                    var ring = this.rings[counter];

                    ring.transform.position = position.Position;
                    ring.SetActive(true);

                    counter++;
                }
            }

        }

        private void DrawNearestNeighborLine()
        {
            this.nearestNeighborPos = this.nearestNeighbor[0].Position;
            var ls = new LineSegment2D()
            {
                a = new float2(this.nearestNeighborPos.x, this.nearestNeighborPos.z),
                b = new float2(this.mouseHitPos.x, this.mouseHitPos.z),
            };


            var mf = this.neighborLine.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.CreateLine(ls, this.ringThickness);
        }

        void Update()
        {
            this.GetMouseHitPos();
            this.UpdateSearchRings();

            this.searchResults.Clear();
            this.multiQuerySearchResults.Clear();

            this.nearestNeighborQueryPos[0] = this.mouseHitPos;

            kdTreeMarker.Begin();

            if(this.doNearestNeighborQuery)
            {
                var job = this.kdTree.GetNearestNeighbors(this.nearestNeighborQueryPos, ref this.nearestNeighbor);
                job.Complete();
            }
            else if (this.doPolygonQuery)
            {

                var job = this.kdTree.GetPointsInPolygon(this.searchPolygon, Matrix4x4.TRS(this.mouseHitPos, Quaternion.AngleAxis(this.searchRotation, Vector3.up), Vector3.one), ref this.searchResults);
                job.Complete();
            }
            else
            {


                if (this.doMultiQuery)
                {
                    //Important: You have to ensure that the query results can hold enough data, because capacity can not be 
                    //increased automatically in a parallel job!
                    if (this.multiQuerySearchResults.Capacity < this.nrOfPoints)
                    {
                        this.multiQuerySearchResults.Capacity = this.nrOfPoints;
                    }

                    var job = this.kdTree.GetPointsInRadii(this.multiQueryCenters, this.multiQueryRadii, ref this.multiQuerySearchResults, default, 1);
                    job.Complete();
                }
                else
                {
                    var job = this.kdTree.GetPointsInRadius(this.mouseHitPos, this.searchRadius, ref this.searchResults);
                    job.Complete();
                }
            }

            kdTreeMarker.End();

            if(this.doNearestNeighborQuery)
            {
                this.DrawNearestNeighborLine();
            }

            this.DeactivateAllRings();

            this.ActivateRingsInSearchRadii();

            if(this.kdTreeSampler == null || !this.kdTreeSampler.isValid)
            {
                this.kdTreeSampler = Sampler.Get("KDTreeSearch");
            }
        }

        private void Dispose()
        {
            this.points.DisposeIfCreated();
            this.searchResults.DisposeIfCreated();

            if (this.kdTree.IsCreated)
            {
                this.kdTree.Dispose();
            }

            this.multiQueryCenters.DisposeIfCreated();
            this.multiQueryRadii.DisposeIfCreated();
            this.multiQuerySearchResults.DisposeIfCreated();

            this.nearestNeighbor.DisposeIfCreated();
            this.nearestNeighborQueryPos.DisposeIfCreated();

            this.searchPolygon.Dispose();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
