using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiPoint : IIdentifiable
    {
        [SerializeField] public Guid Id { get; private set; }
        [SerializeField] public Vector2 Position { get; set; }
        [SerializeField] public HashSet<Guid> Cell { get; set; }

        // Constructor
        public VoronoiPoint(Vector2 position)
        {
            Id = Guid.NewGuid();
            Position = position;
            Cell = new HashSet<Guid>();
        }
        // Overloaded constructors
        public VoronoiPoint(float x, float y) : this(new Vector2(x, y)) { }
        public VoronoiPoint(int x, int y) : this(new Vector2(x, y)) { }

        // Adds a cell that the point belongs to
        public void PartOfCell(Guid cellId) => Cell.Add(cellId);
        public void PartOfCell(VoronoiCell cell) => PartOfCell(cell.Id);

        // Checks if the point is part of a given cell
        public bool IsPartOfCell(Guid cellId) => Cell.Contains(cellId);
        public bool IsPartOfCell(VoronoiCell cell) => IsPartOfCell(cell.Id);

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
}