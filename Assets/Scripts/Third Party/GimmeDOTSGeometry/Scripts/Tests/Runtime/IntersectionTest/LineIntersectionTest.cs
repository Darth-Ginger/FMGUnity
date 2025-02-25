using NUnit.Framework;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace GimmeDOTSGeometry
{
    //Just in case: The idea is to step through in the inspector until the end for most test cases,
    //so that the process can be observed in human time-scales for mistakes

    public class IntersectionTest
    {


        [UnityTest]
        public IEnumerator RandomSegments()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 100.0f;

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            stepper.intersectionVertices = 4; //Otherwise the GPU stalls way too much ... haha...
            stepper.intersectionRadius = 0.15f;
            stepper.intersectionThickness = 0.01f;
            stepper.Init(new Rect(-50.0f, -50.0f, 100.0f, 100.0f), 1500, Color.cyan, 0.015f);

            int stepsPerFrame = 200;
            while(!stepper.IsDone())
            {
                for(int i = 0; i < stepsPerFrame; i++)
                {
                    if(!stepper.IsDone())
                    {
                        stepper.Step();
                    }
                }
                yield return null;
            }

        }

        [UnityTest]
        public IEnumerator TwoIntersectingSegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, 0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.5f),
                b = new Vector2(0.5f, -0.5f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 1);
        }



        [UnityTest]
        public IEnumerator TwoNonIntersectingSegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(-0.5f, 0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, 0.5f),
                b = new Vector2(0.5f, -0.5f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 0);
        }


        [UnityTest]
        public IEnumerator FourIntersectingSegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, 0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.5f),
                b = new Vector2(0.5f, -0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.25f),
                b = new Vector2(0.5f, 0.0f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.0f),
                b = new Vector2(0.5f, -0.25f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 5);
        }

        [UnityTest]
        public IEnumerator FourIntersectingSegments2()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, 0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.5f),
                b = new Vector2(0.5f, -0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.0f),
                b = new Vector2(0.5f, 0.0f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.0f, -0.5f),
                b = new Vector2(0.0f, 0.5f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 1);
        }

        [UnityTest]
        public IEnumerator FourIntersectingSegments3()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 5.0f;

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(4.289999f, -0.003826627f),
                b = new Vector2(1.285603f, -0.001191493f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-1.623874f, -1.621743f),
                b = new Vector2(-1.620186f, 1.625428f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(1.969667f, -1.972881f),
                b = new Vector2(1.973351f, 1.96919f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.2999978f, 0.0002459485f),
                b = new Vector2(-4.289999f, 0.005205757f)
            });

            stepper.intersectionRadius = 0.5f;
            stepper.intersectionThickness = 0.1f;
            stepper.Init(segments, Color.yellow, 0.01f);


            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 2);
        }

        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator FiveSegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.395f, 0.169f),
                b = new Vector2(-0.348f, -0.152f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.265f, 0.427f),
                b = new Vector2(0.023f, 0.173f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.208f, -0.412f),
                b = new Vector2(0.044f, 0.091f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.326f, -0.161f),
                b = new Vector2(-0.472f, 0.251f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.341f, -0.109f),
                b = new Vector2(0.248f, 0.276f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 1);
        }

        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator FiveSegments2()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.083f, -0.452f),
                b = new Vector2(-0.476f, -0.429f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.283f, 0.257f),
                b = new Vector2(-0.434f, 0.147f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.247f, 0.335f),
                b = new Vector2(-0.422f, -0.012f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.223f, -0.111f),
                b = new Vector2(-0.371f, -0.447f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.000f, 0.261f),
                b = new Vector2(0.296f, 0.244f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 2);
        }


        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator FiveSegments3()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.214f, -0.173f),
                b = new Vector2(-0.004f, 0.065f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.347f, 0.478f),
                b = new Vector2(-0.388f, -0.402f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.218f, -0.064f),
                b = new Vector2(-0.244f, -0.427f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.404f, 0.396f),
                b = new Vector2(0.268f, -0.383f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.204f, 0.358f),
                b = new Vector2(0.154f, -0.253f)
            });

            stepper.Init(segments, Color.yellow, 0.001f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 7);
        }


        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator FiveSegments4()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.336f, -0.405f),
                b = new Vector2(-0.380f, -0.106f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.314f, -0.225f),
                b = new Vector2(0.341f, -0.291f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.201f, -0.360f),
                b = new Vector2(-0.258f, 0.201f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.222f, -0.358f),
                b = new Vector2(0.498f, -0.249f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.488f, -0.368f),
                b = new Vector2(0.191f, -0.063f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 7);
        }

        [UnityTest]
        public IEnumerator DividedLine2()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, 0.5f)
            });

            float xPerLine = 1.0f / 17.0f;

            float startX = xPerLine * 0.5f - 0.5f;
            for (int i = 0; i < 16; i++)
            {
                segments.Add(new LineSegment2D()
                {
                    a = new Vector2(startX, -0.5f),
                    b = new Vector2(startX + xPerLine * 0.5f, 0.5f)
                });
                startX += xPerLine;
            }

            stepper.Init(segments, Color.yellow, 0.005f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 16);
        }



        [UnityTest]
        public IEnumerator Grid()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);

            float xPerLine = 1.0f / 9.0f;

            float startX = xPerLine * 0.5f - 0.5f;
            for (int i = 0; i < 8; i++)
            {
                segments.Add(new LineSegment2D()
                {
                    a = new Vector2(startX, -0.5f),
                    b = new Vector2(startX, 0.5f)
                });
                startX += xPerLine;
            }

            float yPerLine = 1.0f / 9.0f;

            float startY = yPerLine * 0.5f - 0.5f;

            for(int i = 0; i < 8; i++)
            {
                segments.Add(new LineSegment2D()
                {
                    a = new Vector2(-0.5f, startY),
                    b = new Vector2(0.5f, startY),
                });
                startY += yPerLine;
            }

            stepper.Init(segments, Color.yellow, 0.005f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 64);
        }

        [UnityTest]
        public IEnumerator DividedLine()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.0f),
                b = new Vector2(0.5f, 0.0f)
            });

            float xPerLine = 1.0f / 5.0f;

            float startX = xPerLine * 0.5f - 0.5f;
            for(int i = 0; i < 4; i++)
            {
                segments.Add(new LineSegment2D()
                {
                    a = new Vector2(startX, -0.3f),
                    b = new Vector2(startX + xPerLine * 0.5f, 0.3f)
                });
                startX += xPerLine;
            }

            stepper.Init(segments, Color.yellow, 0.005f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 4);
        }

        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator EightSegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.427f, 0.314f),
                b = new Vector2(-0.190f, 0.458f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.084f, 0.186f),
                b = new Vector2(-0.240f, 0.021f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.456f, -0.380f),
                b = new Vector2(-0.256f, 0.150f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.294f, 0.342f),
                b = new Vector2(-0.300f, -0.332f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.326f, -0.381f),
                b = new Vector2(-0.186f, 0.071f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.359f, -0.480f),
                b = new Vector2(0.158f, 0.457f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.368f, 0.392f),
                b = new Vector2(-0.260f, 0.144f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.327f, 0.171f),
                b = new Vector2(0.004f, -0.144f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 11);
        }

        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator TenSegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.314f, -0.242f),
                b = new Vector2(0.397f, 0.347f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.201f, -0.385f),
                b = new Vector2(0.187f, -0.157f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.342f, -0.423f),
                b = new Vector2(0.466f, 0.051f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.326f, 0.394f),
                b = new Vector2(-0.166f, -0.143f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.152f, 0.446f),
                b = new Vector2(0.199f, -0.086f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.106f, -0.261f),
                b = new Vector2(0.463f, -0.446f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.305f, -0.036f),
                b = new Vector2(0.430f, 0.001f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.206f, -0.215f),
                b = new Vector2(-0.305f, -0.285f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.259f, 0.307f),
                b = new Vector2(0.319f, 0.293f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.464f, 0.122f),
                b = new Vector2(-0.427f, -0.358f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 10);
        }


        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator FifteenSegments()
        {

            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);

            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.487f, 0.401f),
                b = new Vector2(-0.109f, 0.157f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.143f, -0.348f),
                b = new Vector2(-0.380f, -0.429f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.145f, -0.040f),
                b = new Vector2(0.282f, 0.370f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.029f, 0.260f),
                b = new Vector2(0.494f, 0.297f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.221f, 0.090f),
                b = new Vector2(0.492f, -0.340f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.440f, 0.049f),
                b = new Vector2(-0.462f, 0.215f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.392f, 0.037f),
                b = new Vector2(-0.143f, -0.235f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.258f, 0.152f),
                b = new Vector2(-0.172f, 0.211f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.226f, 0.034f),
                b = new Vector2(-0.065f, -0.316f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.155f, -0.070f),
                b = new Vector2(0.010f, -0.325f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.073f, -0.133f),
                b = new Vector2(0.076f, -0.224f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.063f, -0.226f),
                b = new Vector2(-0.001f, 0.222f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.350f, -0.165f),
                b = new Vector2(-0.028f, -0.271f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.159f, -0.005f),
                b = new Vector2(-0.036f, 0.184f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.218f, 0.193f),
                b = new Vector2(-0.404f, -0.407f),
            });

            stepper.Init(segments, Color.yellow, 0.003f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 12);
        }


        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator FifteenSegments2()
        {

            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);

            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.437f, -0.010f),
                b = new Vector2(0.176f, 0.477f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.446f, 0.170f),
                b = new Vector2(0.117f, -0.327f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.231f, 0.474f),
                b = new Vector2(0.347f, -0.305f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.203f, -0.369f),
                b = new Vector2(0.326f, -0.018f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.408f, -0.332f),
                b = new Vector2(0.047f, -0.063f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.266f, 0.001f),
                b = new Vector2(-0.258f, -0.467f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.412f, -0.414f),
                b = new Vector2(0.371f, 0.238f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.162f, -0.470f),
                b = new Vector2(0.011f, 0.156f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.210f, 0.248f),
                b = new Vector2(-0.465f, -0.093f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.098f, 0.473f),
                b = new Vector2(0.276f, -0.386f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.448f, 0.270f),
                b = new Vector2(-0.382f, -0.422f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.019f, 0.341f),
                b = new Vector2(-0.323f, 0.224f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.449f, 0.193f),
                b = new Vector2(0.306f, 0.231f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.333f, 0.370f),
                b = new Vector2(-0.367f, 0.434f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.094f, 0.494f),
                b = new Vector2(0.076f, 0.386f),
            });


            stepper.Init(segments, Color.yellow, 0.003f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 25);
        }



        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest, Timeout(1000000)]
        public IEnumerator FifteenSegments3()
        {

            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);

            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.211f, 0.445f),
                b = new Vector2(-0.086f, 0.136f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.170f, -0.112f),
                b = new Vector2(0.104f, -0.218f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.191f, 0.437f),
                b = new Vector2(0.445f, 0.470f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.055f, -0.168f),
                b = new Vector2(0.347f, -0.235f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.057f, -0.211f),
                b = new Vector2(-0.346f, 0.189f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.397f, -0.138f),
                b = new Vector2(-0.097f, 0.258f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.371f, 0.406f),
                b = new Vector2(-0.315f, 0.232f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.359f, 0.241f),
                b = new Vector2(-0.199f, -0.125f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.184f, -0.411f),
                b = new Vector2(0.306f, 0.112f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.437f, -0.292f),
                b = new Vector2(0.128f, -0.007f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.022f, -0.076f),
                b = new Vector2(-0.050f, -0.213f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.052f, -0.225f),
                b = new Vector2(0.451f, 0.242f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.102f, 0.257f),
                b = new Vector2(-0.007f, 0.186f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.278f, 0.209f),
                b = new Vector2(0.440f, 0.259f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.227f, 0.351f),
                b = new Vector2(0.246f, -0.172f),
            });


            stepper.Init(segments, Color.yellow, 0.003f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 19);
        }

        


        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest]
        public IEnumerator TwentySegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);


            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.248f, 0.150f),
                b = new Vector2(-0.331f, 0.153f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.146f, -0.151f),
                b = new Vector2(0.465f, -0.219f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.160f, 0.077f),
                b = new Vector2(-0.170f, 0.464f)
            });


            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.189f, 0.131f),
                b = new Vector2(-0.459f, -0.089f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.162f, -0.385f),
                b = new Vector2(-0.057f, -0.283f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.346f, -0.307f),
                b = new Vector2(0.261f, 0.025f)
            });



            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.433f, 0.484f),
                b = new Vector2(0.264f, -0.486f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.176f, -0.017f),
                b = new Vector2(-0.177f, -0.468f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.371f, -0.305f),
                b = new Vector2(0.293f, 0.232f)
            });



            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.080f, 0.364f),
                b = new Vector2(0.070f, -0.376f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.050f, -0.400f),
                b = new Vector2(0.347f, 0.394f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.096f, 0.498f),
                b = new Vector2(0.186f, -0.354f)
            });


            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.309f, 0.012f),
                b = new Vector2(0.474f, 0.269f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.263f, 0.102f),
                b = new Vector2(0.268f, 0.310f)
            });



            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.261f, -0.454f),
                b = new Vector2(-0.213f, 0.475f)
            });

            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.469f, 0.153f),
                b = new Vector2(0.055f, -0.372f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.197f, 0.364f),
                b = new Vector2(-0.208f, -0.224f)
            });

            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.086f, 0.324f),
                b = new Vector2(0.309f, -0.187f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.464f, -0.228f),
                b = new Vector2(0.432f, -0.402f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.379f, 0.455f),
                b = new Vector2(-0.443f, 0.493f)
            });

            stepper.Init(segments, Color.yellow, 0.003f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 53);




        }

        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest, Timeout(10000000)]
        public IEnumerator TwentySegments2()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);


            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.001f, -0.161f),
                b = new Vector2(0.318f, 0.035f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.386f, 0.089f),
                b = new Vector2(-0.194f, -0.464f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.363f, -0.497f),
                b = new Vector2(-0.113f, 0.104f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.112f, 0.228f),
                b = new Vector2(-0.148f, -0.235f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.280f, 0.348f),
                b = new Vector2(0.396f, -0.104f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.281f, -0.257f),
                b = new Vector2(0.174f, 0.005f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.438f, -0.177f),
                b = new Vector2(-0.405f, 0.268f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.133f, 0.440f),
                b = new Vector2(0.061f, 0.005f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.417f, -0.256f),
                b = new Vector2(0.023f, -0.360f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.394f, 0.200f),
                b = new Vector2(0.240f, -0.030f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.403f, 0.122f),
                b = new Vector2(-0.166f, -0.435f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.439f, 0.091f),
                b = new Vector2(0.236f, 0.192f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.426f, 0.361f),
                b = new Vector2(-0.479f, 0.266f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.129f, -0.176f),
                b = new Vector2(-0.390f, -0.300f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.329f, 0.480f),
                b = new Vector2(-0.300f, -0.068f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.463f, 0.078f),
                b = new Vector2(-0.404f, 0.421f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.490f, -0.426f),
                b = new Vector2(-0.395f, 0.407f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.219f, -0.042f),
                b = new Vector2(0.148f, -0.353f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.298f, 0.252f),
                b = new Vector2(0.331f, -0.024f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.373f, 0.258f),
                b = new Vector2(0.164f, -0.341f),
            });

            stepper.Init(segments, Color.yellow, 0.003f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 47);

        }


        //Values were generated from the random test
        //They are included because they produced erronous results
        [UnityTest, Timeout(10000000)]
        public IEnumerator FiftySegments()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);


            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.145f, -0.434f),
                b = new Vector2(0.393f, 0.324f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.431f, -0.479f),
                b = new Vector2(-0.200f, -0.050f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.418f, 0.059f),
                b = new Vector2(-0.211f, -0.396f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.098f, 0.403f),
                b = new Vector2(0.137f, -0.093f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.367f, -0.440f),
                b = new Vector2(0.084f, 0.007f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.016f, -0.208f),
                b = new Vector2(-0.192f, 0.468f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.390f, -0.316f),
                b = new Vector2(0.181f, 0.394f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.424f, -0.437f),
                b = new Vector2(0.257f, -0.368f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.044f, -0.326f),
                b = new Vector2(-0.446f, -0.457f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.420f, 0.089f),
                b = new Vector2(-0.236f, -0.005f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.086f, -0.387f),
                b = new Vector2(0.454f, -0.243f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.304f, -0.290f),
                b = new Vector2(-0.433f, 0.103f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.157f, 0.375f),
                b = new Vector2(0.192f, -0.463f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.366f, -0.375f),
                b = new Vector2(-0.379f, -0.330f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.317f, 0.408f),
                b = new Vector2(0.023f, 0.155f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.399f, 0.032f),
                b = new Vector2(-0.149f, -0.209f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.221f, 0.334f),
                b = new Vector2(0.468f, 0.307f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.496f, 0.499f),
                b = new Vector2(-0.143f, 0.119f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.136f, -0.373f),
                b = new Vector2(-0.185f, 0.115f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.366f, 0.255f),
                b = new Vector2(-0.401f, 0.372f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.331f, -0.234f),
                b = new Vector2(0.070f, -0.171f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.278f, 0.009f),
                b = new Vector2(-0.189f, 0.341f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.312f, -0.036f),
                b = new Vector2(-0.216f, 0.123f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.264f, 0.331f),
                b = new Vector2(0.355f, -0.264f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.360f, -0.021f),
                b = new Vector2(0.496f, 0.102f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.270f, 0.049f),
                b = new Vector2(0.296f, -0.047f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.372f, 0.028f),
                b = new Vector2(-0.324f, -0.090f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.013f, 0.147f),
                b = new Vector2(0.323f, 0.385f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.375f, 0.306f),
                b = new Vector2(-0.355f, 0.074f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.374f, 0.072f),
                b = new Vector2(0.169f, 0.143f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.465f, 0.494f),
                b = new Vector2(-0.181f, 0.218f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.482f, -0.107f),
                b = new Vector2(0.319f, 0.147f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.366f, 0.009f),
                b = new Vector2(-0.124f, -0.097f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.005f, 0.474f),
                b = new Vector2(-0.207f, -0.130f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.202f, 0.330f),
                b = new Vector2(0.137f, 0.332f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.441f, -0.061f),
                b = new Vector2(0.257f, 0.239f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.389f, -0.368f),
                b = new Vector2(-0.207f, 0.326f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.067f, -0.472f),
                b = new Vector2(0.266f, 0.315f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.125f, 0.384f),
                b = new Vector2(-0.265f, -0.056f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.326f, -0.334f),
                b = new Vector2(0.012f, 0.418f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.038f, 0.022f),
                b = new Vector2(0.162f, 0.488f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.262f, -0.394f),
                b = new Vector2(-0.179f, 0.403f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.213f, 0.143f),
                b = new Vector2(-0.328f, 0.426f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.072f, -0.014f),
                b = new Vector2(-0.120f, 0.437f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.062f, 0.430f),
                b = new Vector2(-0.206f, 0.296f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.088f, 0.186f),
                b = new Vector2(0.103f, 0.147f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.078f, -0.377f),
                b = new Vector2(0.276f, -0.195f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.008f, -0.229f),
                b = new Vector2(-0.453f, 0.468f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.283f, -0.222f),
                b = new Vector2(0.486f, -0.320f),
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.216f, -0.306f),
                b = new Vector2(0.458f, -0.152f),
            });


            stepper.Init(segments, Color.yellow, 0.003f);

            while (!stepper.IsDone())
            {
                stepper.Step();
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 343);
        }



        [UnityTest]
        public IEnumerator CrossLadder()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, -0.3f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, -0.3f),
                b = new Vector2(-0.5f, -0.1f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.1f),
                b = new Vector2(0.5f, 0.1f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, 0.1f),
                b = new Vector2(-0.5f, 0.3f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.3f),
                b = new Vector2(0.5f, 0.5f)
            });


            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, -0.5f),
                b = new Vector2(-0.5f, -0.3f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.3f),
                b = new Vector2(0.5f, -0.1f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, -0.1f),
                b = new Vector2(-0.5f, 0.1f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.1f),
                b = new Vector2(0.5f, 0.3f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, 0.3f),
                b = new Vector2(-0.5f, 0.5f)
            });

            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, 0.5f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, -0.5f),
                b = new Vector2(-0.5f, 0.5f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 29);
        }

        [UnityTest]
        public IEnumerator Ladder()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, -0.3f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, -0.3f),
                b = new Vector2(-0.5f, -0.1f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.1f),
                b = new Vector2(0.5f, 0.1f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, 0.1f),
                b = new Vector2(-0.5f, 0.3f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.3f),
                b = new Vector2(0.5f, 0.5f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 4);
        }


        [UnityTest]
        public IEnumerator DisjointLadder()
        {
            RuntimeTestUtility.CreateCamera();

            var lineSegmentGO = new GameObject("Line Segment Generator");

            var stepper = lineSegmentGO.AddComponent<LineSegmentIntersectionStepper>();

            NativeList<LineSegment2D> segments = new NativeList<LineSegment2D>(Allocator.Persistent);
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.5f),
                b = new Vector2(0.5f, -0.35f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, -0.3f),
                b = new Vector2(-0.5f, -0.15f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, -0.1f),
                b = new Vector2(0.5f, 0.05f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(0.5f, 0.1f),
                b = new Vector2(-0.5f, 0.25f)
            });
            segments.Add(new LineSegment2D()
            {
                a = new Vector2(-0.5f, 0.3f),
                b = new Vector2(0.5f, 0.5f)
            });

            stepper.Init(segments, Color.yellow, 0.01f);

            while (!stepper.IsDone())
            {
                yield return null;
            }

            var intersections = stepper.Intersections;

            Assert.IsTrue(intersections.Length == 0);
        }
    }
}
