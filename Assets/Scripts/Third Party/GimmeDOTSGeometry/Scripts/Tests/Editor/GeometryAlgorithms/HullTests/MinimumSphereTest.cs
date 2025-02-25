using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class MinimumSphereTest
    {
        [Test]
        public void EmptySphere()
        {
            var points = new NativeArray<float3>(0, Allocator.TempJob);

            var center = new NativeReference<float3>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            Assert.IsTrue(math.all(center.Value == 0.0f));
            Assert.IsTrue(radius.Value == 0.0f);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }


        [Test]
        public void SinglePoint()
        {
            var points = new NativeArray<float3>(1, Allocator.TempJob);
            var point = new float3(2.0f, 3.0f, 4.0f);
            points[0] = point;

            var center = new NativeReference<float3>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            Assert.IsTrue(math.all(center.Value == point));
            Assert.IsTrue(radius.Value == 0.0f);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }


        [Test]
        public void TwoPoints()
        {
            var points = new NativeArray<float3>(2, Allocator.TempJob);
            var point0 = new float3(-4.0f, 0.0f, 0.0f);
            var point1 = new float3(4.0f, 0.0f, 0.0f);
            points[0] = point0;
            points[1] = point1;

            var center = new NativeReference<float3>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            Assert.IsTrue(math.all(center.Value == float3.zero));
            Assert.IsTrue(radius.Value == 4.0f);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }

        [Test]
        public void ThreePoints()
        {
            var points = new NativeArray<float3>(3, Allocator.TempJob);
            var point0 = new float3(-4.0f, 0.0f, 0.0f);
            var point1 = new float3(4.0f, 0.0f, 0.0f);
            var point2 = new float3(0.0f, 0.0f, 0.0f);

            points[0] = point0;
            points[1] = point1;
            points[2] = point2;

            var center = new NativeReference<float3>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            Assert.IsTrue(math.all(center.Value == float3.zero));
            Assert.IsTrue(radius.Value == 4.0f);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }

        [Test]
        public void EquilateralTriangle()
        {
            var points = new NativeArray<float3>(3, Allocator.TempJob);
            var point0 = new float3(-4.0f, 0.0f, 0.0f);
            var point1 = new float3(4.0f, 0.0f, 0.0f);
            var point2 = new float3(0.0f, Mathf.Sin(Mathf.PI / 3) * 8.0f, 0.0f);

            points[0] = point0;
            points[1] = point1;
            points[2] = point2;

            var center = new NativeReference<float3>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            float3 expectedCenter = (point0 + point1 + point2) / 3.0f;
            float expectedRadius = math.length(point0 - expectedCenter);

            Assert.IsTrue(center.Value.x == expectedCenter.x);
            Assert.IsTrue(center.Value.y == expectedCenter.y);
            Assert.IsTrue(center.Value.z == expectedCenter.z);
            Assert.IsTrue(radius.Value == expectedRadius);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }


        [Test]
        public void EquilateralTetrahedon()
        {
            var points = new NativeArray<float3>(4, Allocator.TempJob);
            var point0 = new float3(-4.0f, 0.0f, 0.0f);
            var point1 = new float3(4.0f, 0.0f, 0.0f);
            var point2 = new float3(0.0f, 0.0f, Mathf.Sin(Mathf.PI / 3) * 8.0f);

            var tetraCenter = (point0 + point1 + point2) / 3.0f;
            float distToCenter = math.distance(tetraCenter, point0);
            var point3 = new float3(tetraCenter.x, math.sqrt(8.0f * 8.0f - distToCenter * distToCenter), tetraCenter.z);


            points[0] = point0;
            points[1] = point1;
            points[2] = point2;
            points[3] = point3;

            var center = new NativeReference<float3>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingSphereJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            float3 expectedCenter = (point0 + point1 + point2 + point3) / 4.0f;
            float expectedRadius = math.length(point0 - expectedCenter);

            Assert.IsTrue(math.abs(center.Value.x - expectedCenter.x) < 10e-5f);
            Assert.IsTrue(math.abs(center.Value.y - expectedCenter.y) < 10e-5f);
            Assert.IsTrue(math.abs(center.Value.y - expectedCenter.y) < 10e-5f);
            Assert.IsTrue(radius.Value == expectedRadius);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }
    }
}
