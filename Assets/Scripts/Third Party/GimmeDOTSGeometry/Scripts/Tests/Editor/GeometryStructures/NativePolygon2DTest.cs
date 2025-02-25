using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class NativePolygon2DTest
    {
        [Test]
        public void Convexity()
        {
            var poly0 = Polygon2DGeneration.Regular(Allocator.TempJob, Vector2.zero, 1.0f, 4);

            Assert.IsTrue(poly0.IsConvex());

            var poly1 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(1.0f, 0.0f),
                new float2(0.0f, 0.5f),
                new float2(1.0f, 1.0f),
                new float2(0.0f, 1.0f),
                new float2(-1.0f, 0.0f),
                new float2(0.0f, -1.0f)
            });

            Assert.IsTrue(!poly1.IsConvex());

            var poly2 = new NativePolygon2D(Allocator.TempJob, new float2[] { });

            //Strictly speaking -- each point of the polygon is visible from each other point... there just is no point... to this
            Assert.IsTrue(poly2.IsConvex());

            poly0.Dispose();
            poly1.Dispose();
            poly2.Dispose();
        }

        [Test]
        public void RemoveColinearPoints0()
        {
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(0.0f, 0.0f),
                new float2(0.25f, 0.0f),
                new float2(0.5f, 0.0f),
                new float2(0.75f, 0.0f),
                new float2(1.0f, 0.0f),
                new float2(0.0f, 1.0f),
            });

            var newPoly = NativePolygon2D.RemoveColinearVertices(Allocator.TempJob, poly0);

            Assert.IsTrue(newPoly.points.Length < poly0.points.Length);
            Assert.IsTrue(newPoly.points.Length == 3);

            poly0.Dispose();
            newPoly.Dispose();
        }

        [Test]
        public void RemoveColinearPoints1()
        {
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(0.0f, 0.0f),
                new float2(0.25f, 0.0f),
                new float2(0.5f, 0.0f),
                new float2(0.75f, 0.0f),
                new float2(1.0f, 0.0f),
                new float2(0.0f, 1.0f),

                new float2(0.1f, 0.1f),
                new float2(0.2f, 0.1f),
                new float2(0.3f, 0.1f),
                new float2(0.3f, 0.3f)
            },
            new int[]
            {
                6,
            });

            var newPoly = NativePolygon2D.RemoveColinearVertices(Allocator.TempJob, poly0);

            Assert.IsTrue(newPoly.points.Length < poly0.points.Length);
            Assert.IsTrue(newPoly.points.Length == 6);

            poly0.Dispose();
            newPoly.Dispose();
        }

        [Test]
        public void PointInPolygonParallelTest1()
        {
            //Polygon has a line from below on the X-Axis, on the right side, to test for the degenerate case,
            //when the point we check is precisely on that line
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.0f), //Horizontal Line
                new float2(1.0f, 0.0f), //Horizontal Line
                new float2(1.0f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygonParallel(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();
        }

        [Test]
        public void PointInPolygonParallelTest2()
        {
            //Polygon has TWO lines from below on the X-Axis, on the right side, to test for the degenerate case,
            //when the point we check is precisely on one of those lines
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.0f), //Horizontal Line 0
                new float2(1.0f, 0.0f), //Horizontal Line 0
                new float2(1.0f, -0.5f),
                new float2(1.5f, -0.5f),
                new float2(1.5f, 0.0f), //Horizontal Line 1
                new float2(2f, 0.0f), //Horizontal Line 1
                new float2(1f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygonParallel(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();
        }

        [Test]
        public void PointInPolygonParallelTest3()
        {
            //Polygon has a line from above on the X-Axis, on the right side, to test for the degenerate case,
            //when the point we check is precisely on that line
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(1.5f, -0.5f),
                new float2(1.5f, 0.5f),
                new float2(1.0f, 0.0f), //Horizontal Line
                new float2(0.5f, 0.0f), //Horizontal Line
                new float2(0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygonParallel(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();
        }

        [Test]
        public void PointInPolygonParallelTest4()
        {
            //Polygon has a line from above and one from below on the X-Axis, on the right side
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(2.5f, -0.5f),
                new float2(2.5f, 0.5f),
                new float2(2.0f, 0.0f), //Horizontal Line (above)
                new float2(1.5f, 0.0f), //Horizontal Line (above)
                new float2(1.5f, -0.25f),
                new float2(1.0f, -0.25f),
                new float2(1.0f, 0.0f), //Horizontal Line (below)
                new float2(0.5f, 0.0f), //Horizontal Line (below)
                new float2(-0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygonParallel(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();
        }



        [Test]
        public void PointInPolygonTest1()
        {
            //Polygon has a line from below on the X-Axis, on the right side, to test for the degenerate case,
            //when the point we check is precisely on that line
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.0f), //Horizontal Line
                new float2(1.0f, 0.0f), //Horizontal Line
                new float2(1.0f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygon(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();

        }

        [Test]
        public void PointInPolygonTest2()
        {
            //Polygon has TWO lines from below on the X-Axis, on the right side, to test for the degenerate case,
            //when the point we check is precisely on one of those lines
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.0f), //Horizontal Line 0
                new float2(1.0f, 0.0f), //Horizontal Line 0
                new float2(1.0f, -0.5f),
                new float2(1.5f, -0.5f),
                new float2(1.5f, 0.0f), //Horizontal Line 1
                new float2(2f, 0.0f), //Horizontal Line 1
                new float2(1f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygon(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();

        }

        [Test]
        public void PointInPolygonTest3()
        {
            //Polygon has a line from above on the X-Axis, on the right side, to test for the degenerate case,
            //when the point we check is precisely on that line
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(1.5f, -0.5f),
                new float2(1.5f, 0.5f),
                new float2(1.0f, 0.0f), //Horizontal Line
                new float2(0.5f, 0.0f), //Horizontal Line
                new float2(0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygon(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();

        }

        [Test]
        public void PointInPolygonTest4()
        {
            //Polygon has a line from above and one from below on the X-Axis, on the right side
            var poly0 = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(2.5f, -0.5f),
                new float2(2.5f, 0.5f),
                new float2(2.0f, 0.0f), //Horizontal Line (above)
                new float2(1.5f, 0.0f), //Horizontal Line (above)
                new float2(1.5f, -0.25f),
                new float2(1.0f, -0.25f),
                new float2(1.0f, 0.0f), //Horizontal Line (below)
                new float2(0.5f, 0.0f), //Horizontal Line (below)
                new float2(-0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var queryPoints = new NativeArray<float2>(1, Allocator.TempJob);
            queryPoints[0] = new float2(0.0f, 0.0f);

            var results = new NativeArray<bool>(1, Allocator.TempJob);

            var parallelJob = Polygon2DPointLocation.ArePointsInPolygon(poly0, queryPoints, ref results);
            parallelJob.Complete();

            Assert.IsTrue(results[0]);

            poly0.Dispose();
            queryPoints.Dispose();
            results.Dispose();

        }

        [Test]
        public void AreaTest0()
        {
            var poly = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            Assert.IsTrue(poly.Area() == 1.0f);

            poly.Dispose();
        }

        [Test]
        public void AreaTest1()
        {
            var poly = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            var hole = new List<Vector2>()
            {
                new Vector2(-0.25f, -0.25f),
                new Vector2(0.25f, -0.25f),
                new Vector2(0.25f, 0.25f),
                new Vector2(-0.25f, 0.25f),
            };

            poly.AddHole(hole);

            Assert.IsTrue(poly.Area() == 0.75f);

            poly.Dispose();
        }



        [Test]
        public void AreaTest2()
        {
            var poly = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(-0.5f, -0.5f),
                new float2(0.5f, -0.5f),
                new float2(0.5f, 0.5f),
                new float2(-0.5f, 0.5f),
            });

            //Area of triangle: 0.045
            var hole0 = new List<Vector2>()
            {
                new Vector2(-0.4f, -0.4f),
                new Vector2(-0.1f, -0.4f),
                new Vector2(-0.1f, -0.1f),
            };

            //Area of square: 0.09
            var hole1 = new List<Vector2>()
            {
                new Vector2(0.1f, 0.1f),
                new Vector2(0.4f, 0.1f),
                new Vector2(0.4f, 0.4f),
                new Vector2(0.1f, 0.4f)
            };

            poly.AddHole(hole0);
            poly.AddHole(hole1);

            Assert.IsTrue(Mathf.Abs(poly.Area() - 0.865f) < 0.001f);

            poly.Dispose();
        }

        [Test]
        public void FlipTest0()
        {
            var poly = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(0.0f, 0.0f),
                new float2(0.5f, 0.0f),
                new float2(0.25f, 0.25f),
            });

            var flippedPoly = NativePolygon2D.Flip(Allocator.TempJob, poly);

            Assert.IsTrue(math.all(flippedPoly.points[0] == poly.points[2]));
            Assert.IsTrue(math.all(flippedPoly.points[1] == poly.points[1]));
            Assert.IsTrue(math.all(flippedPoly.points[2] == poly.points[0]));

            poly.Dispose();
            flippedPoly.Dispose();
        }

        [Test]
        public void FlipTest1()
        {
            var poly = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(0.0f, 0.0f),
                new float2(1.0f, 0.0f),
                new float2(0.5f, 1.0f),
                new float2(0.25f, 0.1f),
                new float2(0.75f, 0.1f),
                new float2(0.5f, 0.2f),
            }, new int[]
            {
                3,
            });

            var flippedPoly = NativePolygon2D.Flip(Allocator.TempJob, poly);

            Assert.IsTrue(math.all(flippedPoly.points[0] == poly.points[2]));
            Assert.IsTrue(math.all(flippedPoly.points[1] == poly.points[1]));
            Assert.IsTrue(math.all(flippedPoly.points[2] == poly.points[0]));

            Assert.IsTrue(math.all(flippedPoly.points[3] == poly.points[5]));
            Assert.IsTrue(math.all(flippedPoly.points[4] == poly.points[4]));
            Assert.IsTrue(math.all(flippedPoly.points[5] == poly.points[3]));


            poly.Dispose();
            flippedPoly.Dispose();
        }

        [Test]
        public void FlipTest2()
        {
            //Not a valid polygon - just for testing purposes
            var poly = new NativePolygon2D(Allocator.TempJob, new float2[]
            {
                new float2(0.0f, 0.0f),
                new float2(1.0f, 0.0f),
                new float2(0.5f, 1.0f),
                new float2(0.25f, 0.1f),
                new float2(0.75f, 0.1f),
                new float2(0.5f, 0.2f),
                new float2(0.0f, 0.1f),
                new float2(0.0f, 0.2f),
                new float2(0.0f, 0.3f),
            }, new int[]
            {
                3,
                6,
            });

            var flippedPoly = NativePolygon2D.Flip(Allocator.TempJob, poly);

            Assert.IsTrue(math.all(flippedPoly.points[0] == poly.points[2]));
            Assert.IsTrue(math.all(flippedPoly.points[1] == poly.points[1]));
            Assert.IsTrue(math.all(flippedPoly.points[2] == poly.points[0]));

            Assert.IsTrue(math.all(flippedPoly.points[3] == poly.points[5]));
            Assert.IsTrue(math.all(flippedPoly.points[4] == poly.points[4]));
            Assert.IsTrue(math.all(flippedPoly.points[5] == poly.points[3]));

            Assert.IsTrue(math.all(flippedPoly.points[6] == poly.points[8]));
            Assert.IsTrue(math.all(flippedPoly.points[7] == poly.points[7]));
            Assert.IsTrue(math.all(flippedPoly.points[8] == poly.points[6]));

            poly.Dispose();
            flippedPoly.Dispose();
        }
    }
}
