using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using GimmeDOTSGeometry;

[TestFixture]
public class VoronoiDiagramAdapterTest
{
    [Test]
    public void GenerateVoronoiDiagram_CreatesValidDiagram()
    {
        // Arrange
        Rect bounds = new Rect(0, 0, 100, 100);
        int numSites = 10;
        int seed = 12345;

        // Act
        VoronoiDiagram voronoiDiagram = VoronoiDiagramAdapter.GenerateVoronoiDiagram(bounds, numSites, seed);

        // Assert
        Assert.IsNotNull(voronoiDiagram);
        Assert.AreEqual(numSites, voronoiDiagram.Sites.Count);
    }

    [Test]
    public void InitializeFromGimmeDOTSGeometry_InitializesCorrectly()
    {
        // Arrange
        Rect bounds = new Rect(0, 0, 100, 100);
        int numSites = 10;
        int seed = 12345;
        NativeArray<float2> points = new NativeArray<float2>(numSites, Allocator.Persistent);
        NativeArray<NativePolygon2D> polygons = new NativeArray<NativePolygon2D>(numSites, Allocator.Persistent);
        NativeArray<int> polygonSites = new NativeArray<int>(numSites, Allocator.Persistent);
        JobAllocations jobAllocations = new JobAllocations();

        Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)seed);
        for (int i = 0; i < numSites; i++)
        {
            polygons[i] = new NativePolygon2D(Allocator.Persistent, 3);
            points[i] = new float2(random.NextFloat(bounds.xMin, bounds.xMax), random.NextFloat(bounds.yMin, bounds.yMax));
        }

        var VJob = Voronoi2D.CalculateVoronoi(bounds, points, ref polygons, ref polygonSites, out jobAllocations);
        VJob.Complete();

        VoronoiDiagram voronoiDiagram = new VoronoiDiagram(bounds, numSites, seed);

        // Act
        voronoiDiagram.InitializeFromGimmeDOTSGeometry(bounds, seed, points, ref polygons, ref polygonSites, jobAllocations);

        // Assert
        Assert.AreEqual(numSites, voronoiDiagram.Sites.Count);
        Assert.IsTrue(voronoiDiagram.Vertices.Count > 0);
        Assert.IsTrue(voronoiDiagram.Edges.Count > 0);

        // Cleanup
        points.Dispose();
        polygons.Dispose();
        polygonSites.Dispose();
        jobAllocations.Dispose();
    }
}