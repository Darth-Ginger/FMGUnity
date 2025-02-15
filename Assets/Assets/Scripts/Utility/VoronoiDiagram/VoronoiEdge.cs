using System;
using UnityEngine;
using NaughtyAttributes;
using FMGUnity.Utility.Interfaces;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiEdge: IIdentifiable
    {
        public Guid Id { get; private set; }
        [SerializeField] public Guid Start { get; }
        [SerializeField] public Guid End { get; }
        [SerializeField] public Guid LeftCell  { get; private set; } // The cell on the left
        [SerializeField] public Guid RightCell { get; private set; } // The cell on the right

        public VoronoiEdge(VoronoiPoint start, VoronoiPoint end, VoronoiCell leftCell = null, VoronoiCell rightCell = null)
        {
            Id = Guid.NewGuid();
            Start = start.Id;
            End = end.Id;
            LeftCell = leftCell?.Id ?? Guid.Empty;
            RightCell = rightCell?.Id ?? Guid.Empty;
        }

        public enum CellSide { Left, Right }

        public void SetCell(CellSide side = CellSide.Left, Guid cellId = default)
        {
            if (cellId != default)
            {
                if (side == CellSide.Left) LeftCell = cellId;
                else RightCell = cellId;
            }
        }
        public void SetCell(CellSide side = CellSide.Left, VoronoiCell cell = null) => SetCell(side, cell?.Id ?? Guid.Empty);
        public void SetLeft(Guid cellId) => SetCell(CellSide.Left, cellId);
        public void SetLeft(VoronoiCell cell) => SetCell(CellSide.Left, cell);
        public void SetRight(Guid cellId) => SetCell(CellSide.Right, cellId);
        public void SetRight(VoronoiCell cell) => SetCell(CellSide.Right, cell);


        public override bool Equals(object obj)
        {
            if (obj is VoronoiEdge other)
            {
                return (Start == other.Start && End == other.End) ||
                    (Start == other.End && End == other.Start);
            }
            return false;
        }

        public override string ToString()
        {
            return $"VoronoiEdge: Start={Start} => End={End} : LeftCell={LeftCell} : RightCell={RightCell}";
        }

        public override int GetHashCode() => Start.GetHashCode() ^ End.GetHashCode();

    }

}