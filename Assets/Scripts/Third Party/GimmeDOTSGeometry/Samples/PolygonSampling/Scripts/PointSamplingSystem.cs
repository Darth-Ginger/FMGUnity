

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;


namespace GimmeDOTSGeometry.Samples
{

    public class PointSamplingSystem : MonoBehaviour
    {

        #region Public Variables

        public Color pointColor;

        public float yOffset = 0.01f;
        public float pointSize = 0.1f;

        public int initialPointSize;

        public GameObject point;

        public Material polyMaterial;

        public Polygon2DWrapper polygonWrapper;

        #endregion

        #region Private Variables

        private GameObject polygonGO;

        private MaterialPropertyBlock MPB;

        private int positionCount = 0;

        private List<GameObject> points = new List<GameObject>();

        private NativePolygon2DSampler polySampler;

        private Polygon2DSampleMethod sampleMethod = Polygon2DSampleMethod.DISTANCE_FIELD;

        private Sampler pointLocationSampler = null;

        #endregion

        private static readonly string SHADER_COLOR = "_Color";
        private static readonly ProfilerMarker pointLocationMarker = new ProfilerMarker("PointLocation");


        public Sampler GetPointLocationSampler() => this.pointLocationSampler;

        public int GetNrOfPoints() => this.positionCount;

        public Polygon2DSampleMethod SampleMethod() => this.sampleMethod;

        void Start()
        {
            this.polygonWrapper.Init();

            var poly = this.polygonWrapper.polygon;

            var simplePoly = NativePolygon2D.MakeSimple(Allocator.TempJob, poly);
            var triangulation = Polygon2DTriangulation.EarClippingTriangulate(simplePoly);

            var mesh = MeshUtil.CreatePolygonMesh(simplePoly, triangulation);

            this.polygonGO = new GameObject("Polygon");
            this.polygonGO.transform.parent = this.transform;

            var meshFilter = this.polygonGO.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            var meshRenderer = this.polygonGO.AddComponent<MeshRenderer>();
            meshRenderer.material = this.polyMaterial;

            this.MPB = new MaterialPropertyBlock();
            this.MPB.SetColor(SHADER_COLOR, this.pointColor);

            this.polySampler = new NativePolygon2DSampler(Allocator.Persistent, poly, 100, 100, Polygon2DSampleMethod.DISTANCE_FIELD);

            this.AddPoints(this.initialPointSize);

            simplePoly.Dispose();
        }

        public void ChangeSampleMethod(Polygon2DSampleMethod sampleMethod)
        {
            this.polySampler.Dispose();

            var poly = this.polygonWrapper.polygon;

            this.polySampler = new NativePolygon2DSampler(Allocator.Persistent, poly, 100, 100, sampleMethod);

            this.sampleMethod = sampleMethod;
        }

        public void ClearPoints()
        {
            for(int i = 0; i < this.points.Count; i++)
            {
                GameObject.Destroy(this.points[i]);
            }

            this.points.Clear();
        }

        public void AddPoints(int nrOfPoints)
        {

            this.positionCount += nrOfPoints;


            NativeList<float2> pointPositions = new NativeList<float2>(Allocator.TempJob);

            var sampleHandle = this.polySampler.SamplePoints(nrOfPoints, ref pointPositions);
            sampleHandle.Complete();

            for(int i = 0; i < pointPositions.Length; i++)
            {
                var pos = pointPositions[i];
                var worldPos = new Vector3(pos.x, this.yOffset, pos.y);

                var point = GameObject.Instantiate(this.point);
                point.transform.parent = this.transform;
                point.transform.position = worldPos;

                var meshRenderer = point.GetComponentInChildren<MeshRenderer>();
                meshRenderer.SetPropertyBlock(this.MPB);

                this.points.Add(point);
            }

            pointPositions.Dispose();
        }


        void Update()
        {

        }

        private void Dispose()
        {
            if (this.polySampler.IsCreated)
            {
                this.polySampler.Dispose();
            }
        }

        private void OnDestroy()
        {
            this.Dispose();
        }
    }
}