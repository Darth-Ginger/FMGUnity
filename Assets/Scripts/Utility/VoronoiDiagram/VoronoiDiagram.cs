using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using System.Linq;

public class VoronoiDiagram
{
    [SerializeField] private Rect mapBounds;
    [SerializeField] private int numSites;
    [SerializeField] private int seed;

    // Private backing Arrays
    [SerializeField] private List<VoronoiSite>   sites;
    [SerializeField] private List<VoronoiVertex> vertices;
    [SerializeField] private List<VoronoiEdge>  edges;
    [SerializeField] private List<VoronoiCell>   cells;

    // Private mapping Dictionaries
    [SerializeField] public SerialMap<string, int> SiteIndexMap = new();
    [SerializeField] public SerialMap<string, int> VertexIndexMap = new();
    [SerializeField] public SerialMap<string, int> EdgeIndexMap = new();
    [SerializeField] public SerialMap<string, int> CellIndexMap = new();


    // Public properties
    public List<VoronoiSite>   Sites => sites;
    public List<VoronoiVertex> Vertices => vertices;
    public List<VoronoiEdge>  Edges => edges;
    public List<VoronoiCell>   Cells => cells;
    
    
    public Rect MapBounds => mapBounds;
    public int NumSites => numSites;
    public int Seed => seed;



    #region Constructors

    public VoronoiDiagram() {}

    public VoronoiDiagram(Rect mapBounds, int numSites, int seed)
    {
        this.mapBounds = mapBounds;
        this.numSites  = numSites;
        this.sites     = new();
        this.cells     = new();
        this.edges     = new();
        this.vertices  = new();
        this.seed      = seed;
    }

    public VoronoiDiagram(Rect mapBounds, int numSites, int seed, List<VoronoiSite> sites, List<VoronoiVertex> vertices, List<VoronoiEdge>edges, List<VoronoiCell> cells)
    {
        this.mapBounds = mapBounds;
        this.numSites  = numSites;
        this.seed      = seed;
        this.sites     = sites;
        this.vertices  = vertices;
        this.edges     = edges;
        this.cells     = cells;
    }

    #endregion

    #region Setters

    /// <summary>
    /// Adds a new Voronoi element (site, vertex, edge, or cell) to the diagram.
    /// </summary>
    /// <param name="obj">The Voronoi element to be added. It can be a VoronoiSite, VoronoiVertex, VoronoiEdge, or VoronoiCell.</param>
    /// <returns>
    /// <c>true</c> if the element was added successfully; <c>false</c> if an element with the same identifier already exists.
    /// </returns>
    public bool Add(object obj) 
    {
        switch (obj)
        {
            case VoronoiSite site when site != null:
                if (SiteIndexMap.ContainsKey(site.Id)) return false;
                sites.Add(site);
                return SiteIndexMap.Add(site.Id, sites.Count - 1);
            case VoronoiVertex vertex when vertex != null:
                if (VertexIndexMap.ContainsKey(vertex.Id)) return false;
                vertices.Add(vertex);
                return VertexIndexMap.Add(vertex.Id, vertices.Count - 1);
            case VoronoiEdge edge when edge != null:
                if (EdgeIndexMap.ContainsKey(edge.Id)) return false;
                edges.Add(edge);
                return EdgeIndexMap.Add(edge.Id, edges.Count - 1);
            case VoronoiCell cell when cell != null:
                if (CellIndexMap.ContainsKey(cell.Id)) return false;
                cells.Add(cell);
                return CellIndexMap.Add(cell.Id, cells.Count - 1);
            default:
                return false;
        }
    }
    public bool AddRange(IEnumerable<object> objs) => objs.All(Add);

    public void SetBounds(Rect bounds) => mapBounds = bounds;
    public void SetNumSites(int numSites) => this.numSites = numSites;
    public void SetSeed(int seed) => this.seed = seed;

    public void SetSites   (List<VoronoiSite> sites)        => this.sites = sites;
    public void SetVertices(List<VoronoiVertex> vertices)   => this.vertices = vertices;
    public void SetEdges   (List<VoronoiEdge> edges)        => this.edges = edges;
    public void SetCells   (List<VoronoiCell> cells)        => this.cells = cells;

    #endregion

    #region Getters
    
    public VoronoiSite GetSite(string id) 
    {
        bool exits = SiteIndexMap.TryGetValue(id, out int index);
        return exits ? sites[index] : null;
    }
    public VoronoiSite GetSite(int index) => sites[index];
    public VoronoiSite GetSite(Vector2 position) => GetSite($"VoronoiSite-{position}");

    public VoronoiVertex GetVertex(string id) 
    {
        bool exits = VertexIndexMap.TryGetValue(id, out int index);
        return exits ? vertices[index] : null;
    }
    public VoronoiVertex GetVertex(int index) => vertices[index];
    public VoronoiVertex GetVertex(Vector2 position) => GetVertex($"VoronoiVertex-{position}");

    public VoronoiEdge GetEdge(string id) 
    {
        bool exits = EdgeIndexMap.TryGetValue(id, out int index);
        return exits ? edges[index] : null;
    }
    public VoronoiEdge GetEdge(int index) => edges[index];
    public VoronoiEdge GetEdge(Vector2 start, Vector2 end) => GetEdge($"VoronoiEdge-{start}->{end}");


    public VoronoiCell GetCell(string id) 
    {
        bool exits = CellIndexMap.TryGetValue(id, out int index);
        return exits ? cells[index] : null;
    }
    public VoronoiCell GetCell(int index) => cells[index]; 
    public VoronoiCell GetCell(VoronoiSite site) => GetCell(site.Id);
    public VoronoiCell GetCell(Vector2 position) => GetCell($"VoronoiCell-{position}");

    public List<VoronoiCell>   GetCells() => cells;
    public List<VoronoiSite>   GetSites() => sites;
    public List<VoronoiVertex> GetVertices() => vertices;
    public List<VoronoiEdge>  GetEdges() => edges;
    public string[] GetSiteIds() => SiteIndexMap.Keys.ToArray();
    public string[] GetVertexIds() => VertexIndexMap.Keys.ToArray();
    public string[] GetEdgeIds() => EdgeIndexMap.Keys.ToArray();
    public string[] GetCellIds() => CellIndexMap.Keys.ToArray();


    #endregion

    #region Public Methods

    public bool Contains(VoronoiSite site) => SiteIndexMap.ContainsKey(site.Id);
    public bool Contains(VoronoiVertex vertex) => VertexIndexMap.ContainsKey(vertex.Id);
    public bool Contains(VoronoiEdge edge) => EdgeIndexMap.ContainsKey(edge.Id);
    public bool Contains(VoronoiCell cell) => CellIndexMap.ContainsKey(cell.Id);
    public bool Contains(string id) => 
        SiteIndexMap.ContainsKey(id)    || 
        VertexIndexMap.ContainsKey(id)  || 
        EdgeIndexMap.ContainsKey(id)    || 
        CellIndexMap.ContainsKey(id);


    public void Clear()
    {
        sites    = new();
        vertices = new();
        edges    = new();
        cells    = new();
        SiteIndexMap?.Clear();
        VertexIndexMap?.Clear();
        EdgeIndexMap?.Clear();
        CellIndexMap?.Clear();
    }
    #endregion


    #region Serialization

    public string Serialize()
    {
        return JsonUtility.ToJson(this);
    }

    public static VoronoiDiagram Deserialize(string json)
    {
        return JsonUtility.FromJson<VoronoiDiagram>(json);
    }

    #endregion

}
