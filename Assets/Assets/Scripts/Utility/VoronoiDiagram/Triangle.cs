using UnityEngine;

namespace FMGUnity.Utility
{
    using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Triangle
{
    // Vertices of the triangle
    [SerializeField] public Vector2[] Vertices { get; private set; }

    // Cached circumcenter and circumradius
    [SerializeField] private Vector2? _circumcenter;
    [SerializeField] private float? _circumradiusSquared;

    public Triangle(Vector2 v1, Vector2 v2, Vector2 v3)
    {
        Vertices = new Vector2[] { v1, v2, v3 };
    }

    /// <summary>
    /// Gets all edges of the triangle.
    /// </summary>
    public List<Edge> GetEdges()
    {
        return new List<Edge>
        {
            new Edge(Vertices[0], Vertices[1]),
            new Edge(Vertices[1], Vertices[2]),
            new Edge(Vertices[2], Vertices[0])
        };
    }

    /// <summary>
    /// Calculates the circumcenter of the triangle.
    /// </summary>
    public Vector2 GetCircumcenter()
    {
        if (_circumcenter.HasValue)
        {
            return _circumcenter.Value;
        }

        Vector2 a = Vertices[0];
        Vector2 b = Vertices[1];
        Vector2 c = Vertices[2];

        // Midpoints of two edges
        Vector2 midAB = (a + b) / 2f;
        Vector2 midBC = (b + c) / 2f;

        // Perpendicular directions
        Vector2 dirAB = b - a;
        Vector2 perpAB = new Vector2(-dirAB.y, dirAB.x);

        Vector2 dirBC = c - b;
        Vector2 perpBC = new Vector2(-dirBC.y, dirBC.x);

        // Solve for intersection of two lines: midAB + t * perpAB = midBC + s * perpBC
        float t = (midBC.x - midAB.x) * perpBC.y - (midBC.y - midAB.y) * perpBC.x;
        t /= (perpAB.x * perpBC.y - perpAB.y * perpBC.x);

        // Compute circumcenter
        _circumcenter = midAB + t * perpAB;
        return _circumcenter.Value;
    }

    /// <summary>
    /// Checks if a point is inside the circumcircle of the triangle.
    /// </summary>
    public bool IsPointInCircumcircle(Vector2 point)
    {
        if (!_circumradiusSquared.HasValue)
        {
            Vector2 circumcenter = GetCircumcenter();
            _circumradiusSquared = (Vertices[0] - circumcenter).sqrMagnitude; // Distance squared
        }

        float distanceSquared = (point - GetCircumcenter()).sqrMagnitude;
        return distanceSquared <= _circumradiusSquared.Value;
    }

    /// <summary>
    /// Checks if the triangle contains a specific vertex.
    /// </summary>
    public bool ContainsVertex(Vector2 vertex)
    {
        return Vertices[0] == vertex || Vertices[1] == vertex || Vertices[2] == vertex;
    }

    /// <summary>
    /// Checks if the triangle shares an edge with another triangle.
    /// </summary>
    public bool SharesEdgeWith(Triangle other)
    {
        int sharedCount = 0;

        foreach (var edge1 in GetEdges())
        {
            foreach (var edge2 in other.GetEdges())
            {
                if (edge1.Equals(edge2))
                {
                    sharedCount++;
                }
            }
        }

        return sharedCount == 1; // Exactly one shared edge
    }

    public override string ToString()
    {
        return $"Triangle: ({Vertices[0]}, {Vertices[1]}, {Vertices[2]})";
    }
}

}