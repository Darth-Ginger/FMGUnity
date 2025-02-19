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
        [SerializeField] private Vector2 _start_pos;
        [SerializeField] private string _end;
        [SerializeField] private Vector2 _end_pos;
        [SerializeField] private string _leftCell;
        [SerializeField] private string _rightCell;

        public string Start     => _start;
        public Vector2 StartPos => _start_pos;
        public string End       => _end;
        public Vector2 EndPos   => _end_pos;
        public string LeftCell  => _leftCell;
        public string RightCell => _rightCell;

        public VoronoiEdge(VoronoiPoint start, VoronoiPoint end, VoronoiCell leftCell = null, VoronoiCell rightCell = null)
        {
            if (start == end) throw new ArgumentException("Start and end points must be different.");
            if (leftCell != null && rightCell != null && leftCell == rightCell) throw new ArgumentException("Left and right cells must be different.");
            
            _start = start.Name;
            _start_pos = start.Position;
            _end = end.Name;
            _end_pos = end.Position;
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