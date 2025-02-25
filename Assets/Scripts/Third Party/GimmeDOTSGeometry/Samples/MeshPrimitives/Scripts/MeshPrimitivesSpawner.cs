using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class MeshPrimitivesSpawner : MonoBehaviour
    {
        #region Public Fields

        public Color color2D;
        public Color color2DOutline;
        public Color color3D;
        public Color color3DOutline;

        public float rotationSpeed = 15.0f;

        public Material shapeMaterial;

        public Vector3[] positionBuffer;

        #endregion

        #region Private Fields

        private MaterialPropertyBlock mpb;

        #endregion

        private GameObject CreateObject(string name, int idx, bool twoDimensional, Color color,
            out MeshFilter meshFilter)
        {
            var obj = new GameObject(name);
            obj.transform.parent = this.transform;

            obj.transform.position = this.positionBuffer[idx];

            var spin = obj.AddComponent<Spin>();
            spin.rotationSpeed = this.rotationSpeed;

            if (twoDimensional)
            {
                spin.rotationAxis = Vector3.up;
            } else
            {
                spin.rotationAxis = UnityEngine.Random.insideUnitSphere;
            }

            meshFilter = obj.AddComponent<MeshFilter>();

            var meshRenderer = obj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = this.shapeMaterial;

            this.mpb.SetColor("_Color", color);
            meshRenderer.SetPropertyBlock(this.mpb);

            return obj;
        }

        private GameObject CreateGrid(int cellsX, int cellsY)
        {
            var gridObj = new GameObject("Grid");
            gridObj.transform.parent = this.transform;
            gridObj.transform.position = Vector3.zero;

            var gridMeshFilter = gridObj.AddComponent<MeshFilter>();
            gridMeshFilter.mesh = MeshUtil.CreateGrid2D(new Vector2(1, 1), cellsX, cellsY, 0.1f, CardinalPlane.XZ);

            var gridMeshRenderer = gridObj.AddComponent<MeshRenderer>();
            gridMeshRenderer.sharedMaterial = this.shapeMaterial;

            this.mpb.SetColor("_Color", Color.gray * 0.5f);
            gridMeshRenderer.SetPropertyBlock(this.mpb);

            return gridObj;
        }

        private void SpawnMeshes()
        {
            var grid2DShapes = this.CreateGrid(3, 2);
            grid2DShapes.transform.position = new Vector3(-2.5f, 0.0f, 2.0f);

            var grid3DShapes = this.CreateGrid(4, 3);
            grid3DShapes.transform.position = new Vector3(2.5f, 0.0f, 1.5f);

            var grid2DOutlines = this.CreateGrid(3, 2);
            grid2DOutlines.transform.position = new Vector3(-2.5f, 0.0f, -1.0f);

            var grid3DOutlines = this.CreateGrid(3, 2);
            grid3DOutlines.transform.position = new Vector3(2.5f, 0.0f, -2.0f);

            //=== 2D ===

            int idx = 0;

            //Line

            var lineSegment2D = new LineSegment2D(new Vector2(-0.45f, 0), new Vector2(0.45f, 0));
            var go = this.CreateObject("Line2D", idx, true, this.color2D, out var meshFilter);
            meshFilter.mesh = MeshUtil.CreateLine(lineSegment2D, 0.075f, CardinalPlane.XZ);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Triangle

            float height = Mathf.Sin(Mathf.PI / 3.0f) * 0.8f;
            float3 a = new float3(-0.4f, 0.0f, -height * 0.333f);
            float3 b = new float3(0.0f, 0.0f, height * 0.666f);
            float3 c = new float3(0.4f, 0.0f, -height * 0.333f);

            var triangle2D = new NativeTriangle3D(a, b, c);
            go = this.CreateObject("Triangle2D", idx, true, this.color2D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTriangle(triangle2D);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Rectangle

            var rect = new Rect(new Vector2(-0.4f, -0.275f), new Vector2(0.8f, 0.55f));
            go = this.CreateObject("Rectangle", idx, true, this.color2D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateRectangle(rect);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Circle

            go = this.CreateObject("Circle", idx, true, this.color2D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateCircle(0.375f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Arrow


            go = this.CreateObject("Arrow2D", idx, true, this.color2D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateArrow2D(lineSegment2D, 0.075f, 0.325f, 0.2f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Polygon

            var polygon = new NativePolygon2D(Allocator.TempJob, new float2[7]
            {
                new float2(-0.45f, 0.0f),
                new float2(-0.1f, -0.1f),
                new float2(0.1f, -0.35f),
                new float2(0.45f, 0.05f),
                new float2(0.0f, 0.15f),
                new float2(0.0f, 0.4f),
                new float2(-0.15f, 0.4f),
            });
            var triangulation = new NativeList<int>(Allocator.TempJob);

            var triangulationJob = Polygon2DTriangulation.EarClippingTriangulationJob(polygon, ref triangulation);
            triangulationJob.Complete();

            go = this.CreateObject("Polygon2D", idx, true, this.color2D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreatePolygonMesh(polygon, triangulation.AsArray());
            go.transform.position = this.positionBuffer[idx];
            idx++;


            //Triangle Outline

            go = this.CreateObject("Triangle2D Outline", idx, true, this.color2DOutline, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTriangleOutline(triangle2D, 0.15f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Rectangle Outline

            go = this.CreateObject("Rectangle Outline", idx, true, this.color2DOutline, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateRectangleOutline(rect, 0.075f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Circle Outline

            go = this.CreateObject("Ring", idx, true, this.color2DOutline, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateRing(0.375f, 0.075f);
            go.transform.position = this.positionBuffer[idx];
            idx++;


            //=== 3D ===

            //Line

            var lineSegment3D = new LineSegment3D(new Vector3(-0.45f, 0.0f, 0.0f), new Vector3(0.45f, 0.0f, 0.0f));
            go = this.CreateObject("Line3D", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateLine(lineSegment3D, 0.0375f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Triangle

            go = this.CreateObject("Triangle3D", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTriangle(triangle2D);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Box

            go = this.CreateObject("Box", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateBox(new Bounds(Vector3.zero, new Vector3(0.8f, 0.6f, 0.4f)));
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Arrow

            go = this.CreateObject("Arrow 3D", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateArrow3D(lineSegment3D, 0.0375f, 0.325f, 0.05f, 16);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Tetrahedron

            var tetrahedron = new Tetrahedron(new float3(-0.4f, -0.25f, -0.4f),
                new float3(0.4f, -0.25f, -0.4f),
                new float3(0.0f, -0.25f, 0.86f * 0.8f - 0.4f),
                new float3(0.0f, 0.35f, -0.266f));
            go = this.CreateObject("Tetrahedron", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTetrahedron(tetrahedron);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Torus

            var torus = new Torus(0.1f, 0.25f);
            go = this.CreateObject("Torus", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTorus(torus);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Cylinder

            var cylinder = new Cylinder(0.25f, 0.65f);
            go = this.CreateObject("Cylinder", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateCylinder(cylinder);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Cone

            var cone = new Cone(0.25f, 0.5f);
            go = this.CreateObject("Cone", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateCone(cone);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Prism

            go = this.CreateObject("Prism", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreatePrism(polygon, triangulation.AsArray(), Vector3.up * 0.5f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Triangle Outline

            go = this.CreateObject("Triangle3D Outline", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTriangleOutline(triangle2D, 0.15f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Box Outline

            go = this.CreateObject("Box Outline", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateBoxOutline(new Bounds(Vector3.zero, new Vector3(0.8f, 0.6f, 0.4f)), 0.075f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Tetrahedron Outline

            go = this.CreateObject("Tetrahedron Outline", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateTetrahedronOutline(tetrahedron, 0.15f);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //Polygon Outline

            go = this.CreateObject("Polygon2D Outline", idx, true, this.color2D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreatePolygonOutline(polygon, 0.1f, CardinalPlane.XZ);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //3D Grid

            go = this.CreateObject("3D Grid", idx, false, this.color3DOutline, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateGrid3D(new Vector3(0.1f, 0.1f, 0.1f), new int3(4, 4, 4), 0.01f);
            go.transform.position = this.positionBuffer[idx];
            idx++;


            //UV Sphere

            go = this.CreateObject("UV Sphere", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateUVSphere(0.4f, 48, 48);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            //UV Capsule

            go = this.CreateObject("UV Capsule", idx, false, this.color3D, out meshFilter);
            meshFilter.mesh = MeshUtil.CreateUVCapsule(0.3f, 0.4f, 24, 12);
            go.transform.position = this.positionBuffer[idx];
            idx++;

            polygon.Dispose();
            triangulation.Dispose();

        }

        private void Start()
        {
            this.mpb = new MaterialPropertyBlock();

            this.SpawnMeshes();
        }

    }
}
