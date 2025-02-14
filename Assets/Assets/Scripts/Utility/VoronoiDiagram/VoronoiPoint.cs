using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using UnityEngine;

public class VoronoiPoint : IIdentifiable
{
    public Guid Id { get; private set; }
    public Vector2 Position { get; set; }
    public List<int> Cell { get; set; }

    // Constructor
    public VoronoiPoint(Vector2 position)
    {
        Id = Guid.NewGuid();
        Position = position;
        Cell = new List<int>();
    }
    // Overloaded constructors
    public VoronoiPoint(float x, float y) : this(new Vector2(x, y)) { }
    public VoronoiPoint(int x, int y) : this(new Vector2(x, y)) { }

    public static implicit operator Vector2(VoronoiPoint p)
    {
        return p.Position;
    }

    public override string ToString()
    {
        return $"VoronoiPoint: Position={Position}";
    }

    public override bool Equals(object obj)
    {
        if (obj is VoronoiPoint other)
        {
            return (Position == other.Position) || (Id == other.Id);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode() ^ Id.GetHashCode();
    }
    
}
