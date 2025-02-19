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
        // [SerializeField] private List<Triangle>     _triangles  = new();
        [SerializeField] private List<VoronoiPoint> _sites     = new();
        [SerializeField] private List<VoronoiCell>  _cells      = new();
        [SerializeField] private List<VoronoiEdge>  _edges      = new();
        [SerializeField] private List<VoronoiPoint> _vertices   = new();
        
        // Public properties
        // public IReadOnlyList<Triangle>     Triangles => _triangleMap.List;
        public IReadOnlyList<VoronoiPoint> Sites     => _siteMap.List;
        public IReadOnlyList<VoronoiCell>  Cells     => _cellMap.List;
        public IReadOnlyList<VoronoiEdge>  Edges     => _edgeMap.List;
        public IReadOnlyList<VoronoiPoint> Vertices  => _vertexMap.List;

        // Object -> Index Mappings
        // private IndexMap<Triangle>     _triangleMap = new();
        private IndexMap<VoronoiPoint> _siteMap    = new();
        private IndexMap<VoronoiCell>  _cellMap     = new();
        private IndexMap<VoronoiEdge>  _edgeMap     = new();
        private IndexMap<VoronoiPoint> _vertexMap   = new();

        // Public Map Accessors
        // public Triangle     GetTriangle (string id) => _triangleMap.Get(id);
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

            if (!regenerate && _cellMap.List.Count > 0) return;

            VoronoiGraph vGraph = Fortune.ComputeVoronoiGraph(Sites.Select(p => p.Position).ToList());

            Dictionary<Vector2, VoronoiCell> siteToCell = new();

            // Convert the results to your VoronoiDiagram data structures

            // Initialize cells for each site
            foreach (Vector2 site in Sites.Select(p => p.Position).ToList())
            {
                VoronoiCell newCell = new VoronoiCell(site);
                _cellMap.Add(newCell);
                _siteMap.Get(site).PartOfCell(newCell.Name);
                siteToCell.Add(site, newCell);
            }

            foreach (BenTools.Mathematics.VoronoiEdge edge in vGraph.Edges)
            {
                if (edge.VVertexA == edge.VVertexB ||
                    edge.VVertexA == null || edge.VVertexB == null ||
                    float.IsNaN(edge.VVertexA.x) || float.IsNaN(edge.VVertexB.x)
                    ) continue;

                // Get start and end points of the edge
                VoronoiPoint start = new(edge.VVertexA);
                VoronoiPoint end   = new(edge.VVertexB);
                AddVertices(new List<VoronoiPoint>(){start, end}); 

                // Get cells associate to a given edge
                VoronoiCell leftCell = siteToCell[edge.LeftData];
                VoronoiCell rightCell = siteToCell[edge.RightData];

                // Create new edge
                VoronoiEdge newEdge = new(start, end);
                if (start == null) {
                    Debug.LogWarning($"Start of {newEdge.Name} is null");
                    continue;
                    }
                if (end == null) {
                    Debug.LogWarning($"End of {newEdge.Name} is null");
                    continue;
                    }
                if (start == end) {
                    Debug.LogWarning($"Start and end of {newEdge.Name} are the same");
                    continue;
                    }
                AddEdge(newEdge);

                if (leftCell != null && rightCell != null && leftCell != rightCell)
                {
                    // Add edge to both cells
                    leftCell .AddEdge(newEdge);
                    rightCell.AddEdge(newEdge);

                    // Add vertices to each cell
                    leftCell .AddVertex(this, newEdge.Start);
                    leftCell .AddVertex(this, newEdge.End);
                    rightCell.AddVertex(this, newEdge.Start);
                    rightCell.AddVertex(this, newEdge.End);
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
            // _triangleMap?.OnBeforeSerialize();
            _cellMap?.OnBeforeSerialize();
            _edgeMap?.OnBeforeSerialize();

            _sites     = _siteMap?.List ?? new();
            // _triangles  = _triangleMap?.List ?? new();
            _cells      = _cellMap?.List ?? new();
            _edges      = _edgeMap?.List ?? new();
        }

        public void OnAfterDeserialize()
        {
            _siteMap = new(_sites);
            // _triangleMap = new(_triangles);
            _cellMap = new(_cells);
            _edgeMap = new(_edges);

            // Rebuild maps
            _siteMap.OnAfterDeserialize();
            // _triangleMap.OnAfterDeserialize();
            _cellMap.OnAfterDeserialize(); 
            _edgeMap.OnAfterDeserialize();
        }
    }
}