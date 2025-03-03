using NaughtyAttributes;
using UnityEngine;

public class VoronoiCell : IIdentifiable
{
    // Private fields
    [ShowNonSerializedField] private VoronoiDiagram diagram;
    [SerializeField]         private string id = "null";

    // Public properties
    public string Id  => id;
    [SerializeField] public string Site { get; private set; } = "null";
    [SerializeField] public SerialHashSet<string> Edges { get; private set; }
    [SerializeField] public SerialHashSet<string> Neighbours { get; private set; }
    [SerializeField] public SerialHashSet<string> Vertices { get; private set; }
    public int Index => diagram.CellIndexMap.TryGetValueIndex(id, out int index) ? index : -1;

    #region Constructors

    public VoronoiCell() {}

    public VoronoiCell(string site)
    {
        Site       = site;
        Edges      = new();
        Neighbours = new();
        Vertices   = new();
        id = SetId();
    }

    public VoronoiCell(VoronoiSite site) : this(site.Id) {}

    public VoronoiCell(VoronoiSite site, VoronoiDiagram diagram) : this(site) => this.diagram = diagram;

    #endregion

    #region Setters

    public void SetDiagram(VoronoiDiagram diagram) => this.diagram = diagram;
    public void SetSite(string site) => Site = Site == "null" ? site : Site;
    public void AddEdge(string edge)
    {
        if (edge.Contains("VoronoiEdge") && diagram.Contains(edge))
        {
            Edges.Add(edge);
        }
    }
    public void AddEdge(VoronoiEdge edge) => AddEdge(edge.Id);
    public void AddEdge(Vector2 start, Vector2 end) => AddEdge(diagram.GetEdge(start, end).Id);

    public void AddNeighbour(string neighbour)
    {
        if (neighbour.Contains("VoronoiCell") && diagram.Contains(neighbour))
        {
            Neighbours.Add(neighbour);
        }
    }
    public void AddNeighbour(VoronoiCell neighbour) => AddNeighbour(neighbour.Id);
    public void AddVertex(string vertex)
    {
        if (vertex.Contains("VoronoiVertex") && diagram.Contains(vertex))
        {
            Vertices.Add(vertex);
        }
    }
    public void AddVertex(VoronoiVertex vertex) => AddVertex(vertex.Id);
    public void AddVertex(Vector2 vertex) => AddVertex(diagram.GetVertex(vertex).Id);
    public string SetId() => $"VoronoiCell-{Site}";

    #endregion


    #region Getters



    #endregion

    #region Public Methods

    public bool ContainsVertex(VoronoiVertex vertex) => Vertices.Contains(vertex.Id);
    public bool ContainsEdge(VoronoiEdge edge) => Edges.Contains(edge.Id);
    public bool ContainsNeighbour(VoronoiCell neighbour) => Neighbours.Contains(neighbour.Id);
    public override string ToString() => $"VoronoiCell-{Site}";
    public bool Initialize()
    {
        bool success = false;
        if (Site == "null") return success;
        id = SetId();
        success = true;
        return success;
    }

    public bool Initialize(VoronoiDiagram diagram)
    {
        SetDiagram(diagram);
        return Initialize();
    }

    #endregion
}

