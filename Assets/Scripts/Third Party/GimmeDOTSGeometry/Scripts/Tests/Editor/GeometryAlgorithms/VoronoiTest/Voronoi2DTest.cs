using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class Voronoi2DTest 
    {
        /*
        private void Setup()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 7.0f;
            cam.transform.forward = Vector3.down;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            cam.clearFlags = CameraClearFlags.SolidColor;
           
            RuntimeTestUtility.CreateDirectionalLight();
        }



        [UnityTest]
        public IEnumerator NoArcDegeneracy()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[2] {
                new float2(-0.5f, 0.0f),
                new float2(0.5f, 0.0f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TwoPoints0()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[2] { 
                new float2(-0.75f, -0.5f),
                new float2(0.5f, 0.5f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while(!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator TwoPoints1()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[2] {
                new float2(0.0f, 0.5f),
                new float2(0.0f, -0.5f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator TwoPoints2()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[2] {
                new float2(0.1f, 0.5f),
                new float2(0.0f, -0.5f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator TwoPoints3()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[2] {
                new float2(0.5f, 0.0f),
                new float2(-0.5f, -0.1f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DiagonalOne()
        {
            this.Setup();


            var sites = new NativeArray<float2>(new float2[2] {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, 0.5f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator DiagonalTwo()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[2] {
                new float2(0.5f, -0.5f),
                new float2(-0.5f, 0.5f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThreePoints0()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(0.0f, 0.5f),
                new float2(-0.5f, 0.0f),
                new float2(0.5f, -0.4f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator ThreePoints1()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(0.0f, 0.3f),
                new float2(0.0f, 0.0f),
                new float2(0.0f, -0.5f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThreePoints2()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(0.5f, 0.5f),
                new float2(0.0f, 0.0f),
                new float2(-0.3f, -0.3f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThreePoints3()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(0.0f, 0.5f),
                new float2(-0.3f, -0.2f),
                new float2(0.3f, -0.25f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator ThreePoints4()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(0.5f, -0.5f),
                new float2(0.1f, -0.55f),
                new float2(0.55f, -0.1f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ThreePoints5()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(0.0f, 0.05f),
                new float2(0.6f, 0.0f),
                new float2(1.0f, 0.02f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FourPoints0()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[4] {
                new float2(0.0f, 0.5f),
                new float2(-0.5f, 0.0f),
                new float2(0.5f, -0.45f),
                new float2(-0.2f, 0.2f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FourPoints1()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[4] {
                new float2(-0.3f, 0.5f),
                new float2(0.5f, 0.4f),
                new float2(-0.5f, -0.4f),
                new float2(0.6f, -0.6f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator FourPoints2()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[4] {
                new float2(-0.3f, 0.5f),
                new float2(0.0f, 0.55f),
                new float2(0.2f, 0.4f),
                new float2(2.0f, 0.51f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }



        [UnityTest]
        public IEnumerator FourPoints3()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[4] {
                new float2(-0.3f, 0.5f),
                new float2(0.0f, 0.55f),
                new float2(0.2f, 0.4f),
                new float2(1.0f, 0.51f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator FourCollinear()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[4] {
                new float2(-0.5f, 0.1f),
                new float2(0.0f, 0.1f),
                new float2(0.3f, 0.1f),
                new float2(0.6f, 0.1f),
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }


        [UnityTest]
        public IEnumerator ThreeCollinear()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(-0.5f, 0.1f),
                new float2(0.0f, 0.1f),
                new float2(0.3f, 0.1f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BreakDegeneracy()
        {
            this.Setup();

            var sites = new NativeArray<float2>(new float2[3] {
                new float2(-0.5f, 0.1f),
                new float2(0.5f, 0.1f),
                new float2(0.0f, -0.5f)
            }, Allocator.Persistent);

            var voronoiGO = new GameObject("Voronoi");
            var stepper = voronoiGO.AddComponent<Voronoi2DStepper>();
            stepper.Init(sites, 0.1f, 0.1f);

            while (!stepper.finished)
            {
                yield return null;
            }
        }*/

        [Test]
        public void NinePoints()
        {
            var bounds = new Rect(-5.0f, -5.0f, 10.0f, 10.0f);
            var sites = new NativeArray<float2>(new float2[9]
            {
                new float2(-0.5620418f, -2.771394f),
                new float2(2.101959f,-1.46248f),
                new float2(2.262857f, -4.340491f),
                new float2(-0.5891013f, 0.2277222f),
                new float2(0.4643879f, -1.931563f),
                new float2(4.020946f, -1.140755f),
                new float2(-2.41565f, 0.8128252f),
                new float2(-4.58834f, 2.299792f),
                new float2(-1.083088f, 0.04793882f),
            }, Allocator.Persistent);

            var polygons = new NativeArray<NativePolygon2D>(sites.Length, Allocator.Persistent);
            var polygonSites = new NativeArray<int>(sites.Length, Allocator.Persistent);
            for(int i = 0; i < sites.Length; i++)
            {
                polygons[i] = new NativePolygon2D(Allocator.Persistent, 1);
            }

            var voronoiJob = Voronoi2D.CalculateVoronoi(bounds, sites, ref polygons, ref polygonSites, out var jobAllocations);
            voronoiJob.Complete();

            sites.Dispose();
            jobAllocations.Dispose();
            for(int i = 0; i < polygons.Length; i++)
            {
                polygons[i].Dispose();
            }
            polygons.Dispose();
            polygonSites.Dispose();
        }
    }
}
