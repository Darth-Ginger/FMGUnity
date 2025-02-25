using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GimmeDOTSGeometry.Samples
{
    public class KDTree3DSystem : MonoBehaviour
    {

        #region Public Variables

        public KDTree3DCamera treeCamera;

        public float searchRadius = 1.0f;
        public float sphereRadius = 0.1f;
        public float lineThickness = 0.005f;
        public float searchRingThickness = 0.05f;
        public float moveSpeed = 2.0f;
        public float searchRotationSpeed = 10.0f;

        public int nrOfPoints = 1000;

        public LineRenderer boundsRenderer = null;

        public Material lineMaterial = null;
        public Material sphereMaterial = null;
        public Material searchRingMaterial = null;

        public Vector3 min;
        public Vector3 max;

        public Vector3 searchMin;
        public Vector3 searchMax;

        #endregion

        #region Private Variables

        private bool doMultiQuery = false;
        private bool doNearestNeighborQuery = false;

        private Color lineColor;

        private float searchRotation = 0.0f;
        private float3 nearestNeighborPos;

        private GameObject searchRing;
        private GameObject neighborLine;
        private GameObject[] multiQuerySearchRings;

        private List<GameObject> spheres = new List<GameObject>();

        private NativeArray<Position3D> points;
        private NativeArray<float3> multiQueryCenters;
        private NativeArray<float> multiQueryRadii;

        private NativeArray<float3> nearestNeigborQueryPos;
        private NativeArray<Position3D> nearestNeighbor;

        private NativeParallelHashSet<Position3D> multiQuerySearchResults;
        private NativeList<Position3D> searchResults;

        private Native3DKDTree<Position3D> kdTree;

        private static readonly ProfilerMarker kdTreeMarker = new ProfilerMarker("KDTreeSearch");

        private Sampler kdTreeSampler = null;

        private Vector3 currentPos;
        private Vector3[] searchRingOffsets;

        #endregion

        public bool IsDoingMultiQuery() => this.doMultiQuery;
        public bool IsDoingNearestNeighborQuery() => this.doNearestNeighborQuery;
        public Sampler GetKDTreeSampler() => this.kdTreeSampler;


        public void EnableNearestNeighborQuery(bool enable)
        {
            this.doNearestNeighborQuery = enable;

            var mr = this.searchRing.GetComponent<MeshRenderer>();
            mr.enabled = !this.doNearestNeighborQuery;

            mr = this.neighborLine.GetComponent<MeshRenderer>();
            mr.enabled = this.doNearestNeighborQuery;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                mr = this.multiQuerySearchRings[i].GetComponent<MeshRenderer>();
                mr.enabled = !this.doNearestNeighborQuery;
            }
        }

        public void EnableMultiQuery(bool enable)
        {
            this.doMultiQuery = enable;

            var mr = this.searchRing.GetComponent<MeshRenderer>();
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

            for (int i = 0; i < this.nrOfPoints; i++)
            {
                var position = float3.zero;

                position.x = UnityEngine.Random.Range(this.min.x, this.max.x);
                position.y = UnityEngine.Random.Range(this.min.y, this.max.y);
                position.z = UnityEngine.Random.Range(this.min.z, this.max.z);

                this.points[i] = new Position3D(position);
            }
        }


        private void CreateSpheres()
        {

            for (int i = 0; i < this.points.Length; i++)
            {
                var sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereObj.SetActive(false);
                sphereObj.transform.parent = this.transform;
                sphereObj.transform.localScale = new Vector3(this.sphereRadius, this.sphereRadius, this.sphereRadius);

                var meshRenderer = sphereObj.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = this.sphereMaterial;

                this.spheres.Add(sphereObj);
            }

        }

        private unsafe void CreateKDTreeRectanglesRecursion(int currentNodeIdx, Bounds currentBounds, int depth)
        {
            var nodes = this.kdTree.GetNodes();

            var currentNode = nodes[currentNodeIdx];
            int axis = depth % 3;

            float splitPlane = currentNode.Position[axis];

            float3 minLeft = currentBounds.min;
            float3 maxLeft = currentBounds.max;
            float3 minRight = currentBounds.min;
            float3 maxRight = currentBounds.max;

            maxLeft[axis] = splitPlane;
            minRight[axis] = splitPlane;

            Vector3[] planePoints = new Vector3[5];

            switch (axis)
            {
                case 0:
                    planePoints[0] = new Vector3(maxLeft.x, minLeft.y, minLeft.z);
                    planePoints[1] = new Vector3(maxLeft.x, maxLeft.y, minLeft.z);
                    planePoints[2] = new Vector3(maxLeft.x, maxLeft.y, maxLeft.z);
                    planePoints[3] = new Vector3(maxLeft.x, minLeft.y, maxLeft.z);
                    break;
                case 1:
                    planePoints[0] = new Vector3(minLeft.x, maxLeft.y, minLeft.z);
                    planePoints[1] = new Vector3(maxLeft.x, maxLeft.y, minLeft.z);
                    planePoints[2] = new Vector3(maxLeft.x, maxLeft.y, maxLeft.z);
                    planePoints[3] = new Vector3(minLeft.x, maxLeft.y, maxLeft.z);
                    break;
                case 2:
                    planePoints[0] = new Vector3(minLeft.x, minLeft.y, maxLeft.z);
                    planePoints[1] = new Vector3(maxLeft.x, minLeft.y, maxLeft.z);
                    planePoints[2] = new Vector3(maxLeft.x, maxLeft.y, maxLeft.z);
                    planePoints[3] = new Vector3(minLeft.x, maxLeft.y, maxLeft.z);
                    break;
            }

            planePoints[4] = planePoints[0];

            var rectangleObject = new GameObject();

            var lineRenderer = rectangleObject.AddComponent<LineRenderer>();

            var mpb = new MaterialPropertyBlock();
            var col = this.lineColor;
            col.a = col.a * Mathf.Pow(1.0f - (1.0f / Mathf.Log(this.nrOfPoints)), depth);
            mpb.SetColor("_Color", col);

            lineRenderer.sharedMaterial = this.lineMaterial;
            lineRenderer.widthMultiplier = this.lineThickness;

            lineRenderer.transform.parent = this.transform;

            lineRenderer.positionCount = planePoints.Length;
            lineRenderer.SetPositions(planePoints);
            lineRenderer.SetPropertyBlock(mpb);


            var boundsLeft = new Bounds();
            var boundsRight = new Bounds();

            boundsLeft.SetMinMax(minLeft, maxLeft);
            boundsRight.SetMinMax(minRight, maxRight);

            int left = currentNodeIdx * 2 + 1;
            int right = currentNodeIdx * 2 + 2;
            if (left < nodes.Length)
            {
                this.CreateKDTreeRectanglesRecursion(left, boundsLeft, depth + 1);
            }

            if (right < nodes.Length)
            {
                this.CreateKDTreeRectanglesRecursion(right, boundsRight, depth + 1);
            }
        }

        private unsafe void CreateKDTreeRectangles()
        {

            var bounds = this.kdTree.GetBounds();
            this.CreateKDTreeRectanglesRecursion(0, bounds, 0);
            
        }

        private void InitMultiQueries()
        {
            this.multiQuerySearchRings = new GameObject[8];
            this.searchRingOffsets = new Vector3[8];

            var searchRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.searchRingThickness);

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
            }

            this.multiQuerySearchResults = new NativeParallelHashSet<Position3D>(this.nrOfPoints, Allocator.Persistent);
            this.multiQueryRadii = new NativeArray<float>(this.multiQuerySearchRings.Length, Allocator.Persistent);
            this.multiQueryCenters = new NativeArray<float3>(this.multiQuerySearchRings.Length, Allocator.Persistent);
        }

        void Start()
        {
            this.CreatePoints();
            this.CreateSpheres();

            this.searchResults = new NativeList<Position3D>(Allocator.Persistent);

            Profiler.BeginSample("Construction");
            this.kdTree = new Native3DKDTree<Position3D>(this.points, Allocator.Persistent);
            Profiler.EndSample();
            this.lineColor = this.lineMaterial.GetColor("_Color");

            this.CreateKDTreeRectangles();

            this.searchRing = new GameObject("Search Ring");
            this.searchRing.transform.parent = this.transform;

            this.neighborLine = new GameObject("Neighbor Line");
            this.neighborLine.transform.parent = this.transform;

            var searchRing = MeshUtil.CreateRing(this.searchRadius, this.searchRingThickness);

            var meshFilter = this.searchRing.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = searchRing;

            var meshRenderer = this.searchRing.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;

            var line = MeshUtil.CreateLine(new LineSegment3D() { a = new float3(0, 0, 0), b = new float3(1, 1, 1) }, this.searchRingThickness);

            meshFilter = this.neighborLine.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = line;

            meshRenderer = this.neighborLine.AddComponent<MeshRenderer>();
            meshRenderer.material = this.searchRingMaterial;
            meshRenderer.enabled = false;

            this.nearestNeighbor = new NativeArray<Position3D>(1, Allocator.Persistent);
            this.nearestNeigborQueryPos = new NativeArray<float3>(1, Allocator.Persistent);

            this.InitMultiQueries();

            var bounds = new Bounds();
            bounds.SetMinMax(this.searchMin, this.searchMax);
            this.boundsRenderer.SetPositionsFromBounds(bounds);
        }


        public void UpdateSearchRingRadius()
        {
            var newSearchRing = MeshUtil.CreateRing(this.searchRadius, this.searchRingThickness);
            var meshFilter = this.searchRing.GetComponent<MeshFilter>();

            meshFilter.sharedMesh = newSearchRing;

            var newMultiQueryRing = MeshUtil.CreateRing(this.searchRadius * 0.5f, this.searchRingThickness);

            for(int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var ring = this.multiQuerySearchRings[i];
                meshFilter = ring.GetComponent<MeshFilter>();
                meshFilter.sharedMesh = newMultiQueryRing;
                this.searchRingOffsets[i] = this.searchRingOffsets[i].normalized * this.searchRadius;
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

            this.currentPos = Vector3.Max(this.currentPos, this.searchMin);
            this.currentPos = Vector3.Min(this.currentPos, this.searchMax);
        }

        private void DeactivateAllSpheres()
        {
            for (int i = 0; i < this.points.Length; i++)
            {
                this.spheres[i].SetActive(false);
            }
        }

        private void ActivateSpheresInSearchRadii()
        {
            if (this.doMultiQuery)
            {
                int counter = 0;
                foreach (var position in this.multiQuerySearchResults)
                {
                    var sphere = this.spheres[counter];

                    sphere.transform.position = position.Position;
                    sphere.SetActive(true);

                    counter++;
                }
            }
            else
            {
                for (int i = 0; i < this.searchResults.Length; i++)
                {
                    var sphere = this.spheres[i];

                    var position = this.searchResults[i];

                    sphere.transform.position = position.Position;
                    sphere.SetActive(true);
                }
            }
        }

        private void FaceRingsTowardsCamera()
        {
            this.searchRing.transform.position = this.currentPos;
            this.searchRing.transform.up = -this.treeCamera.transform.forward;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                this.multiQuerySearchRings[i].transform.up = -this.treeCamera.transform.forward;
            }
        }

        private void UpdateMultiQuerySearch()
        {

            this.searchRotation += Time.deltaTime * this.searchRotationSpeed;

            for (int i = 0; i < this.multiQuerySearchRings.Length; i++)
            {
                var offset = Quaternion.AngleAxis(this.searchRotation, Vector3.up) * this.searchRingOffsets[i];
                var pos = this.currentPos + offset;

                this.multiQuerySearchRings[i].transform.position = pos;

                this.multiQueryRadii[i] = this.searchRadius * 0.5f;
                this.multiQueryCenters[i] = pos;
            }
        }

        private void DrawNearestNeighborLine()
        {
            this.nearestNeighborPos = this.nearestNeighbor[0].Position;
            var ls = new LineSegment3D()
            {
                a = this.nearestNeighborPos,
                b = this.currentPos,
            };

            var mf = this.neighborLine.GetComponent<MeshFilter>();
            mf.sharedMesh = MeshUtil.CreateLine(ls, this.searchRingThickness);
        }

        void Update()
        {

            this.searchResults.Clear();
            this.multiQuerySearchResults.Clear();

            this.FaceRingsTowardsCamera();
            this.UpdateMultiQuerySearch();

            this.nearestNeigborQueryPos[0] = this.currentPos;

            kdTreeMarker.Begin();

            if(this.doNearestNeighborQuery)
            {
                var job = this.kdTree.GetNearestNeighbors(this.nearestNeigborQueryPos, ref this.nearestNeighbor);
                job.Complete();
            }
            else if (this.doMultiQuery)
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
                var job = this.kdTree.GetPointsInRadius(this.currentPos, this.searchRadius, ref this.searchResults);
                job.Complete();
            }

            kdTreeMarker.End();

            this.DeactivateAllSpheres();

            this.ActivateSpheresInSearchRadii();

            if(this.doNearestNeighborQuery)
            {
                this.DrawNearestNeighborLine();
            }

            if (this.kdTreeSampler == null || !this.kdTreeSampler.isValid)
            {
                this.kdTreeSampler = Sampler.Get("KDTreeSearch");
            }


            this.treeCamera.SetTargetPoint(this.currentPos);

            this.HandleInput();
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
            this.nearestNeigborQueryPos.DisposeIfCreated();
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}
