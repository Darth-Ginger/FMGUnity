using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class LineSegmentTriangleIntersectionTest
    {
        [Test]
        public void NoIntersection()
        {
            var triangle = new NativeTriangle3D(
                new float3(0.0f, 1.0f, 0.0f),
                new float3(1.0f, 1.0f, 0.0f),
                new float3(0.5f, 1.0f, 0.5f));

            var ray = new LineSegment3D(
                new Vector3(5.0f, 5.0f, 0.0f),
                new Vector3(5.0f, -5.0f, 0.0f));

            bool intersect = ShapeIntersection.LineSegmentTriangleIntersection(ray, triangle, out _);

            Assert.IsTrue(!intersect);
        }

        [Test]
        public void Intersecting()
        {
            var triangle = new NativeTriangle3D(
                new float3(0.0f, 1.0f, 0.0f),
                new float3(1.0f, 1.0f, 0.0f),
                new float3(0.5f, 1.0f, 0.5f));

            var ray = new LineSegment3D(
                new Vector3(0.2f, 5.0f, 0.2f),
                new Vector3(0.2f, -5.0f, 0.2f));

            bool intersect = ShapeIntersection.LineSegmentTriangleIntersection(ray, triangle, out _);

            Assert.IsTrue(intersect);
        }
    }
}
