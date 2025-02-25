using NUnit.Framework;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public class LineIntersectionTest
    {
        [Test]
        public void ParallelLines()
        {
            var l0 = new Line3D(float3.zero, new float3(1, 0, 0));
            var l1 = new Line3D(new float3(0, 1, 0), new float3(1, 0, 0));

            Assert.IsTrue(!ShapeIntersection.LineIntersection(l0, l1, out _));
        }

        [Test]
        public void SkewedLines()
        {
            var l0 = new Line3D(float3.zero, new float3(1, 0, 0));
            var l1 = new Line3D(new float3(0, 1, 0), new float3(0, 0, 1));

            Assert.IsTrue(!ShapeIntersection.LineIntersection(l0, l1, out _));
        }

        [Test]
        public void Intersection1()
        {
            var l0 = new Line3D(float3.zero, new float3(1, 0, 0));
            var l1 = new Line3D(float3.zero, new float3(0, 0, 1));

            Assert.IsTrue(ShapeIntersection.LineIntersection(l0, l1, out _));
        }

        [Test]
        public void Intersection2()
        {
            var l0 = new Line3D(float3.zero, new float3(1, 0, 0));
            var l1 = new Line3D(new float3(5, 0, 0), new float3(0, 0, 1));

            Assert.IsTrue(ShapeIntersection.LineIntersection(l0, l1, out float3 intersection));
            Assert.IsTrue(intersection.x == 5.0f);
        }

        [Test]
        public void Intersection3()
        {
            var l0 = new Line3D(float3.zero, new float3(1, 1, 0));
            var l1 = new Line3D(new float3(5, 5, 0), new float3(0, 0, 1));

            Assert.IsTrue(ShapeIntersection.LineIntersection(l0, l1, out float3 intersection));
            Assert.IsTrue(intersection.x == 5.0f);
            Assert.IsTrue(intersection.y == 5.0f);
            Assert.IsTrue(intersection.z == 0.0f);
        }
    }
}
