using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class BoundingRectAndBoxJobTest
    {


        [Test]
        public void EmptyRect()
        {
            var points = new NativeArray<float2>(0, Allocator.TempJob);
            var rectRef = new NativeReference<Rect>(Allocator.TempJob);

            var minRectJob = HullAlgorithms.CalculateBoundingRect(points, rectRef);

            minRectJob.Complete();

            var rect = rectRef.Value;
            Assert.IsTrue(float.IsNaN(rect.width));
            Assert.IsTrue(float.IsNaN(rect.height));

            points.Dispose();
            rectRef.Dispose();
        }

        [Test]
        public void SinglePointRect()
        {
            var points = new NativeArray<float2>(1, Allocator.TempJob);
            points[0] = new float2(1, 1);
            var rectRef = new NativeReference<Rect>(Allocator.TempJob);

            var minRectJob = HullAlgorithms.CalculateBoundingRect(points, rectRef);

            minRectJob.Complete();

            var rect = rectRef.Value;
            Assert.IsTrue(rect.width == 0.0f);
            Assert.IsTrue(rect.height == 0.0f);
            Assert.IsTrue(rect.x == 1.0f);
            Assert.IsTrue(rect.y == 1.0f);

            points.Dispose();
            rectRef.Dispose();
        }

        [Test]
        public void BoundingRect()
        {
            var points = new NativeArray<float2>(4, Allocator.TempJob);
            points[0] = new float2(-1, -1);
            points[1] = new float2(1, -1);
            points[2] = new float2(1, 1);
            points[3] = new float2(-1, 1);
            var rectRef = new NativeReference<Rect>(Allocator.TempJob);

            var minRectJob = HullAlgorithms.CalculateBoundingRect(points, rectRef);

            minRectJob.Complete();

            var rect = rectRef.Value;
            Assert.IsTrue(rect.width == 2.0f);
            Assert.IsTrue(rect.height == 2.0f);
            Assert.IsTrue(rect.x == -1.0f);
            Assert.IsTrue(rect.y == -1.0f);

            points.Dispose();
            rectRef.Dispose();
        }


        [Test]
        public void EmptyBox()
        {
            var points = new NativeArray<float3>(0, Allocator.TempJob);
            var boundsRef = new NativeReference<Bounds>(Allocator.TempJob);

            var minBoundsJob = HullAlgorithms.CalculateBoundingBox(points, boundsRef);

            minBoundsJob.Complete();

            var bounds = boundsRef.Value;
            Assert.IsTrue(float.IsNaN(bounds.size.x));
            Assert.IsTrue(float.IsNaN(bounds.size.y));
            Assert.IsTrue(float.IsNaN(bounds.size.z));


            points.Dispose();
            boundsRef.Dispose();
        }


        [Test]
        public void SinglePointBox()
        {
            var points = new NativeArray<float3>(1, Allocator.TempJob);
            points[0] = new float3(1, 1, 1);
            var boundsRef = new NativeReference<Bounds>(Allocator.TempJob);

            var minBoundsJob = HullAlgorithms.CalculateBoundingBox(points, boundsRef);

            minBoundsJob.Complete();

            var bounds = boundsRef.Value;
            Assert.IsTrue(bounds.size.x == 0.0f);
            Assert.IsTrue(bounds.size.y == 0.0f);
            Assert.IsTrue(bounds.size.z == 0.0f);
            Assert.IsTrue(math.all((float3)bounds.center == points[0]));

            points.Dispose();
            boundsRef.Dispose();
        }


        [Test]
        public void BoundingBox()
        {
            var points = new NativeArray<float3>(4, Allocator.TempJob);
            points[0] = new float3(-1, -1, -1);
            points[1] = new float3(1, 1, 1);
            points[2] = new float3(-1, 1, -1);
            points[3] = new float3(1, -1, 1);
            var boundsRef = new NativeReference<Bounds>(Allocator.TempJob);

            var minBoundsJob = HullAlgorithms.CalculateBoundingBox(points, boundsRef);

            minBoundsJob.Complete();

            var bounds = boundsRef.Value;
            Assert.IsTrue(bounds.size.x == 2.0f);
            Assert.IsTrue(bounds.size.y == 2.0f);
            Assert.IsTrue(bounds.size.z == 2.0f);
            Assert.IsTrue(math.all((float3)bounds.center == float3.zero));

            points.Dispose();
            boundsRef.Dispose();
        }
    }
}
