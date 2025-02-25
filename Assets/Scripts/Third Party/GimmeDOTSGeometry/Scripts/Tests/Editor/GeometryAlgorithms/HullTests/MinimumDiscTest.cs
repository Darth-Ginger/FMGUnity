using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class MinimumDiscTest
    {
        [Test]
        public void EmptyDisc()
        {
            var points = new NativeArray<float2>(0, Allocator.TempJob);

            var center = new NativeReference<float2>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
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
            var points = new NativeArray<float2>(1, Allocator.TempJob);
            var point = new float2(3.0f, 4.0f);
            points[0] = point;

            var center = new NativeReference<float2>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
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
            var points = new NativeArray<float2>(2, Allocator.TempJob);
            var point0 = new float2(0.0f, 4.0f);
            var point1 = new float2(8.0f, 4.0f);
            points[0] = point0;
            points[1] = point1;

            var center = new NativeReference<float2>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            Assert.IsTrue(center.Value.x == 4.0f);
            Assert.IsTrue(center.Value.y == 4.0f);
            Assert.IsTrue(radius.Value == 4.0f);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }


        [Test]
        public void EquilateralTriangle()
        {
            var points = new NativeArray<float2>(3, Allocator.TempJob);

            //Equilateral Triangle
            var point0 = new float2(-4.0f, 0.0f);
            var point1 = new float2(4.0f, 0.0f);
            var point2 = new float2(0.0f, Mathf.Sin(Mathf.PI / 3.0f) * 8.0f);

            points[0] = point0;
            points[1] = point1;
            points[2] = point2;

            var center = new NativeReference<float2>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            //As it is an equilateral triangle, the barycenter of the triangle coincides with the
            //center of the circumcircle of the triangle

            float2 expectedCenter = (point0 + point1 + point2) / 3.0f;
            float expectedRadius = math.length(point0 - expectedCenter);

            Assert.IsTrue(center.Value.x == expectedCenter.x);
            Assert.IsTrue(center.Value.y == expectedCenter.y);
            Assert.IsTrue(radius.Value == expectedRadius);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }

        [Test]
        public void Colinear()
        {
            var points = new NativeArray<float2>(3, Allocator.TempJob);

            var point0 = new float2(-4.0f, 0.0f);
            var point1 = new float2(0.0f, 0.0f);
            var point2 = new float2(4.0f, 0.0f);

            points[0] = point0;
            points[1] = point1;
            points[2] = point2;

            var center = new NativeReference<float2>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            float2 expectedCenter = float2.zero;
            float expectedRadius = 4.0f;

            Assert.IsTrue(center.Value.x == expectedCenter.x);
            Assert.IsTrue(center.Value.y == expectedCenter.y);
            Assert.IsTrue(radius.Value == expectedRadius);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }

        [Test]
        public void Square()
        {
            var points = new NativeArray<float2>(4, Allocator.TempJob);

            var point0 = new float2(-4.0f, -4.0f);
            var point1 = new float2(4.0f, -4.0f);
            var point2 = new float2(4.0f, 4.0f);
            var point3 = new float2(-4.0f, 4.0f);

            points[0] = point0;
            points[1] = point1;
            points[2] = point2;
            points[3] = point3;

            var center = new NativeReference<float2>(Allocator.TempJob);
            var radius = new NativeReference<float>(Allocator.TempJob);

            var minDiscJob = new HullAlgorithmJobs.MinimumEnclosingDiskJob()
            {
                points = points,
                center = center,
                radius = radius,
            };
            minDiscJob.Schedule().Complete();

            float2 expectedCenter = float2.zero;
            float expectedRadius = 4.0f * Mathf.Sqrt(2.0f);

            Assert.IsTrue(center.Value.x == expectedCenter.x);
            Assert.IsTrue(center.Value.y == expectedCenter.y);
            Assert.IsTrue(radius.Value == expectedRadius);

            points.Dispose();
            center.Dispose();
            radius.Dispose();
        }
    }
}
