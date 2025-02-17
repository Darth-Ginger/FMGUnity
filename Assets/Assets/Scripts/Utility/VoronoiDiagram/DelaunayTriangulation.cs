using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMGUnity.Utility
{

    public static class DelaunayTriangulation
    {
        public static List<Triangle> GenerateInThreads(List<Vector2> points, Rect bounds, int maxThreads)
        {
            // Determine the number of threads
            int numThreads = Mathf.Min(maxThreads, points.Count);

            // Split the grid into subgrids
            int width = (int)Mathf.Ceil(Mathf.Sqrt(numThreads));
            int height = (int)Mathf.Ceil((float)numThreads / width);

            // Create a hashtable for each possible subgrid
            var subgrids = new Dictionary<int, List<Vector2>>();
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    int index = i + j * width;
                    subgrids[index] = new List<Vector2>();
                }
            }

            // Sort the points according to what section they would fall
            foreach (var point in points)
            {
                int x = (int)Mathf.Floor((point.x - bounds.xMin) / (bounds.xMax - bounds.xMin) * width);
                int y = (int)Mathf.Floor((point.y - bounds.yMin) / (bounds.yMax - bounds.yMin) * height);
                int index = x + y * width;
                subgrids[index].Add(point);
            }

            // Perform the Generate method on each
            var threads = new List<System.Threading.Thread>();
            var results = new List<List<Triangle>>();
            foreach (var subgrid in subgrids.Values)
            {
                var thread = new System.Threading.Thread(() =>
                {
                    var result = Generate(subgrid);
                    lock (results)
                    {
                        results.Add(result);
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            // Wait for all threads to finish
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Merge them back
            var mergedTriangles = new List<Triangle>();
            foreach (var result in results)
            {
                mergedTriangles.AddRange(result);
            }

            return mergedTriangles;
        }

        public static List<Triangle> Generate(List<Vector2> points)
        {
            // Create a list to store triangles
            var triangles = new List<Triangle>();

            // Step 1: Create a super-triangle that encompasses all points
            Triangle superTriangle = CreateSuperTriangle(points);
            triangles.Add(superTriangle);

            // Step 2: Add each point to the triangulation
            foreach (var point in points)
            {
                var badTriangles = new List<Triangle>();

                // Step 3: Find all triangles whose circumcircle contains the point
                foreach (var triangle in triangles)
                {
                    if (IsPointInCircumcircle(point, triangle))
                    {
                        badTriangles.Add(triangle);
                    }
                }   

                // Debug.Log($"Bad triangles identified: {badTriangles.Count}");

                // Step 4: Find the polygonal hole boundary (edges not shared by two bad triangles)
                var polygon = FindHoleBoundary(badTriangles);

                // Debug.Log($"Edges in hole boundary: {polygon.Count}");

                // Step 5: Remove the bad triangles from the triangulation
                foreach (var badTriangle in badTriangles)
                {
                    triangles.Remove(badTriangle);
                }

                // Debug.Log($"Bad Triangles removed: {triangles.Count}");

                // Step 6: Add new triangles connecting the point to each edge of the polygon
                foreach (var edge in polygon)
                {
                    triangles.Add(new Triangle(edge.Start, edge.End, point));
                }
            }

            // Step 7: Remove triangles that share a vertex with the super-triangle
            triangles.RemoveAll(t => 
                ContainsVertex(t, superTriangle.Vertices[0]) ||
                ContainsVertex(t, superTriangle.Vertices[1]) ||
                ContainsVertex(t, superTriangle.Vertices[2])
            );
            // Debug.Log($"Total Triangles (post super-triangle removal): {triangles.Count}");
            return triangles;
        }

        private static Triangle CreateSuperTriangle(List<Vector2> points)
        {
            // Find the bounding box of all points
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var point in points)
            {
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.y > maxY) maxY = point.y;
            }

            // Create a super-triangle that encompasses all points
            float dx = maxX - minX;
            float dy = maxY - minY;
            float deltaMax = Mathf.Max(dx, dy) * 10;

            Vector2 p1 = new Vector2(minX - deltaMax, minY - deltaMax);
            Vector2 p2 = new Vector2(maxX + deltaMax, minY - deltaMax);
            Vector2 p3 = new Vector2(minX + dx / 2, maxY + deltaMax);

            return new Triangle(p1, p2, p3);
        }

        private static bool IsPointInCircumcircle(Vector2 point, Triangle triangle)
        {
            var v1 = triangle.Vertices[0];
            var v2 = triangle.Vertices[1];
            var v3 = triangle.Vertices[2];

            float ax = v1.x - point.x;
            float ay = v1.y - point.y;
            float bx = v2.x - point.x;
            float by = v2.y - point.y;
            float cx = v3.x - point.x;
            float cy = v3.y - point.y;

            float det = (ax * (by * cy - bx * cy)) -
                        (bx * (ay * cy - cx * cy)) +
                        (cx * (ay * by - bx * by));
            // Debug.Log($"Checking circumcircle: {point}, Triangle {triangle} -> {det}");
            return det > 0;
        }

        private static List<Edge> FindHoleBoundary(List<Triangle> badTriangles)
        {
            Dictionary<Edge, int> edgeUsage = new();

            foreach (var triangle in badTriangles)
            {
                foreach (var edge in triangle.GetEdges())
                {
                    if (edgeUsage.ContainsKey(edge))
                    {
                        edgeUsage[edge]++;
                    }
                    else
                    {
                        edgeUsage[edge] = 1;
                    }
                }
            }

            var polygon = new List<Edge>();
            foreach (var edge in edgeUsage)
            {
                if (edge.Value == 1)  // Only keep edges that are not shared
                {
                    polygon.Add(edge.Key);
                }
            }

            return polygon;
        }

        private static bool ContainsVertex(Triangle triangle, Vector2 vertex)
        {
            return triangle.Vertices[0] == vertex ||
                triangle.Vertices[1] == vertex ||
                triangle.Vertices[2] == vertex;
        }
    }

    [Serializable]
    public class Edge
    {
        [SerializeField] private Vector2 _start;
        [SerializeField] private Vector2 _end;

        public Vector2 Start => _start;
        public Vector2 End   => _end;

        public Edge(Vector2 start, Vector2 end)
        {
            _start = start;
            _end = end;
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge other)
            {
                return (Start == other.Start && End == other.End) ||
                    (Start == other.End && End == other.Start);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() ^ End.GetHashCode();
        }
    }
}
