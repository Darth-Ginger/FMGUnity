using NaughtyAttributes;
using UnityEngine;

public class VoronoiEdge : IIdentifiable
{
    [ShowNonSerializedField] private VoronoiDiagram diagram;
    [SerializeField]         private string id = "null";
    [SerializeField]         private string startVertex = "null";
    [SerializeField]         private string endVertex   = "null";
    [SerializeField]         private string leftSite    = "null";
    [SerializeField]         private string rightSite   = "null";

    public string Id => id;
    public string StartVertexId => startVertex;
    public string EndVertexId   => endVertex;
    public string LeftSiteId    => leftSite;
    public string RightSiteId   => rightSite;

    public int Index => diagram.EdgeIndexMap.TryGetValue(id, out int index) ? index : -1;

    public string SetId() => $"VoronoiEdge-{startVertex}->{endVertex}";

    #region Constructors
    public VoronoiEdge() {}

    public VoronoiEdge(VoronoiDiagram diagram, string startVertex, string endVertex)
    {
        this.diagram     = diagram;
        this.startVertex = startVertex;
        this.endVertex   = endVertex;
        this.id          = SetId();
    }

    public VoronoiEdge(VoronoiSite leftSite, VoronoiSite rightSite) : base()
    {
        this.leftSite    = leftSite.Id;
        this.rightSite   = rightSite.Id;
    }
    #endregion

    #region Setters

    /// <summary>
    /// Sets the start vertex of the edge. The start vertex should be a valid <see cref="VoronoiVertex"/> identifier.
    /// </summary>
    /// <param name="startVertex">The start vertex of the edge.</param>
    /// <returns><c>true</c> if the start vertex was set successfully, <c>false</c> otherwise.</returns>
    public bool SetStartVertex(string startVertex)
    {
        if (!startVertex.Contains("VoronoiVertex") || 
            this.startVertex != "null") 
                return false;

        this.startVertex = startVertex;
        return true;
    }
    public bool SetStartVertex(VoronoiVertex startVertex) => SetStartVertex(startVertex.Id);

    /// <summary>
    /// Sets the end vertex of the edge. The end vertex should be a valid <see cref="VoronoiVertex"/> identifier.
    /// </summary>
    /// <param name="endVertex">The end vertex of the edge.</param>
    /// <returns><c>true</c> if the end vertex was set successfully, <c>false</c> otherwise.</returns>
    public bool SetEndVertex(string endVertex)
    {
        if (!endVertex.Contains("VoronoiVertex") ||
            this.endVertex != "null") 
                return false;

        this.endVertex = endVertex;
        return true;
    }
    public bool SetEndVertex(VoronoiVertex endVertex) => SetEndVertex(endVertex.Id);

    /// <summary>
    /// Sets the left site of the edge. The left site should be a valid <see cref="VoronoiSite"/> identifier.
    /// </summary>
    /// <param name="leftSite">The left site of the edge.</param>
    /// <returns><c>true</c> if the left site was set successfully, <c>false</c> otherwise.</returns>
    public bool SetLeftSite(string leftSite)
    {
        if (!leftSite.Contains("VoronoiSite") ||
            this.leftSite != "null") 
                return false;

        this.leftSite = leftSite;
        return true;
    }
    public bool SetLeftSite(VoronoiSite leftSite) => SetLeftSite(leftSite.Id);


    /// <summary>
    /// Sets the right site of the edge. The right site should be a valid <see cref="VoronoiSite"/> identifier.
    /// </summary>
    /// <param name="rightSite">The right site of the edge.</param>
    /// <returns><c>true</c> if the right site was set successfully, <c>false</c> otherwise.</returns>
    public bool SetRightSite(string rightSite)
    {
        if (!rightSite.Contains("VoronoiSite") ||
            this.rightSite != "null") 
                return false;

        this.rightSite = rightSite;
        return true;
    }
    public bool SetRightSite(VoronoiSite rightSite) => SetRightSite(rightSite.Id);
    public void SetDiagram(VoronoiDiagram diagram) => this.diagram = diagram;

    #endregion

    #region Public Methods

    public bool Initialize()
    {
        if (startVertex == "null" || endVertex == "null") return false;

        id = SetId();
        return true;
    }

    public bool Initialize(VoronoiDiagram diagram)
    {
        SetDiagram(diagram);
        return Initialize();
    }

    /// <summary>
    /// Calculates the length of the edge. The length is calculated by taking the distance between the two vertices that make up the edge.
    /// </summary>
    /// <returns>The length of the edge.</returns>
    public float EdgeLength()
    {
        if (startVertex == "null" || endVertex == "null") return 0f;

        var start = diagram.GetVertex(startVertex).Position;
        var end   = diagram.GetVertex(endVertex).Position;

        return Vector2.Distance(start, end);
    }

    /// <summary>
    /// Checks whether a point is on the edge within a given tolerance. The algorithm used is to calculate the cross product of the line segment and the point vector. If the cross product is close to zero, the point is on the edge. Additionally, the algorithm checks if the point is within the bounds of the line segment.
    /// </summary>
    /// <param name="point">The point to check.</param>
    /// <param name="tolerance">The tolerance for the cross product calculation. Defaults to 0.001f.</param>
    /// <returns><c>true</c> if the point is on the edge within the given tolerance, <c>false</c> otherwise.</returns>
    public bool PointOnEdge(Vector2 point, float tolerance = 0.001f)
    {
        // Check that point has both start and end vertices
        if (startVertex == "null" || endVertex == "null") return false;

        // Calculate the Cross Product (xProduct). If close to zero, point is on the edge.
        Vector2 lineVector = diagram.GetVertex(endVertex).Position - diagram.GetVertex(startVertex).Position;
        Vector2 pointVector = point - diagram.GetVertex(startVertex).Position;

        float xProduct = lineVector.x * pointVector.y - lineVector.y * pointVector.x;

        // Check if point within bound of line segment
        float dotProduct = Vector2.Dot(pointVector, lineVector);
        float lineMagnitudeSquarted = lineVector.sqrMagnitude;

        if (dotProduct < 0 || dotProduct > lineMagnitudeSquarted) return false;

        return Mathf.Abs(xProduct) <= tolerance;
    }
    #endregion
    
}
