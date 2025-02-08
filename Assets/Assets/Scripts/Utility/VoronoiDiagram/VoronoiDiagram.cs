using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using Codice.Client.BaseCommands;
using System.Linq;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiDiagram
    {
        [SerializeField] private List<Vector2>     _points = new();
        [SerializeField] private List<Triangle>    _triangles = new();
        [SerializeField] private List<VoronoiCell> _cells = new();
        [SerializeField] private List<VoronoiEdge> _edges = new();

        public List<Vector2>     Points     => _points;
        public List<Triangle>    Triangles  => _triangles;
        public List<VoronoiCell> Cells      => _cells;
        public List<VoronoiCell> GetCells() => _cells;
        public List<VoronoiEdge> Edges      => _edges;

        [Header("Voronoi Diagram Settings")]
        public Vector2Int MapBounds { get; private set; }
        public int Seed             { get; private set; }
        public int PointCount       { get; private set; }

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
            if (regenerate && IsInitialized())
            {
                Clear();
            }

            // Generate random points
            GeneratePoints(PointCount);

            // Generate Delaunay triangulation
            _triangles = DelaunayTriangulation.Generate(_points);

            // Generate Voronoi diagram
            GenerateDiagram(_triangles, _points);

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

        public bool IsInitialized() => !(_points.Count == 0 || _triangles.Count == 0 || _cells.Count == 0);

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
        public void GenerateDiagram(List<Triangle> delaunayTriangles, List<Vector2> points, bool regenerate = false)
        {
            if (!regenerate && _cells.Count > 0) return;

            // Create a dictionary to store the cells
            var cells = new Dictionary<Vector2, VoronoiCell>();

            // Initialize cells for each point
            foreach (var point in points)
            {
                cells[point] = new VoronoiCell(point);
            }

            // Store edges to track shared ones
            Dictionary<Edge, VoronoiEdge> edgeMap = new();

            foreach (var triangle in delaunayTriangles)
            {
                Vector2 circumcenter = ComputeCircumcenter(triangle);

                foreach (var edge in triangle.GetEdges())
                {
                    if (!edgeMap.ContainsKey(edge))
                    {
                        edgeMap[edge] = new VoronoiEdge(edge.Start, edge.End);
                    }

                    foreach (var vertex in triangle.Vertices)
                    {
                        if (cells.TryGetValue(vertex, out VoronoiCell cell))
                        {
                            if (edgeMap[edge].LeftCell == null)
                            {
                                edgeMap[edge].LeftCell = cell;
                                edgeMap[edge].LeftCell.AddNeighbor(edgeMap[edge].RightCell);
                            }
                            else
                            {
                                edgeMap[edge].RightCell = cell;
                                edgeMap[edge].RightCell.AddNeighbor(edgeMap[edge].LeftCell);
                            }
                        }
                    }
                }
            }

            // Set the Cells and Edges lists
            _cells = cells.Values.ToList();
            _edges = edgeMap.Values.ToList();
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
    
        public override string ToString()
        {
            return $"VoronoiDiagram: Points= {Points.Count}, Triangles= {Triangles.Count}, Cells= {Cells.Count}, Edges= {Edges.Count}";
        }

        public string ToJson() => $"{{\"bounds\": {MapBounds}, \"seed\": {Seed}, \"pointCount\": {PointCount}, \"points\": {Points}, \"triangles\": {Triangles}, \"cells\": {Cells}, \"edges\": {Edges}}}";
    }
}