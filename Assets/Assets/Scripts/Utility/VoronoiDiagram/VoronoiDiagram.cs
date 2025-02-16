using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using System.Linq;
using System;
using Random = UnityEngine.Random;
using FMGUnity.Utility.Interfaces;
using JetBrains.Annotations;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiDiagram: ISerializationCallbackReceiver
    {

        // Serialiable fields for Unity and JSON
        [SerializeField] private List<Triangle>     _triangles  = new();
        [SerializeField] private List<VoronoiPoint> _points     = new();
        [SerializeField] private List<VoronoiCell>  _cells      = new();
        [SerializeField] private List<VoronoiEdge>  _edges      = new();
        
        // Public properties
        public IReadOnlyList<Triangle>     Triangles => _triangleMap.List;
        public IReadOnlyList<VoronoiPoint> Points    => _pointMap.List;
        public IReadOnlyList<VoronoiCell>  Cells     => _cellMap.List;
        public IReadOnlyList<VoronoiEdge>  Edges     => _edgeMap.List;

        // Object -> Index Mappings
        private IndexMap<Triangle>     _triangleMap = new();
        private IndexMap<VoronoiPoint> _pointMap    = new();
        private IndexMap<VoronoiCell>  _cellMap     = new();
        private IndexMap<VoronoiEdge>  _edgeMap     = new();

        // Public Map Accessors
        public Triangle     GetTriangle (Guid id) => _triangleMap.Get(id);
        public VoronoiPoint GetPoint    (Guid id) => _pointMap.Get(id);
        public VoronoiPoint GetPoint    (Vector2 position) => _pointMap.GetBy(p => p.Position == position);
        public VoronoiCell  GetCell     (Guid id) => _cellMap.Get(id);
        public VoronoiCell  GetCell     (Vector2 site) => _cellMap.GetBy(c => c.Site == site);
        public VoronoiEdge  GetEdge     (Guid id) => _edgeMap.Get(id);
        public VoronoiEdge  GetEdge     (VoronoiPoint start, VoronoiPoint end) => _edgeMap.GetBy(e => e.Start == start.Id && e.End == end.Id);
        public VoronoiEdge  GetEdge     (Vector2 start, Vector2 end) => _edgeMap.GetBy(e => e.Start == GetPoint(start).Id && e.End == GetPoint(end).Id);



        [Header("Voronoi Diagram Settings")]
        public Vector2Int MapBounds { get; private set; }
        public int Seed             { get; private set; }
        public int PointCount       { get; private set; }

        public VoronoiDiagram(Vector2Int mapBounds, int pointCount = 100, int seed = 42, bool regenerate = false, bool useMultiThreading = true, int maxThreads = 8)
        {
            MapBounds = mapBounds;
            Seed = seed;
            PointCount = pointCount;

            Random.InitState(Seed);

            Generate(regenerate, useMultiThreading, maxThreads);
        }

        public VoronoiDiagram Generate(bool regenerate = false, bool useMultiThreading = true, int maxThreads = 8)
        {
            if (!regenerate && IsInitialized()) return this;
            
            
            Clear();

            // Generate random points
            GeneratePoints(PointCount);

            // Generate Delaunay triangulation
            if (!useMultiThreading || maxThreads <= 1)
            {
                Debug.Log("Generating Delaunay Triangulation in a single thread");
                _triangleMap = new(DelaunayTriangulation.Generate(Points.Select(p => p.Position).ToList()));
            }
            else{
                Debug.Log($"Generating Delaunay Triangulation in {maxThreads} threads");
                Rect bounds = new Rect(0, 0, MapBounds.x, MapBounds.y);
                _triangleMap = new(DelaunayTriangulation.GenerateInThreads(Points.Select(p => p.Position).ToList(), bounds, maxThreads));
            }

            // Generate Voronoi diagram
            GenerateDiagram();

            return this;
        }


        /// <summary>
        /// Generates points in the Voronoi diagram, optionally regenerating existing points.
        /// </summary>
        /// <param name="count">The number of points to generate.</param>
        /// <param name="regenerate">Whether to regenerate existing points.</param>
        public void GeneratePoints(int count, bool regenerate = false)
        {
            if (regenerate || _pointMap.List.Count <= 0)
            {
                _pointMap.Clear();
                Random.InitState(Seed);

                for (int i = 0; i < count; i++)
                {
                    _pointMap.Add(new VoronoiPoint(
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
            _pointMap?.Clear();
            _triangleMap?.Clear();
            _cellMap?.Clear();
            _edgeMap?.Clear();
        }

        public bool IsInitialized() => !(Points == null || Triangles == null || Cells == null || Edges == null) &&
                                      !(Points.Count == 0 || Triangles.Count == 0 || Cells.Count == 0 || Edges.Count == 0);

        /// <summary>
        /// Generates a Voronoi diagram from a given set of Delaunay triangles and points.
        /// </summary>
        /// <param name="regenerate">Whether to regenerate the Voronoi diagram.</param>
        /// <returns>A list of Voronoi cells, each representing a region around a point site.</returns>
        /// <remarks>
        ///     This method computes the circumcenters of each Delaunay triangle and assigns them to the
        ///     corresponding Voronoi cells based on triangle vertices. It also establishes adjacency
        ///     relationships between neighboring Voronoi cells.
        /// </remarks>
        private void GenerateDiagram( bool regenerate = false)
        {

            if (!regenerate && _cellMap.List.Count > 0) return;

            // Create a dictionary to store the cells
            var cells = new Dictionary<Vector2, VoronoiCell>();

            // Initialize cells for each point
            foreach (var point in _pointMap.List)
            {
                cells[point.Position] = new VoronoiCell(point.Position);
            }

            // Set up cell map
            _cellMap = new(cells.Values.ToList());

            // Store edges to track shared ones
            Dictionary<Edge, VoronoiEdge> edgeMap = new();

            foreach (var triangle in _triangleMap.List)
            {
                Vector2 circumcenter = ComputeCircumcenter(triangle);

                foreach (var edge in triangle.GetEdges())
                {
                    if (!edgeMap.ContainsKey(edge))
                    {
                        //TODO: Update the GetEdges to return VoronoiEdges or find a way to map edge back to VoronoiEdge
                        VoronoiPoint start = _pointMap.GetBy(p => p.Position == edge.Start);
                        VoronoiPoint end = _pointMap.GetBy(p => p.Position == edge.End);
                        if (start == null) {
                            Debug.LogWarning($"Start of {edge} is null");
                            continue;
                            }
                        if (end == null) {
                            Debug.LogWarning($"End of {edge} is null");
                            continue;
                            }

                        edgeMap[edge] = new VoronoiEdge(start, end);
                    }

                    foreach (var vertex in triangle.Vertices)
                    {
                        if (cells.TryGetValue(vertex, out VoronoiCell cell))
                        {
                            if (edgeMap[edge].LeftCell == null || edgeMap[edge].LeftCell == Guid.Empty)
                            {
                                // Assign the cell to the left side of the edge
                                edgeMap[edge].SetLeft(cell);
                            }
                            else if (edgeMap[edge].RightCell == null || edgeMap[edge].RightCell == Guid.Empty)
                            {
                                // Assign the cell to the right side of the edge
                                edgeMap[edge].SetRight(cell);
                            }
                        }
                    }

                    // Add Neighbors to cell 
                    if (edgeMap[edge].LeftCell != Guid.Empty && edgeMap[edge].RightCell != Guid.Empty)
                    {
                        _cellMap.Get(edgeMap[edge].LeftCell).AddNeighbor(this, edgeMap[edge].RightCell);
                        _cellMap.Get(edgeMap[edge].RightCell).AddNeighbor(this, edgeMap[edge].LeftCell);
                    }
                }
            }

            // Set up edge map
            _edgeMap = new(edgeMap.Values.ToList());
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
            t /= perpAB.x * perpBC.y - perpAB.y * perpBC.x;

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

        public void OnBeforeSerialize()
        {
             // Ensure IndexMaps are synchronized internally
            _pointMap?.OnBeforeSerialize();
            _triangleMap?.OnBeforeSerialize();
            _cellMap?.OnBeforeSerialize();
            _edgeMap?.OnBeforeSerialize();

            _points     = _pointMap?.List ?? new();
            _triangles  = _triangleMap?.List ?? new();
            _cells      = _cellMap?.List ?? new();
            _edges      = _edgeMap?.List ?? new();
        }

        public void OnAfterDeserialize()
        {
            _pointMap = new(_points);
            _triangleMap = new(_triangles);
            _cellMap = new(_cells);
            _edgeMap = new(_edges);

            // Rebuild maps
            _pointMap.OnAfterDeserialize();
            _triangleMap.OnAfterDeserialize();
            _cellMap.OnAfterDeserialize(); 
            _edgeMap.OnAfterDeserialize();
        }
    }
}