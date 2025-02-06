using System.Collections.Generic;
using UnityEngine;

namespace FMGUnity.Utility
{

    public static class DelaunayTriangulation
    {
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

                // Step 4: Find the polygonal hole boundary (edges not shared by two bad triangles)
                var polygon = FindHoleBoundary(badTriangles);

                // Step 5: Remove the bad triangles from the triangulation
                foreach (var badTriangle in badTriangles)
                {
                    triangles.Remove(badTriangle);
                }

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

            float det = (ax * (by * cy - by * cy)) -
                        (bx * (ay * cy - ay * cy)) +
                        (cx * (ay * by - ay * by));
            return det > 0;
        }

        private static List<Edge> FindHoleBoundary(List<Triangle> badTriangles)
        {
            var edges = new List<Edge>();

            foreach (var triangle in badTriangles)
            {
                foreach (var edge in triangle.GetEdges())
                {
                    // Add edges if they're not shared by two triangles
                    if (edges.Contains(edge))
                    {
                        edges.Remove(edge);
                    }
                    else
                    {
                        edges.Add(edge);
                    }
                }
            }

            return edges;
        }

        private static bool ContainsVertex(Triangle triangle, Vector2 vertex)
        {
            return triangle.Vertices[0] == vertex ||
                triangle.Vertices[1] == vertex ||
                triangle.Vertices[2] == vertex;
        }
    }

    public class Edge
    {
        public Vector2 Start { get; }
        public Vector2 End { get; }

        public Edge(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
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
