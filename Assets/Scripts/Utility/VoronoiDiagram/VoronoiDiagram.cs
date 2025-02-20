using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using System.Linq;
using System;
using Random = UnityEngine.Random;
using FMGUnity.Utility.Interfaces;
using JetBrains.Annotations;
using BenTools.Mathematics;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiDiagram: ISerializationCallbackReceiver
    {

        // Serialiable fields for Unity and JSON
        [SerializeField] private List<VoronoiPoint> _sites      = new();
        [SerializeField] private List<VoronoiCell>  _cells      = new();
        [SerializeField] private List<VoronoiEdge>  _edges      = new();
        [SerializeField] private List<VoronoiPoint> _vertices   = new();
        
        // Public properties
        public IReadOnlyList<VoronoiPoint> Sites     => _siteMap.List;
        public IReadOnlyList<VoronoiCell>  Cells     => _cellMap.List;
        public IReadOnlyList<VoronoiEdge>  Edges     => _edgeMap.List;
        public IReadOnlyList<VoronoiPoint> Vertices  => _vertexMap.List;

        // Object -> Index Mappings
        private IndexMap<VoronoiPoint> _siteMap    = new();
        private IndexMap<VoronoiCell>  _cellMap     = new();
        private IndexMap<VoronoiEdge>  _edgeMap     = new();
        private IndexMap<VoronoiPoint> _vertexMap   = new();

        // Public Map Accessors
        public VoronoiPoint GetSite     (string id) => _siteMap.Get(id);
        public VoronoiPoint GetSite     (Vector2 position) => _siteMap.GetBy(p => p.Position == position);
        public VoronoiPoint GetPoint    (string id) => _siteMap.Get(id) ?? _vertexMap.Get(id);
        public VoronoiPoint GetPoint    (Vector2 position) => _siteMap.GetBy(p => p.Position == position) ?? _vertexMap.GetBy(p => p.Position == position);
        public VoronoiCell  GetCell     (string id) => _cellMap.Get(id);
        public VoronoiCell  GetCell     (Vector2 site) => _cellMap.GetBy(c => c.Site == site);
        public VoronoiEdge  GetEdge     (string id) => _edgeMap.Get(id);
        public VoronoiEdge  GetEdge     (VoronoiPoint start, VoronoiPoint end) => _edgeMap.GetBy(e => e.Start == start.Name && e.End == end.Name);
        public VoronoiEdge  GetEdge     (Vector2 start, Vector2 end) => _edgeMap.GetBy(e => e.Start == GetSite(start).Name && e.End == GetSite(end).Name);
        public VoronoiPoint GetVertex  (string id) => _vertexMap.Get(id);
        public VoronoiPoint GetVertex  (Vector2 position) => _vertexMap.GetBy(p => p.Position == position);
        


        [Header("Voronoi Diagram Settings")]
        public Vector2Int MapBounds { get; private set; }
        public int Seed             { get; private set; }
        public int SiteCount       { get; private set; }

        public VoronoiDiagram(Vector2Int mapBounds, int siteCount = 100, int seed = 42, bool regenerate = false)
        {
            MapBounds = mapBounds;
            Seed = seed;
            SiteCount = siteCount;

            Random.InitState(Seed);

            Generate(regenerate);
        }

        public VoronoiDiagram Generate(bool regenerate = false)
        {
            if (!regenerate && IsInitialized()) return this;
            
            Clear();

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
                    AddSite(new VoronoiPoint(
                        Random.Range(0, MapBounds.x),
                        Random.Range(0, MapBounds.y)
                    ));
                }

            }
        }

        // Add / Remove Sites
        #region Add / Remove Sites
        public void AddSite(VoronoiPoint site) 
        {
            if (site != null && !_siteMap.Contains(site.Name))
            {
                _siteMap.Add(site);
            }
        }
        public void AddSites(List<VoronoiPoint> sites) 
        {
            foreach (var site in sites)
            {
                AddSite(site);
            }
        }
        public void RemoveSite(VoronoiPoint site) 
        {
            _siteMap.RemoveById(site.Name);
        }
        public void RemoveSites(List<VoronoiPoint> sites) 
        {
            foreach (var site in sites)
            {
                RemoveSite(site);
            }
        }
        #endregion Add / Remove Sites

        // Add / Remove Cells
        #region Add / Remove Cells
        public void AddCell(VoronoiCell cell) 
        {
            if (cell != null && !_cellMap.Contains(cell.Name))
            {
                _cellMap.Add(cell);
            }
        }
        public void AddCells(List<VoronoiCell> cells) 
        {
            foreach (var cell in cells)
            {
                AddCell(cell);
            }
        }
        public void RemoveCell(VoronoiCell cell) 
        {
            _cellMap.RemoveById(cell.Name);
        }
        public void RemoveCells(List<VoronoiCell> cells) 
        {
            foreach (var cell in cells)
            {
                RemoveCell(cell);
            }
        }
        #endregion Add / Remove Cells

        // Add / Remove Vertices
        #region Add / Remove Vertices
        public void AddVertex(VoronoiPoint point) 
        {
            if (point != null && !_vertexMap.Contains(point.Name))
            {
                _vertexMap.Add( point);
            }
        }
        public void AddVertices(List<VoronoiPoint> points) 
        {
            foreach (var point in points)
            {
                AddVertex(point);
            }
        }
        public void RemoveVertex(VoronoiPoint point) 
        {
             _vertexMap.RemoveById(point.Name);
        }
        public void RemoveVertices(List<VoronoiPoint> points) 
        {
            foreach (var point in points)
            {
                RemoveVertex(point);
            }
        }
        #endregion Add / Remove Vertices

        // Add / Remove Edges
        #region Add / Remove Edges
        public void AddEdge(VoronoiEdge edge)
        {
            if (edge != null && !_edgeMap.Contains(edge))
            {
                _edgeMap.Add(edge);
            }
        }
        public void AddEdges(List<VoronoiEdge> edges)
        {
            foreach (var edge in edges)
            {
                AddEdge(edge);
            }
        }
        public void RemoveEdge(VoronoiEdge edge)
        {
            if (edge != null && _edgeMap.Contains(edge))
            {
                _edgeMap.RemoveById(edge.Name);
            }
        }
        public void RemoveEdges(List<VoronoiEdge> edges)
        {
            foreach (var edge in edges)
            {
                RemoveEdge(edge);
            }
        }
        #endregion Add / Remove Edges

        /// <summary>
        /// Clears the Voronoi diagram, removing all sites, triangles and cells.
        /// </summary>
        public void Clear()
        {
            _siteMap?.Clear();
            // _triangleMap?.Clear();
            _cellMap?.Clear();
            _edgeMap?.Clear();
        }

        public bool IsInitialized() => !(Sites == null || Cells == null || Edges == null) &&
                                      !(Sites.Count == 0 || Cells.Count == 0 || Edges.Count == 0);

        /// <summary>
        /// Generates a Voronoi diagram from a given set of Delaunay triangles and sites.
        /// </summary>
        /// <param name="regenerate">Whether to regenerate the Voronoi diagram.</param>
        /// <returns>A list of Voronoi cells, each representing a region around a site.</returns>
        /// <remarks>
        ///     The Voronoi diagram is generated using the Fortune's algorithm.
        /// </remarks>
        private void GenerateDiagram( bool regenerate = false)
        {
            // Initialize temp data structures
            Dictionary<Vector2, VoronoiCell> siteToCell = new();

            if (!regenerate && _cellMap.List.Count > 0) return;

            // 1. Generate the sites and add them to _siteMap
            GenerateSites(SiteCount);

            // 2. Generate the Voronoi Graph
            List<Vector2> sites = Sites.Select(p => p.Position).ToList();
            Debug.Log($"Generated {sites.Count} sites.");
            VoronoiGraph vGraph = Fortune.ComputeVoronoiGraph(sites);

            Debug.Log($"Generated a Voronoi Graph with {vGraph.Edges.Count} edges and {vGraph.Vertices.Count} vertices.");


            // Convert the results to your VoronoiDiagram data structures

            // 3. Initialize cells for each site and add them to _cellMap
            foreach (Vector2 site in Sites.Select(p => p.Position).ToList())
            {
                VoronoiCell newCell = new(site);
                AddCell(newCell);
                GetSite(site).PartOfCell(newCell.Name);
                siteToCell.Add(site, newCell);
            }

            

            // 4. Initialize edges and add them to _edgeMap
            foreach (BenTools.Mathematics.VoronoiEdge edge in vGraph.Edges)
            {
                if (edge.VVertexA == edge.VVertexB ||
                    edge.VVertexA == null || edge.VVertexB == null ||
                    float.IsNaN(edge.VVertexA.x) || float.IsNaN(edge.VVertexB.x)
                    ) continue;

                // 4.1 Get start and end points of the edge
                VoronoiPoint start = new(edge.VVertexA);
                VoronoiPoint end   = new(edge.VVertexB);

                // 4.2 Add start and end vertices to _vertexMap
                AddVertex(start);
                AddVertex(end);

                // 4.3 Get cells associate to a given edge and add the vertices to them
                VoronoiCell leftCell  = siteToCell[edge.LeftData];
                leftCell.AddVertex(start);
                leftCell.AddVertex(end);
                VoronoiCell rightCell = siteToCell[edge.RightData];
                rightCell.AddVertex(start);
                rightCell.AddVertex(end);

                // 4.4 Create new edge and add it to _edgeMap
                if (start == null) {
                    Debug.LogError($"Start of Edge cannot be null");
                    continue;
                    }
                if (end == null) {
                    Debug.LogWarning($"End of Edge cannot be null");
                    continue;
                    }
                if (start == end) {
                    Debug.LogWarning($"Start and end cannot be the same");
                    continue;
                    }
                VoronoiEdge newEdge = new(start, end);
                AddEdge(newEdge);

                // 4.5 Add edge to both cells 
                if (leftCell != null && rightCell != null && leftCell != rightCell)
                {
                    // Add edge to both cells
                    leftCell .AddEdge(newEdge);
                    rightCell.AddEdge(newEdge);
                }
            }
        }

    
        public override string ToString()
        {
            return $"VoronoiDiagram: Sites= {Sites.Count}, Cells= {Cells.Count}, Edges= {Edges.Count}";
        }

        public void OnBeforeSerialize()
        {
             // Ensure IndexMaps are synchronized internally
            _siteMap?.OnBeforeSerialize();
            _cellMap?.OnBeforeSerialize();
            _edgeMap?.OnBeforeSerialize();
            _vertexMap?.OnBeforeSerialize();

            _sites      = _siteMap?.List ?? new();
            _cells      = _cellMap?.List ?? new();
            _edges      = _edgeMap?.List ?? new();
            _vertices   = _vertexMap?.List ?? new();
        }

        public void OnAfterDeserialize()
        {
            _siteMap   = new(_sites);
            _cellMap   = new(_cells);
            _edgeMap   = new(_edges);
            _vertexMap = new(_vertices);

            // Rebuild maps
            _siteMap.OnAfterDeserialize();
            _cellMap.OnAfterDeserialize(); 
            _edgeMap.OnAfterDeserialize();
            _vertexMap.OnAfterDeserialize();
        }
    }
}