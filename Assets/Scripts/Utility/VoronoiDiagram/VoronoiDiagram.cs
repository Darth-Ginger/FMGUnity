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
    [SerializeField] private VoronoiSite[]   sites;
    [SerializeField] private VoronoiVertex[] vertices;
    [SerializeField] private VoronoiEdge[]   edges;
    [SerializeField] private VoronoiCell[]   cells;

    // Private mapping Dictionaries
    [SerializeField] public SerialMap<string, int> SiteIndexMap;
    [SerializeField] public SerialMap<string, int> VertexIndexMap;
    [SerializeField] public SerialMap<string, int> EdgeIndexMap;
    [SerializeField] public SerialMap<string, int> CellIndexMap;


    // Public properties
    public VoronoiSite[]   Sites => sites;
    public VoronoiVertex[] Vertices => vertices;
    public VoronoiEdge[]   Edges => edges;
    public VoronoiCell[]   Cells => cells;
    
    
    public Rect MapBounds => mapBounds;
    public int NumSites => numSites;
    public int Seed => seed;



    #region Constructors

    public VoronoiDiagram() {}

    public VoronoiDiagram(Rect mapBounds, int numSites, int seed)
    {
        this.mapBounds = mapBounds;
        this.numSites  = numSites;
        this.sites     = new VoronoiSite[numSites];
        this.cells     = new VoronoiCell[numSites];
        this.seed      = seed;
    }

    public VoronoiDiagram(Rect mapBounds, int numSites, int seed, VoronoiSite[] sites, VoronoiVertex[] vertices, VoronoiEdge[] edges, VoronoiCell[] cells)
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
            case VoronoiSite site:
                if (SiteIndexMap.ContainsKey(site.Id)) return false;
                sites.Append(site);
                return SiteIndexMap.Add(site.Id, sites.Length - 1);
            case VoronoiVertex vertex:
                if (VertexIndexMap.ContainsKey(vertex.Id)) return false;
                vertices.Append(vertex);
                return VertexIndexMap.Add(vertex.Id, vertices.Length - 1);
            case VoronoiEdge edge:
                if (EdgeIndexMap.ContainsKey(edge.Id)) return false;
                edges.Append(edge);
                return EdgeIndexMap.Add(edge.Id, edges.Length - 1);
            case VoronoiCell cell:
                if (CellIndexMap.ContainsKey(cell.Id)) return false;
                cells.Append(cell);
                return CellIndexMap.Add(cell.Id, cells.Length - 1);
            default:
                return false;
        }
    }
    public bool AddRange(IEnumerable<object> objs) => objs.All(Add);

    public void SetBounds(Rect bounds) => mapBounds = bounds;
    public void SetNumSites(int numSites) => this.numSites = numSites;
    public void SetSeed(int seed) => this.seed = seed;

    public void SetSites(VoronoiSite[] sites) => this.sites = sites;
    public void SetVertices(VoronoiVertex[] vertices) => this.vertices = vertices;
    public void SetEdges(VoronoiEdge[] edges) => this.edges = edges;
    public void SetCells(VoronoiCell[] cells) => this.cells = cells;

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

    public VoronoiCell[]   GetCells() => cells;
    public VoronoiSite[]   GetSites() => sites;
    public VoronoiVertex[] GetVertices() => vertices;
    public VoronoiEdge[]   GetEdges() => edges;
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
        sites = new VoronoiSite[0];
        vertices = new VoronoiVertex[0];
        edges = new VoronoiEdge[0];
        cells = new VoronoiCell[0];
        SiteIndexMap.Clear();
        VertexIndexMap.Clear();
        EdgeIndexMap.Clear();
        CellIndexMap.Clear();
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