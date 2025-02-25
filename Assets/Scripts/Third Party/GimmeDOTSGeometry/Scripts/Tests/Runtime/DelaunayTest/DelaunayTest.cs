using System.Collections;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace GimmeDOTSGeometry
{
    public class DelaunayTest 
    {

        private void Setup()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 10.0f;
            cam.transform.forward = Vector3.down;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            RuntimeTestUtility.CreateDirectionalLight();
        }

        private GameObject CreateTriangle(NativeTriangle3D triangle)
        {
            var triangleMesh = MeshUtil.CreateTriangle(triangle);

            var triangleGo = new GameObject($"Triangle");

            var mf = triangleGo.AddComponent<MeshFilter>();
            mf.mesh = triangleMesh;

            var mr = triangleGo.AddComponent<MeshRenderer>();
            mr.material = RuntimeTestUtility.CreateUnlitMaterial(Color.cyan);

            return triangleGo;
        }

        [UnityTest]
        public IEnumerator DelaunayTest1()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(0, 0),
                new float2(3, 0),
                new float2(3, 3)
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while(!stepper.finished)
            {
                yield return null;
            }

        }



        [UnityTest]
        public IEnumerator DelaunayTest2()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(0, 0),
                new float2(3, 0),
                new float2(3, 3),
                new float2(0, 3),
            };


            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }




        [UnityTest]
        public IEnumerator DelaunayTest3()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(-2, 0),
                new float2(4, 1),
                new float2(5, 3),
                new float2(0, 3),
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator DelaunayTest4()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(1.808626f, 3.659459f),
                new float2(-2.506015f, -2.133718f),
                new float2(-0.5679724f, -3.254171f),
                new float2(1.15012f, 2.16657f),
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DelaunayTest5()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(1.290648f, 3.753161f),
                new float2(-2.435814f, 0.4945734f),
                new float2(-3.99791f, 2.859239f),
                new float2(-1.780279f, -3.704404f),
                new float2(-0.3484472f, -0.7484827f),
                new float2(-1.72839f, 2.54185f),
                new float2(1.249507f, -3.328268f),
                new float2(2.720228f, -0.4404614f),
                new float2(-3.655077f, -2.548617f),
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DelaunayTest6()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(1.345793f, 3.366262f),
                new float2(-2.501199f, 0.08421735f),
                new float2(-3.337929f, 2.774806f),
                new float2(-1.188509f, -3.411703f),
                new float2(-0.02791732f, -0.01200345f),
                new float2(-0.6284496f, 2.706267f),
                new float2(0.8875678f, -4.135719f),
                new float2(2.499016f, -4.342084E-05f),
                new float2(-3.646757f, -3.087841f),
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DelaunayTest7()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(-3.780567f, -2.706054f),
                new float2(-1.559079f, -0.05238381f),
                new float2(-2.958168f, 3.189691f),
                new float2(-1.157977f, -3.033186f),
                new float2(-0.2513209f, -0.2868904f),
                new float2(-1.433677f, 3.524721f),
                new float2(1.868958f, -3.029507f),
                new float2(3.440059f, 0.2256542f),
                new float2(1.201557f, 4.091408f),
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        
        [UnityTest]
        public IEnumerator DelaunayTest8()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(-4.795383f, -4.223825f),         //0
                new float2(-3.4484f, -2.232002f),           //6
                new float2(-4.230311f, -0.6354748f),        //12
                new float2(-3.705988f, 0.706986f),          //18
                new float2(-4.271442f, 2.49203f),           //24
                new float2(-3.184175f, 3.824863f),          //30

                new float2(-2.82339f, -4.323812f),          //1
                new float2(-2.372542f, -2.252189f),         //7
                new float2(-2.707673f, -0.58789f),          //13
                new float2(-1.877921f, 1.337677f),          //19
                new float2(-2.911858f, 2.537031f),          //25
                new float2(-1.970738f, 4.192628f),          //31

                new float2(-1.470758f, -4.238768f),         //2
                new float2(-0.550707f, -2.560691f),         //8
                new float2(-1.249121f, -1.05596f),          //14
                new float2(-0.5879518f, 0.5723619f),        //20
                new float2(-1.385205f, 2.776885f),          //26
                new float2(-1.027745f, 4.18304f),           //32

                new float2(-0.2814942f, -4.112146f),        //3
                new float2(0.8006479f, -2.659481f),         //9
                new float2(0.03259943f, -1.049869f),        //15
                new float2(0.7752064f, 0.924412f),          //21
                new float2(-0.3470168f, 2.229478f),         //27
                new float2(1.01391f, 4.380322f),            //33

                new float2(1.198527f, -4.214371f),          //4
                new float2(2.28773f, -2.056334f),           //10
                new float2(1.235761f, -0.7996256f),         //16
                new float2(2.480357f, 1.015612f),           //22
                new float2(1.335244f, 2.358459f),           //28
                new float2(2.127419f, 4.408235f),           //34

                new float2(3.07024f, -4.285481f),           //5
                new float2(3.617303f, -2.89666f),           //11
                new float2(2.650636f, -0.800903f),          //17
                new float2(3.853598f, 1.124285f),           //23
                new float2(2.848858f, 2.49786f),            //29
                new float2(4.009638f, 4.40522f),            //35
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator DelaunaySplitFourTest()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent)
            {
                new float2(3.0f, 3.0f),
                new float2(0.0f, 3.0f),
                new float2(3.0f, 0.0f),
                new float2(0.0f, 0.0f),
                new float2(1.5f, 1.5f),
            };

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DelaunayGridTest()
        {
            Setup();

            var points = new NativeList<float2>(Allocator.Persistent);
            for(int y = 0; y < 5; y++)
            {
                for(int x = 0; x < 5; x++)
                {
                    points.Add(new float2(x, y));
                }
            }

            for(int i = 0; i < 25; i++)
            {
                int idxA = UnityEngine.Random.Range(0, points.Length);
                int idxB = UnityEngine.Random.Range(0, points.Length);

                points.Swap(idxA, idxB);
            }

            var delaunayGo = new GameObject("Delaunay");
            var stepper = delaunayGo.AddComponent<DelaunayStepper>();
            stepper.Init(points, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

    }
}
