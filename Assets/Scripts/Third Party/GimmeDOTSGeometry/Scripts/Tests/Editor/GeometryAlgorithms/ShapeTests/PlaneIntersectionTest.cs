using NUnit.Framework;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class PlaneIntersectionTest
    {
        [Test]
        public void ParallelPlanes()
        {
            var p0 = new Plane(Vector3.one, 0.0f);
            var p1 = new Plane(Vector3.one, 1.0f);

            Assert.IsTrue(!ShapeIntersection.PlaneIntersection(p0, p1, out _));
        }

        [Test]
        public void Intersection1()
        {
            var p0 = new Plane(Vector3.up, 0.0f);
            var p1 = new Plane(Vector3.left, 1.0f);

            Assert.IsTrue(ShapeIntersection.PlaneIntersection(p0, p1, out var line));
            Assert.IsTrue(line.direction.z != 0.0f);
        }

        [Test]
        public void Intersection2()
        {
            var p0 = new Plane(Vector3.one, 0.0f);
            var p1 = new Plane(new Vector3(-1.0f, 1.0f, 1.0f), 0.0f);

            Assert.IsTrue(ShapeIntersection.PlaneIntersection(p0, p1, out var line));
            Assert.IsTrue(line.direction.z != 0.0f);
            Assert.IsTrue(line.direction.y != 0.0f);
            Assert.IsTrue(line.direction.x == 0.0f);
        }

        [Test]
        public void Intersection3()
        {
            var p0 = new Plane(Vector3.up, 5.0f);
            var p1 = new Plane(Vector3.right, 5.0f);

            Assert.IsTrue(ShapeIntersection.PlaneIntersection(p0, p1, out var line));
            Assert.IsTrue(line.point.x == 5.0f);
            Assert.IsTrue(line.point.y == 5.0f);
        }
    }
}
