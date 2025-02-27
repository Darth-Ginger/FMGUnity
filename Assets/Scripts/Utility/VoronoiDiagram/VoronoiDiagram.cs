// using System.Collections.Generic;
// using Unity.Collections;
// using Unity.Mathematics;
// using UnityEngine;
// using GimmeDOTSGeometry;

// public class VoronoiDiagram
// {
//     private NativeArray<VoronoiCell> cells;
//     private NativeArray<float2> sites;
//     private NativeArray<VoronoiEdge> edges;
//     private NativeArray<float2> vertices;
//     private Rect mapBounds;

//     public VoronoiDiagram(Rect mapBounds, int numSites)
//     {
//         this.mapBounds = mapBounds;
//         GenerateDiagram(numSites);
//     }

//     public void GenerateDiagram(int numSites)
//     {
//         // Generate random sites within the map bounds.
//         sites = new NativeArray<float2>(numSites, Allocator.Persistent);
//         var random = new Unity.Mathematics.Random(1);
//         for (int i = 0; i < numSites; i++)
//         {
//             sites[i] = new float2(
//                 random.NextFloat(mapBounds.xMin, mapBounds.xMax),
//                 random.NextFloat(mapBounds.yMin, mapBounds.yMax)
//             );
//         }

//         // Generate the Voronoi diagram.
//         edges = Voronoi2D.GenerateVoronoiDiagram(sites, mapBounds, out vertices, out cells);
//         var delaunay = new Delaunay2D();
//         var voronoiOutput = new NativeList<VoronoiCell>(Allocator.Persistent);
//         var delaunayOutput = new NativeList<DelaunayTriangle>(Allocator.Persistent);
//         JobAllocations allocations = default;

//         try
//         {
//             delaunay.CalculateDelaunay(sites, ref delaunayOutput, ref allocations);
//             voronoi.CalculateVoronoi(sites, delaunayOutput, ref voronoiOutput, ref allocations);

//             // Store the generated cells.
//             cells = voronoiOutput.ToArray(Allocator.Persistent);

//             // Extract the edges and vertices from the cells.
//             var edgesSet = new HashSet<VoronoiEdge>();
//             var verticesSet = new HashSet<float2>();
//             foreach (var cell in voronoiOutput)
//             {
//                 foreach (var edge in cell.edges)
//                 {
//                     edgesSet.Add(edge);
//                     verticesSet.Add(edge.a);
//                     verticesSet.Add(edge.b);
//                 }
//             }

//             // Store the extracted edges and vertices.
//             edges = new NativeArray<VoronoiEdge>(edgesSet.Count, Allocator.Persistent);
//             int edgeIndex = 0;
//             foreach (var edge in edgesSet)
//             {
//                 edges[edgeIndex] = edge;
//                 edgeIndex++;
//             }

//             vertices = new NativeArray<float2>(verticesSet.Count, Allocator.Persistent);
//             int vertexIndex = 0;
//             foreach (var vertex in verticesSet)
//             {
//                 vertices[vertexIndex] = vertex;
//                 vertexIndex++;
//             }
//         }
//         finally
//         {
//             // Dispose the native arrays.
//             allocations.Dispose();
//             delaunayOutput.Dispose();
//             voronoiOutput.Dispose();
//         }
//     }

//     public void Dispose()
//     {
//         // Dispose the native arrays.
//         cells.Dispose();
//         sites.Dispose();
//         edges.Dispose();
//         vertices.Dispose();
//     }

//     public NativeArray<VoronoiCell> Cells => cells;
//     public NativeArray<float2> Sites => sites;
//     public NativeArray<VoronoiEdge> Edges => edges;
//     public NativeArray<float2> Vertices => vertices;
//     public Rect MapBounds => mapBounds;
// }