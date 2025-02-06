using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Codice.Client.BaseCommands;

namespace FMGUnity.Utility
{
    public class VoronoiDiagram
    {
        private List<Vector2> _points     = new();
        private List<Triangle> _triangles = new();
        private List<VoronoiCell> _cells  = new();

        public List<Vector2> Points => _points;
        public List<Triangle> Triangles => _triangles;
        public List<VoronoiCell> Cells => _cells;
        public List<VoronoiCell> GetCells() => _cells;

        [Header("Voronoi Diagram Settings")]
        public Vector2Int MapBounds { get; private set;}
        public int Seed { get; private set; }
        public int PointCount { get; private set; }

        public VoronoiDiagram(Vector2Int mapBounds, int pointCount = 100, int seed = 42)
        {
            MapBounds = mapBounds;
            Seed = seed;
            PointCount = pointCount;

            Random.InitState(Seed);

            Generate();
        }

        public VoronoiDiagram Generate(bool regenerate = false)
        {
            if (regenerate)
            {
                Clear();
            }

            // Generate random points
            GeneratePoints(PointCount);

            // Generate Delaunay triangulation
            _triangles = DelaunayTriangulation.Generate(_points);

            // Generate Voronoi diagram
            _cells = GenerateDiagram(_triangles, _points);

            return this;
        }


        /// <summary>
        /// Generates points in the Voronoi diagram, optionally regenerating existing points.
        /// </summary>
        /// <param name="count">The number of points to generate.</param>
        /// <param name="regenerate">Whether to regenerate existing points.</param>
        public void GeneratePoints(int count, bool regenerate = false)
        {
            if (regenerate || _points.Count <= 0) 
            {
                _points.Clear();
                Random.InitState(Seed);

                for (int i = 0; i < count; i++)
                {
                    _points.Add(new Vector2(
                        Random.Range(0, MapBounds.x),
                        Random.Range(0, MapBounds.y)
                    ));
                }
            }
        }

        /// <summary>
        /// Clears the Voronoi diagram, removing all points, triangles and cells.
        /// </summary>
        public void Clear()
        {
            _points.Clear();
            _triangles.Clear();
            _cells.Clear();
        }


        /// <summary>
        /// Generates a Voronoi diagram from a given set of Delaunay triangles and points.
        /// </summary>
        /// <param name="delaunayTriangles">The list of Delaunay triangles used to generate the Voronoi cells.</param>
        /// <param name="points">The list of points that serve as sites for the Voronoi cells.</param>
        /// <returns>A list of Voronoi cells, each representing a region around a point site.</returns>
        /// <remarks>
        ///     This method computes the circumcenters of each Delaunay triangle and assigns them to the
        ///     corresponding Voronoi cells based on triangle vertices. It also establishes adjacency
        ///     relationships between neighboring Voronoi cells.
        /// </remarks>
        public List<VoronoiCell> GenerateDiagram(List<Triangle> delaunayTriangles, List<Vector2> points, bool regenerate = false)
        {
            if (!regenerate && delaunayTriangles.Count > 0) return _cells;

            // Create a dictionary to store the cells
            var cells = new Dictionary<Vector2, VoronoiCell>();

            // Initialize cells for each point
            foreach (var point in points)
            {
                cells[point] = new VoronoiCell(point);
            }

            // Store edges to track shared ones
            Dictionary<Edge, List<VoronoiCell>> edgeToCells = new();

            // Iterate through each Delaunay triangle to compute circumcenters
            foreach (var triangle in delaunayTriangles)
            {
                // Compute the circumcenter of the triangle
                Vector2 circumcenter = ComputeCircumcenter(triangle);

                // Assign the circumcenter to all three vertices (sites) of the triangle
                foreach (var vertex in triangle.Vertices)
                {
                    // Add the circumcenter to the Voronoi cell for the corresponding site
                    if (cells.ContainsKey(vertex))
                    {
                        cells[vertex].Vertices.Add(circumcenter);
                    }
                }
                // Add triangle edges to edgeToCells
                var edges = triangle.GetEdges();
                foreach (var edge in edges)
                {
                    if (!edgeToCells.ContainsKey(edge))
                    {
                        edgeToCells[edge] = new List<VoronoiCell>();
                    }

                    foreach (var vertex in triangle.Vertices)
                    {
                        if (cells.TryGetValue(vertex, out VoronoiCell cell))
                        {
                            if (!edgeToCells[edge].Contains(cell))
                            {
                                edgeToCells[edge].Add(cell);
                            }
                        }
                    }
                }
            }

            // Connect circumcenters to form edges (optional visualization)
            foreach (var cell in cells.Values)
            {
                OrderVertices(cell); // Order the vertices in a clockwise manner
            }

            // Establish adjacency relationships
            foreach (var pair in edgeToCells)
            {
                var adjacentCells = pair.Value;
                if (adjacentCells.Count == 2)
                {
                    adjacentCells[0].AddNeighbor(adjacentCells[1]);
                    adjacentCells[1].AddNeighbor(adjacentCells[0]);
                }
            }

            // Return the Voronoi cells
            return new List<VoronoiCell>(cells.Values);
        }

        /// <summary>
        /// Computes the circumcenter of a given triangle.
        /// </summary>
        /// <param name="triangle">The triangle for which to compute the circumcenter.</param>
        /// <returns>The circumcenter as a Vector2.</returns>
        /// <remarks>
        ///     The circumcenter is calculated by finding the intersection point of the 
        ///     perpendicular bisectors of two edges of the triangle. It is the center of 
        ///     the circle that passes through all three vertices of the triangle.
        /// </remarks>
        private Vector2 ComputeCircumcenter(Triangle triangle)
        {
            // Extract the vertices of the triangle
            Vector2 a = triangle.Vertices[0];
            Vector2 b = triangle.Vertices[1];
            Vector2 c = triangle.Vertices[2];

            // Compute the midpoints of two edges
            Vector2 midAB = (a + b) / 2f;
            Vector2 midBC = (b + c) / 2f;

            // Compute perpendicular slopes
            Vector2 dirAB = b - a;
            Vector2 dirBC = c - b;

            Vector2 perpAB = new Vector2(-dirAB.y, dirAB.x); // Rotate 90 degrees
            Vector2 perpBC = new Vector2(-dirBC.y, dirBC.x);

            // Solve for intersection of two lines: midAB + t * perpAB = midBC + s * perpBC
            float t = (midBC.x - midAB.x) * perpBC.y - (midBC.y - midAB.y) * perpBC.x;
            t /= (perpAB.x * perpBC.y - perpAB.y * perpBC.x);

            // Compute the circumcenter
            return midAB + t * perpAB;
        }

        /// <summary>
        /// Orders the vertices of a Voronoi cell in a clockwise manner such that they can be rendered
        /// correctly. The vertices are sorted by their angle with respect to the center of the cell.
        /// </summary>
        /// <param name="cell">The Voronoi cell for which to order the vertices.</param>
        private void OrderVertices(VoronoiCell cell)
        {
            // Order the vertices in a clockwise manner for rendering
            Vector2 center = Vector2.zero;
            foreach (var vertex in cell.Vertices)
            {
                center += vertex;
            }
            center /= cell.Vertices.Count;

            cell.Vertices.Sort((v1, v2) =>
            {
                float angle1 = Mathf.Atan2(v1.y - center.y, v1.x - center.x);
                float angle2 = Mathf.Atan2(v2.y - center.y, v2.x - center.x);
                return angle1.CompareTo(angle2);
            });
        }
    }
}