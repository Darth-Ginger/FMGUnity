using System.Collections.Generic;
using GimmeDOTSGeometry;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using Random = UnityEngine.Random;

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
        voronoiDiagram.InitializeFromGimmeDOTSGeometry(bounds, points, ref polygons, ref polygonSites, jobAllocations);

        // Dispose of native arrays
        points.Dispose();
        polygons.Dispose();
        polygonSites.Dispose();
        jobAllocations.Dispose();

        return voronoiDiagram;
    }

    public static void InitializeFromGimmeDOTSGeometry(this VoronoiDiagram voronoiDiagram, Rect bounds, 
        NativeArray<float2> points, ref NativeArray<NativePolygon2D> polygons, ref NativeArray<int> polygonSites,
        JobAllocations jobAllocations)
    {
        voronoiDiagram.Clear();

        // Initialize sites
        voronoiDiagram = new(bounds, points.Length, seed);
        BuildSites(voronoiDiagram, ref points);
        BuildVertices(voronoiDiagram, ref polygons);

        GetAllocationMapping(voronoiDiagram, jobAllocations, ref points, ref polygons, ref polygonSites, out Dictionary<string, Dictionary<string, List<object>>> allocationMapping);

        BuildEdges(voronoiDiagram, ref allocationMapping);
        BuildCells(voronoiDiagram, ref allocationMapping);


    }

    private static void BuildSites(this VoronoiDiagram voronoiDiagram, ref NativeArray<float2> points)
    {
        foreach (float2 point in points)
        {
            VoronoiSite site = new VoronoiSite(point);
            voronoiDiagram.Add(site);
        }
    }

    private static void BuildVertices(this VoronoiDiagram voronoiDiagram, ref NativeArray<NativePolygon2D> polygons)
    {

        foreach (NativePolygon2D polygon in polygons)
        {
            foreach (float2 vertex in polygon.points)
            {
                VoronoiVertex voronoiVertex = new VoronoiVertex(vertex);
                voronoiDiagram.Add(voronoiVertex);
            }
            
        }
    }

    private static List<(Vector2, Vector2)> GetVertexPairs(List<Vector2> polygon)
    {
        List<(Vector2, Vector2)> vertexPairs = new();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 currentVertex = polygon[i];
            Vector2 nextVertex = polygon[(i + 1) % polygon.Count];
            vertexPairs.Add((currentVertex, nextVertex));
        }

        return vertexPairs;
    }

    private static void BuildEdges(this VoronoiDiagram voronoiDiagram, ref Dictionary<string, Dictionary<string, List<object>>> allocationMapping)
    {
        
        foreach (var sites in allocationMapping)
        {
            string siteId = sites.Key;
            Dictionary<string, List<object>> siteData = sites.Value;

            List<HalfEdge> halfEdges    = siteData["halfEdges"].Where(item => item is HalfEdge).Cast<HalfEdge>().ToList();
            List<Vector2> polygonVerts  = siteData["polygon"].Where(item => item is Vector2).Cast<Vector2>().ToList();

            foreach (HalfEdge halfEdge in halfEdges)
            {
                if (halfEdge.vertexFwd >= 0 && halfEdge.vertexBack >= 0)
                {
                    VoronoiVertex startVertex = voronoiDiagram.GetVertex(polygonVerts[halfEdge.vertexBack]);
                    VoronoiVertex endVertex   = voronoiDiagram.GetVertex(polygonVerts[halfEdge.vertexFwd]);

                    if (startVertex == null || endVertex == null)
                    {
                        Debug.LogError("Start or end vertex of Polygon was not initialized");
                        continue;
                    }

                    VoronoiEdge edge = new VoronoiEdge(voronoiDiagram, startVertex, endVertex);

                    voronoiDiagram.Add(edge);
                }
        
            }
        }
    }

    internal static void BuildCells(this VoronoiDiagram voronoiDiagram, ref Dictionary<string, Dictionary<string, List<object>>> allocationMapping)
    {
        
        foreach (var site in allocationMapping)
        {
            string siteId = site.Key;
            VoronoiSite vSite = voronoiDiagram.GetSite(siteId);

            Dictionary<string, List<object>> siteData = site.Value;

            List<HalfEdge> halfEdges    = siteData["halfEdges"].Where(item => item is HalfEdge).Cast<HalfEdge>().ToList();
            List<Vector2> polygonVerts  = siteData["polygon"].Where(item => item is Vector2).Cast<Vector2>().ToList();
            List<(Vector2, Vector2)> vertexPairs = GetVertexPairs(polygonVerts);


            // Initial Cel
            VoronoiCell cell = new VoronoiCell(vSite, voronoiDiagram);

            // Add Vertices
            foreach (Vector2 vertex in polygonVerts)
            {
                cell.AddVertex(vertex);
            }

            // Add Edges
            foreach ((Vector2 start, Vector2 end) in vertexPairs)
            {
                VoronoiEdge edge = voronoiDiagram.GetEdge(start, end);
                if (edge != null)
                {
                    // Assign Right and left cells
                    if (edge.LeftSiteId == "null")
                    {
                        edge.SetLeftSite(vSite);
                    }
                    else if (edge.RightSiteId == "null")
                    {
                        edge.SetRightSite(vSite);
                    }
                    cell.AddEdge(edge);
                }
            }
            voronoiDiagram.Add(cell);
            
        }
    }

    public static void GetAllocationMapping(this VoronoiDiagram voronoiDiagram, 
                                            JobAllocations jobAllocations, 
                                            ref NativeArray<float2> points, 
                                            ref NativeArray<NativePolygon2D> polygons, 
                                            ref NativeArray<int> polygonSites,
                                            out Dictionary<string, Dictionary<string, List<object>>> allocationMapping)
    {
        int[] reversedPolygonSites = new int[polygonSites.Length];
        for (int i = 0; i < polygonSites.Length; i++)
        {
            reversedPolygonSites[polygonSites[i]] = i;
        }

        var VoronoiHalfEdges = (NativeList<HalfEdge>)jobAllocations.allocatedMemory[3];
        var HalfEdgeSites = (NativeList<int>)jobAllocations.allocatedMemory[5];

        // Get allocation mapping
        // Dictionary< SiteId, Dictionary <AllocationName, List<AllocationId>>>
        allocationMapping = new Dictionary<string, Dictionary<string, List<object>>>();

        for (int i = 0; i < points.Length; i++)
        {
            var siteId = voronoiDiagram.GetSite(points[i]).Id;
            allocationMapping[siteId] = new()
            {
                { "site",       new List<object>() },
                { "polygon",    new List<object>() },
                { "halfEdges",  new List<object>() },
            };


            allocationMapping[siteId]["site"].Add(voronoiDiagram.GetSite(points[i]).Id);
            
            
            // Each polygon is mapped to 1 site per PolygonSites (must be in list form due to Dictionary setup)
            List<Vector2> polygonVerts= new();

            foreach (float2 vert in polygons[reversedPolygonSites[i]].points)
            { 
                polygonVerts.Add((Vector2)vert);
            }

            allocationMapping[siteId]["polygon"] = polygonVerts.Cast<object>().ToList();
        }

        for (int he = 0; he < VoronoiHalfEdges.Length; he++)
        {
            var siteIndex = HalfEdgeSites[he];
            if (siteIndex >= points.Length)
            {
                Debug.LogError($"Invalid site index {siteIndex} for half edge {he}");
                continue;
            }
            string siteId = voronoiDiagram.GetSite(points[siteIndex]).Id;

            if (!allocationMapping.ContainsKey(siteId))
            {
                Debug.LogError($"Site {siteId} not found in allocation mapping");
                continue;
            }

            allocationMapping[siteId]["halfEdges"].Add(VoronoiHalfEdges[he]);
        }

        
    }
}


