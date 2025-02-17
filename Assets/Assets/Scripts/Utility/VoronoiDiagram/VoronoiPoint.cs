using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using FMGUnity.Utility.Serials;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiPoint : IIdentifiable
    {
        [SerializeField] private SerialGuid _id;
        [SerializeField] private Vector2 _position;
        [SerializeField] private List<SerialGuid> _cell;

        public SerialGuid Id => _id;
        public Vector2 Position => _position;
        public List<SerialGuid> Cell => _cell;

        // Constructor
        public VoronoiPoint(Vector2 position)
        {
            _id = SerialGuid.NewGuid();
            _position = position;
            _cell = new();
        }
        // Overloaded constructors
        public VoronoiPoint(float x, float y) : this(new Vector2(x, y)) { }
        public VoronoiPoint(int x, int y) : this(new Vector2(x, y)) { }

        // Adds a cell that the point belongs to
        public void PartOfCell(Guid cellId) => _cell.Add(cellId);
        public void PartOfCell(SerialGuid cellId) => _cell.Add(cellId);
        public void PartOfCell(VoronoiCell cell) => PartOfCell(cell.Id);

        // Checks if the point is part of a given cell
        public bool IsPartOfCell(Guid cellId) => Cell.Contains(cellId);
        public bool IsPartOfCell(SerialGuid cellId) => Cell.Contains(cellId);
        public bool IsPartOfCell(VoronoiCell cell) => IsPartOfCell(cell.Id);

        public static implicit operator Vector2(VoronoiPoint p) => p.Position;
        public static implicit operator VoronoiPoint(Vector2 v) => new(v);

        public override string ToString() => $"VoronoiPoint: Position={Position}";

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