using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class VolumeAndAreaTest
    {
        [Test]
        public void BoxArea()
        {
            var box = new Bounds(Vector3.zero, new Vector3(3.0f, 4.0f, 5.0f));

            var area = box.Area();

            Assert.IsTrue(area == (3 * 4 * 2 + 4 * 5 * 2 + 3 * 5 * 2));

        }


        [Test]
        public void TriangleArea2D()
        {
            //Right-Angled
            var triangle = new NativeTriangle2D(new float2(0, 0), new float2(4, 0), new float2(4, 3));

            Assert.IsTrue(triangle.HasRightAngle(10e-5f));

            var area = triangle.Area();

            Assert.IsTrue(math.abs(area - 6.0f) < 10e-5f);

            var halfHeightTriangle = triangle;
            halfHeightTriangle.c = new float2(4, 1.5f);

            var halfArea = halfHeightTriangle.Area();

            Assert.IsTrue(math.abs(halfArea - 3.0f) < 10e-5f);
        }

        [Test]
        public void TriangleArea3D()
        {

            //Right-Angled
            var triangle = new NativeTriangle3D(float3.zero, new float3(4, 0, 0), new float3(4, 0, 3));

            Assert.IsTrue(triangle.HasRightAngle(10e-5f));

            var area = triangle.Area();

            Assert.IsTrue(math.abs(area - 6.0f) < 10e-5f);

            var halfHeightTriangle = triangle;
            halfHeightTriangle.c = new float3(4, 0, 1.5f);

            var halfArea = halfHeightTriangle.Area();

            Assert.IsTrue(math.abs(halfArea - 3.0f) < 10e-5f);
        }


        [Test]
        public void TorusArea()
        {
            var torus = new Torus(1.0f / math.PI, 5.0f / math.PI);

            float area = torus.Area();

            Assert.IsTrue(math.abs(area - 2 * 1.0f * 2 * 5.0f) < 10e-5f);
        }


        [Test]
        public void TorusVolume()
        {
            var torus = new Torus(1.0f / math.sqrt(math.PI), 5.0f / math.PI);

            float volume = torus.Volume();

            Assert.IsTrue(math.abs(volume - 1.0f * 2 * 5.0f) < 10e-5f);
        }

        [Test]
        public void TetrahedronArea()
        {
            
            //Corner Tetrahedron of size sqrt(2)
            var tetrahedron = new Tetrahedron(float3.zero,
                new float3(math.SQRT2, 0, 0),
                new float3(math.SQRT2, 0, math.SQRT2),
                new float3(math.SQRT2, math.SQRT2, 0));

            var area = tetrahedron.Area();

            float triangleArea = 1.0f;
            float hypothenuseTriangleArea = (2 * Mathf.Sin(Mathf.PI / 3) * 2) / 2.0f;

            Assert.IsTrue(math.abs(area - 3 * triangleArea - hypothenuseTriangleArea) < 10e-5f);

            //Equilateral Tetrahedron. Each side has length 8

            var point0 = new float3(-4.0f, 0.0f, 0.0f);
            var point1 = new float3(4.0f, 0.0f, 0.0f);
            var point2 = new float3(0.0f, 0.0f, Mathf.Sin(Mathf.PI / 3) * 8.0f);

            var tetraCenter = (point0 + point1 + point2) / 3.0f;
            float distToCenter = math.distance(tetraCenter, point0);
            var point3 = new float3(tetraCenter.x, math.sqrt(8.0f * 8.0f - distToCenter * distToCenter), tetraCenter.z);


            var equilateralTetrahedron = new Tetrahedron(point0, point1, point2, point3);

            area = equilateralTetrahedron.Area();

            float triangleHeight = Mathf.Sin(Mathf.PI / 3) * 8.0f;
            triangleArea = (8 * triangleHeight) / 2.0f;

            Assert.IsTrue(math.abs(area - 4 * triangleArea) < 10e-5f);
        }

        [Test]
        public void TetrahedronVolume()
        {
            float root3 = math.pow(2, 1.0f / 3.0f);

            //Corner Tetrahedron. Each side has length third root of 2
            var tetrahedron = new Tetrahedron(float3.zero,
                new float3(root3, 0, 0),
                new float3(root3, 0, root3),
                new float3(root3, root3, 0));

            float volume = tetrahedron.Volume();

            //Base Triangle Area = root3 * root3 * 0.5f
            //Height of Tetrahedron = root3
            //Result should be 1 / 3
            float expectedVolume = (((root3 * root3) / 2.0f) * root3) / 3.0f;

            Assert.IsTrue(math.abs(volume - expectedVolume) < 10e-5f);

            //Equilateral Tetrahedron. Each side has length 8

            var point0 = new float3(-4.0f, 0.0f, 0.0f);
            var point1 = new float3(4.0f, 0.0f, 0.0f);
            var point2 = new float3(0.0f, 0.0f, Mathf.Sin(Mathf.PI / 3) * 8.0f);

            var tetraCenter = (point0 + point1 + point2) / 3.0f;
            float distToCenter = math.distance(tetraCenter, point0);
            var point3 = new float3(tetraCenter.x, math.sqrt(8.0f * 8.0f - distToCenter * distToCenter), tetraCenter.z);

            var equilateralTetrahedron = new Tetrahedron(point0, point1, point2, point3);

            volume = equilateralTetrahedron.Volume();

            float triangleHeight = Mathf.Sin(Mathf.PI / 3) * 8.0f;
            float triangleArea = (8 * triangleHeight) / 2.0f;

            float tetrahedronHeight = point3.y;

            expectedVolume = (triangleArea * tetrahedronHeight) / 3.0f;

            Assert.IsTrue(math.abs(volume - expectedVolume) < 10e-5f);
        }



    }
}
