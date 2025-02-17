using System;
using UnityEngine;
using NaughtyAttributes;
using FMGUnity.Utility.Interfaces;
using FMGUnity.Utility.Serials;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiEdge: IIdentifiable
    {
        [SerializeField] private SerialGuid _id;
        [SerializeField] private SerialGuid _start;
        [SerializeField] private SerialGuid _end;
        [SerializeField] private SerialGuid _leftCell;
        [SerializeField] private SerialGuid _rightCell;
        public SerialGuid Id => _id;
        [SerializeField] public SerialGuid Start => _start;
        [SerializeField] public SerialGuid End => _end;
        [SerializeField] public SerialGuid LeftCell  => _leftCell;
        [SerializeField] public SerialGuid RightCell => _rightCell;

        public VoronoiEdge(VoronoiPoint start, VoronoiPoint end, VoronoiCell leftCell = null, VoronoiCell rightCell = null)
        {
            _id = SerialGuid.NewGuid();
            _start = start.Id;
            _end = end.Id;
            _leftCell = leftCell?.Id ?? Guid.Empty;
            _rightCell = rightCell?.Id ?? Guid.Empty;
        }

        public enum CellSide { Left, Right }

        public void SetCell(CellSide side = CellSide.Left, Guid cellId = default)
        {
            if (cellId != default)
            {
                if (side == CellSide.Left) _leftCell = cellId;
                else _rightCell = cellId;
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