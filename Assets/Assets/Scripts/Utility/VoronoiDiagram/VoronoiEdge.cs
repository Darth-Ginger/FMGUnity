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
        [SerializeField] public Vector2 Start { get; }
        [SerializeField] public Vector2 End { get; }
        [SerializeField] public VoronoiCell LeftCell { get; set; } // The cell on the left
        [SerializeField] public VoronoiCell RightCell { get; set; } // The cell on the right

        public VoronoiEdge(Vector2 start, Vector2 end)
        {
            Id = Guid.NewGuid();
            Start = start;
            End = end;
        }

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

        public string ToJSON()
        {
            return $"{{\"start\": {Start}, \"end\": {End}, \"leftCell\": {LeftCell}, \"rightCell\": {RightCell}}}";
        }
    }

}