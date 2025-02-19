using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using FMGUnity.Utility.Serials;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiPoint : Identifiable
    {
        [SerializeField] private Vector2 _position;
        [SerializeField] private List<string> _cell;

        public Vector2 Position => _position;
        public List<string> Cell => _cell;

        // Constructor
        public VoronoiPoint(Vector2 position)
        {
            _position = position;
            _cell = new();
            Initialize("VoronoiPoint", position);
        }
        // Overloaded constructors
        public VoronoiPoint(float x, float y) : this(new Vector2(x, y)) { }
        public VoronoiPoint(int x, int y) : this(new Vector2(x, y)) { }

        // Adds a cell that the point belongs to

        public void PartOfCell(string cellId) => _cell.Add(cellId);
        public void PartOfCell(VoronoiCell cell) => PartOfCell(cell.Name);

        // Checks if the point is part of a given cell
        public bool IsPartOfCell(string cellId) => Cell.Contains(cellId);
        public bool IsPartOfCell(VoronoiCell cell) => IsPartOfCell(cell.Name);

        public static implicit operator Vector2(VoronoiPoint p) => p.Position;
        public static implicit operator VoronoiPoint(Vector2 v) => new(v);

        public override string ToString() => $"VoronoiPoint: Name={Name}, Position={Position}, Within cells: {Cell}";

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