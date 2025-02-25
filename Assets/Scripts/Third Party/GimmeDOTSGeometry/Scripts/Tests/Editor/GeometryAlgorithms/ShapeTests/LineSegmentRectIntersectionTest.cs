using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class LineSegmentRectIntersectionTest 
    {
        [Test]
        public void NoIntersections()
        {
            var r = new Rect(0, 0, 3, 3);

            var ls0 = new LineSegment2D(new Vector2(-5, -5), new Vector2(-5, 5));
            var ls1 = new LineSegment2D(new Vector2(-5, 0), new Vector2(1.5f, 15));
            var ls2 = new LineSegment2D(new Vector2(1, 1), new Vector2(2, 2));

            int intersections0 = ShapeIntersection.LineSegmentRectangleIntersections(ls0, r, out _, out _, out _);
            int intersections1 = ShapeIntersection.LineSegmentRectangleIntersections(ls1, r, out _, out _, out _);
            int intersections2 = ShapeIntersection.LineSegmentRectangleIntersections(ls2, r, out _, out _, out var inside);

            Assert.IsTrue(intersections0 == 0);
            Assert.IsTrue(intersections1 == 0);
            Assert.IsTrue(intersections2 == 0);
            Assert.IsTrue(inside);
        }

        [Test]
        public void OneIntersection()
        {
            var r = new Rect(-2, -2, 4, 4);

            var ls0 = new LineSegment2D(new Vector2(0, 0), new Vector2(5, 0));
            var ls1 = new LineSegment2D(new Vector2(-1, -1), new Vector2(8, 10));

            int intersections0 = ShapeIntersection.LineSegmentRectangleIntersections(ls0, r, out var i0, out _, out _);
            int intersections1 = ShapeIntersection.LineSegmentRectangleIntersections(ls1, r, out var i1, out _, out _);

            Assert.IsTrue(intersections0 == 1);
            Assert.IsTrue(intersections1 == 1);
            Assert.IsTrue(math.all(i0 == new float2(2, 0)));
        }

        [Test]
        public void TwoIntersections()
        {
            var r = new Rect(-1, -1, 2, 2);

            var ls0 = new LineSegment2D(new Vector2(-1.5f, -1.5f), new Vector2(0.0f, 1.5f));
            var ls1 = new LineSegment2D(new Vector2(-1.5f, 0.0f), new Vector2(1.5f, 0.0f));
            var ls2 = new LineSegment2D(new Vector2(-1.5f, -1.5f), new Vector2(1.5f, 1.5f));
            var ls3 = new LineSegment2D(new Vector2(-2.0f, 0.0f), new Vector2(4.0f, 2.0f));

            int intersections0 = ShapeIntersection.LineSegmentRectangleIntersections(ls0, r, out _, out _, out _);
            int intersections1 = ShapeIntersection.LineSegmentRectangleIntersections(ls1, r, out var i0, out var i1, out _);
            int intersections2 = ShapeIntersection.LineSegmentRectangleIntersections(ls2, r, out var i2, out var i3, out _);
            int intersections3 = ShapeIntersection.LineSegmentRectangleIntersections(ls3, r, out var i4, out var i5, out _);

            Assert.IsTrue(intersections0 == 2);
            Assert.IsTrue(intersections1 == 2);
            Assert.IsTrue(math.all(i0 + i1 == float2.zero));

            Assert.IsTrue(intersections2 == 2);
            Assert.IsTrue(intersections3 == 2);
        }
    }
}
