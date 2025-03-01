using System.Runtime.CompilerServices;
using GimmeDOTSGeometry;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class VoronoiDiagramAdapter
{
    public static VoronoiDiagram GenerateVoronoiDiagram(Rect bounds, int numSites, int seed)
    {
        // Use GimmeDOTSGeometry to generate the Voronoi diagram
        NativeArray<float2> points = new NativeArray<float2>(numSites, Allocator.Temp);
        NativeArray<NativePolygon2D> polygons = new NativeArray<NativePolygon2D>(numSites, Allocator.Temp);
        NativeArray<int> polygonSites = new NativeArray<int>(numSites, Allocator.Temp);

        // Generate points and calculate Voronoi diagram
        Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)seed);
        for (int i = 0; i < numSites; i++)
        {
            polygons[i] = new NativePolygon2D(Allocator.Temp, 3);
            points[i] = new float2(random.NextFloat(bounds.xMin, bounds.xMax), random.NextFloat(bounds.yMin, bounds.yMax));
        }

        JobAllocations jobAllocations;
        GimmeDOTSGeometry.Voronoi2D.CalculateVoronoi(bounds, points, ref polygons, ref polygonSites, out jobAllocations);

        // Convert the output to VoronoiDiagram
        VoronoiDiagram voronoiDiagram = new VoronoiDiagram(bounds, numSites, seed);
        voronoiDiagram.InitializeFromGimmeDOTSGeometry(bounds, points, polygons, polygonSites);

        // Dispose of native arrays
        points.Dispose();
        polygons.Dispose();
        polygonSites.Dispose();
        jobAllocations.Dispose();

        return voronoiDiagram;
    }

    public static void InitializeFromGimmeDOTSGeometry(this VoronoiDiagram voronoiDiagram, Rect bounds, NativeArray<float2> points, NativeArray<NativePolygon2D> polygons, NativeArray<int> polygonSites)
    {
        // Implement the logic to convert GimmeDOTSGeometry output to VoronoiDiagram
        // This will involve creating VoronoiSite, VoronoiVertex, VoronoiEdge, and VoronoiCell objects
        // and populating the VoronoiDiagram accordingly.
        // ...
    }
}
