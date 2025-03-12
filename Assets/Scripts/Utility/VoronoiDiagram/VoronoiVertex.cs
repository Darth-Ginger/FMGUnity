using NaughtyAttributes;
using UnityEngine;

public class VoronoiVertex : IIdentifiable
{
    [ShowNonSerializedField] protected VoronoiDiagram diagram;
    [SerializeField] protected string id = "null";
    [SerializeField] protected Vector2 position;
    protected string positionString = "null";

    public string Id => id;
    public Vector2 Position => position;
    public float X => position.x;
    public float Y => position.y;
    public int Index => diagram.VertexIndexMap.TryGetValueIndex(id, out int index) ? index : -1;

    #region Constructors

    public VoronoiVertex(Vector2 position)
    {
        this.position = position;
        this.id = SetId();
    }

    public VoronoiVertex(VoronoiDiagram diagram, Vector2 position) : this(position) => this.diagram = diagram;

    public VoronoiVertex(float x, float y) : this(new Vector2(x, y)) {}

    public VoronoiVertex(VoronoiDiagram diagram, float x, float y) : this(diagram, new Vector2(x, y)) {}

    #endregion

    #region Setters

    public void SetDiagram(VoronoiDiagram diagram) => this.diagram = diagram;
    public string SetId() 
    {
        if (positionString == "null")
            positionString = $"{Position}";
            return $"VoronoiVertex-{positionString}"; ;
    }

    

    #endregion

    #region Getters

    #endregion

    #region Public Methods

    public bool Initialize()
    {
        if (positionString == "null") return false;
        id = SetId();        
        return true;
    }

    public bool Initialize(VoronoiDiagram diagram)
    {
        SetDiagram(diagram);
        return Initialize();
    }

    #endregion

}
