using System;
using UnityEngine;
using NaughtyAttributes;
using FMGUnity.Utility.Interfaces;
using FMGUnity.Utility.Serials;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiEdge: Identifiable
    {
        [SerializeField] private string _start;
        [SerializeField] private string _end;
        [SerializeField] private string _leftCell;
        [SerializeField] private string _rightCell;

        [SerializeField] public string Start     => _start;
        [SerializeField] public string End       => _end;
        [SerializeField] public string LeftCell  => _leftCell;
        [SerializeField] public string RightCell => _rightCell;

        public VoronoiEdge(VoronoiPoint start, VoronoiPoint end, VoronoiCell leftCell = null, VoronoiCell rightCell = null)
        {
            _start = start.Name;
            _end = end.Name;
            _leftCell = leftCell?.Name ?? string.Empty;
            _rightCell = rightCell?.Name ?? string.Empty;
            Initialize("VoronoiEdge", _start, _end);
        }

        public enum CellSide { Left, Right }

        public void SetCell(CellSide side = CellSide.Left, string cellId = default)
        {
            if (cellId != default)
            {
                if (side == CellSide.Left) _leftCell = cellId;
                else _rightCell = cellId;
            }
        }
        public void SetCell(CellSide side = CellSide.Left, VoronoiCell cell = null) => SetCell(side, cell?.Name ?? string.Empty);
        public void SetLeft(string cellId) => SetCell(CellSide.Left, cellId);
        public void SetLeft(VoronoiCell cell) => SetCell(CellSide.Left, cell);
        public void SetRight(string cellId) => SetCell(CellSide.Right, cellId);
        public void SetRight(VoronoiCell cell) => SetCell(CellSide.Right, cell);

        public override string ToString()
        {
            return $"VoronoiEdge: Name={Name} : Start={Start} => End={End} : LeftCell={LeftCell} : RightCell={RightCell}";
        }

    }

}