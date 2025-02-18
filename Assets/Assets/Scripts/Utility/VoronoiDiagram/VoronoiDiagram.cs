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
        [SerializeField] private List<VoronoiPoint> _sites     = new();
        [SerializeField] private List<VoronoiCell>  _cells      = new();
        [SerializeField] private List<VoronoiEdge>  _edges      = new();
        
        // Public properties
        public IReadOnlyList<Triangle>     Triangles => _triangleMap.List;
        public IReadOnlyList<VoronoiPoint> Sites     => _siteMap.List;
        public IReadOnlyList<VoronoiCell>  Cells     => _cellMap.List;
        public IReadOnlyList<VoronoiEdge>  Edges     => _edgeMap.List;

        // Object -> Index Mappings
        private IndexMap<Triangle>     _triangleMap = new();
        private IndexMap<VoronoiPoint> _siteMap    = new();
        private IndexMap<VoronoiCell>  _cellMap     = new();
        private IndexMap<VoronoiEdge>  _edgeMap     = new();

        // Public Map Accessors
        public Triangle     GetTriangle (string id) => _triangleMap.Get(id);
        public VoronoiPoint GetSite     (string id) => _siteMap.Get(id);
        public VoronoiPoint GetSite     (Vector2 position) => _siteMap.GetBy(p => p.Position == position);
        public VoronoiCell  GetCell     (string id) => _cellMap.Get(id);
        public VoronoiCell  GetCell     (Vector2 site) => _cellMap.GetBy(c => c.Site == site);
        public VoronoiEdge  GetEdge     (string id) => _edgeMap.Get(id);
        public VoronoiEdge  GetEdge     (VoronoiPoint start, VoronoiPoint end) => _edgeMap.GetBy(e => e.Start == start.Name && e.End == end.Name);
        public VoronoiEdge  GetEdge     (Vector2 start, Vector2 end) => _edgeMap.GetBy(e => e.Start == GetSite(start).Name && e.End == GetSite(end).Name);



        [Header("Voronoi Diagram Settings")]
        public Vector2Int MapBounds { get; private set; }
        public int Seed             { get; private set; }
        public int SiteCount       { get; private set; }

        public VoronoiDiagram(Vector2Int mapBounds, int siteCount = 100, int seed = 42, bool regenerate = false, bool useMultiThreading = true, int maxThreads = 8)
        {
            MapBounds = mapBounds;
            Seed = seed;
            SiteCount = siteCount;

            Random.InitState(Seed);

            Generate(regenerate, useMultiThreading, maxThreads);
        }

        public VoronoiDiagram Generate(bool regenerate = false, bool useMultiThreading = true, int maxThreads = 8)
        {
            if (!regenerate && IsInitialized()) return this;
            
            
            Clear();

            // Generate random sites
            GenerateSites(SiteCount);

            // Generate Delaunay triangulation
            if (!useMultiThreading || maxThreads <= 1)
            {
                Debug.Log("Generating Delaunay Triangulation in a single thread");
                _triangleMap = new(DelaunayTriangulation.Generate(Sites.Select(p => p.Position).ToList()));
            }
            else{
                Debug.Log($"Generating Delaunay Triangulation in {maxThreads} threads");
                Rect bounds = new Rect(0, 0, MapBounds.x, MapBounds.y);
                _triangleMap = new(DelaunayTriangulation.GenerateInThreads(Sites.Select(p => p.Position).ToList(), bounds, maxThreads));
            }

            // Generate Voronoi diagram
            GenerateDiagram();

            return this;
        }


        /// <summary>
        /// Generates sites in the Voronoi diagram, optionally regenerating existing sites.
        /// </summary>
        /// <param name="count">The number of sites to generate.</param>
        /// <param name="regenerate">Whether to regenerate existing sites.</param>
        public void GenerateSites(int count, bool regenerate = false)
        {
            if (regenerate || _siteMap.List.Count <= 0)
            {
                _siteMap.Clear();
                Random.InitState(Seed);

                for (int i = 0; i < count; i++)
                {
                    _siteMap.Add(new VoronoiPoint(
                        Random.Range(0, MapBounds.x),
                        Random.Range(0, MapBounds.y)
                    ));
                }
            }
        }

        /// <summary>
        /// Clears the Voronoi diagram, removing all sites, triangles and cells.
        /// </summary>
        public void Clear()
        {
            _siteMap?.Clear();
            _triangleMap?.Clear();
            _cellMap?.Clear();
            _edgeMap?.Clear();
        }

        public bool IsInitialized() => !(Sites == null || Triangles == null || Cells == null || Edges == null) &&
                                      !(Sites.Count == 0 || Triangles.Count == 0 || Cells.Count == 0 || Edges.Count == 0);

        /// <summary>
        /// Generates a Voronoi diagram from a given set of Delaunay triangles and sites.
        /// </summary>
        /// <param name="regenerate">Whether to regenerate the Voronoi diagram.</param>
        /// <returns>A list of Voronoi cells, each representing a region around a site site.</returns>
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

            // Initialize cells for each site
            foreach (var site in _siteMap.List)
            {
                cells[site.Position] = new VoronoiCell(site.Position);
                site.PartOfCell(cells[site.Position].Name);
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
                        VoronoiPoint start = _siteMap.GetBy(p => p.Position == edge.Start);
                        VoronoiPoint end = _siteMap.GetBy(p => p.Position == edge.End);
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
                            if (edgeMap[edge].LeftCell == null || edgeMap[edge].LeftCell == string.Empty)
                            {
                                // Assign the cell to the left side of the edge
                                edgeMap[edge].SetLeft(cell);
                            }
                            else if (edgeMap[edge].RightCell == null || edgeMap[edge].RightCell == string.Empty)
                            {
                                // Assign the cell to the right side of the edge
                                edgeMap[edge].SetRight(cell);
                            }
                        }
                    }

                    // Add Neighbors to cell 
                    if (edgeMap[edge].LeftCell != string.Empty && edgeMap[edge].RightCell != string.Empty)
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
        ///     The circumcenter is calculated by finding the intersection site of the 
        ///     perpendicular bisectors of two edges of the triangle. It is the center of 
        ///     the circle that passes through all three vertices of the triangle.
        /// </remarks>
        private Vector2 ComputeCircumcenter(Triangle triangle)
        {
            // Extract the vertices of the triangle
            Vector2 a = triangle.Vertices[0];
            Vector2 b = triangle.Vertices[1];
            Vector2 c = triangle.Vertices[2];

            // Compute the midsites of two edges
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
            return $"VoronoiDiagram: Sites= {Sites.Count}, Triangles= {Triangles.Count}, Cells= {Cells.Count}, Edges= {Edges.Count}";
        }

        public void OnBeforeSerialize()
        {
             // Ensure IndexMaps are synchronized internally
            _siteMap?.OnBeforeSerialize();
            _triangleMap?.OnBeforeSerialize();
            _cellMap?.OnBeforeSerialize();
            _edgeMap?.OnBeforeSerialize();

            _sites     = _siteMap?.List ?? new();
            _triangles  = _triangleMap?.List ?? new();
            _cells      = _cellMap?.List ?? new();
            _edges      = _edgeMap?.List ?? new();
        }

        public void OnAfterDeserialize()
        {
            _siteMap = new(_sites);
            _triangleMap = new(_triangles);
            _cellMap = new(_cells);
            _edgeMap = new(_edges);

            // Rebuild maps
            _siteMap.OnAfterDeserialize();
            _triangleMap.OnAfterDeserialize();
            _cellMap.OnAfterDeserialize(); 
            _edgeMap.OnAfterDeserialize();
        }
    }
}