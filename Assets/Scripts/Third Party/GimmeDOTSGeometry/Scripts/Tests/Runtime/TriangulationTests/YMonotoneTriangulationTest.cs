using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace GimmeDOTSGeometry
{
    //Make sure to activate gizmos before running the test, to see the triangulation lines
    public class YMonotoneTriangulationTest
    {
        private void Setup()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 7.0f;

            RuntimeTestUtility.CreateDirectionalLight();

        }

        private GameObject Triangulate(NativePolygon2D poly, ref NativeList<int> triangulation)
        {

            var job = Polygon2DTriangulation.YMonotoneTriangulationJob(poly, ref triangulation, Allocator.Persistent);
            job.Complete();

            var mesh = MeshUtil.CreatePolygonMesh(poly, triangulation.AsArray());

            var go = new GameObject("Polygon");
            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RuntimeTestUtility.CreateUnlitMaterial(Color.cyan);

            var lineDrawer = go.AddComponent<PolygonTriangulationLinesDrawer>();
            lineDrawer.Init(poly, triangulation);

            return go;
        }

        [UnityTest]
        public IEnumerator TriangulationTest1()
        {
            Setup();

            //Example provided by @Dr_Krieg (Discord-Member)
            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(-1, -1),
                new float2(0, -1),
                new float2(1, -1),
                new float2(1, -2),
                new float2(2, -2),
                new float2(2, -1),
                new float2(2, 0),
                new float2(2, 1),
                new float2(1, 1),
                new float2(0, 1),
                new float2(-1, 1),
                new float2(-2, 1),
                new float2(-2, 0),
                new float2(-2, -1),
                new float2(-2, -2),
                new float2(-1, -2),
            };

            var poly = new NativePolygon2D(Allocator.Persistent, points);
            var triangulation = new NativeList<int>(Allocator.Persistent);

            var go = Triangulate(poly, ref triangulation);

            points.Dispose();

            float timer = 0.0f;
            while (timer < RuntimeTestUtility.ShowTime)
            {
                yield return null;
                timer += Time.deltaTime;
            }

            GameObject.Destroy(go);
            yield return null;
            poly.Dispose();
            triangulation.Dispose();

        }


        [UnityTest]
        public IEnumerator TriangulationTest2()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(-1, -1),
                new float2(1, -1),
                new float2(1, 1),
                new float2(-1, 1),
            };

            var poly = new NativePolygon2D(Allocator.Persistent, points);

            poly.AddHole(new Vector2[4]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f),
            });

            var triangulation = new NativeList<int>(Allocator.Persistent);

            var go = Triangulate(poly, ref triangulation);

            points.Dispose();

            float timer = 0.0f;
            while (timer < RuntimeTestUtility.ShowTime)
            {
                yield return null;
                timer += Time.deltaTime;
            }

            GameObject.Destroy(go);
            yield return null;
            poly.Dispose();
            triangulation.Dispose();

        }


        [UnityTest]
        public IEnumerator TriangulationTest3()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(-1f, -1.5f),
                new float2(0, -1.5f),
                new float2(1, -1.5f),
                new float2(1, -1.0f),
                new float2(1.5f, -1.0f),
                new float2(1.5f, 1.0f),
                new float2(1.0f, 1.0f),
                new float2(1.0f, 1.5f),
                new float2(0.0f, 1.5f),
                new float2(-1.0f, 1.5f),
                new float2(-1.0f, 1.0f),
                new float2(-1.5f, 1.0f),
                new float2(-1.5f, -1.0f),
                new float2(-1.0f, -1.0f),
            };

            var poly = new NativePolygon2D(Allocator.Persistent, points);

            poly.AddHole(new Vector2[4]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f),
            });


            var triangulation = new NativeList<int>(Allocator.Persistent);

            var go = Triangulate(poly, ref triangulation);

            points.Dispose();

            float timer = 0.0f;
            while (timer < RuntimeTestUtility.ShowTime)
            {
                yield return null;
                timer += Time.deltaTime;
            }

            GameObject.Destroy(go);
            yield return null;
            poly.Dispose();
            triangulation.Dispose();

        }

    }
}
