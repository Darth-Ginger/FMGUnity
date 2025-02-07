
using System.Collections.Generic;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiCell
    {
        [SerializeField] public Vector2 Site; // Voronoi site
        [SerializeField] public List<Vector2> Vertices; // Cell vertices
        [SerializeField] public HashSet<VoronoiCell> Neighbors { get; } // Adjacent cells

        public VoronoiCell(Vector2 site)
        {
            Site = site;
            Vertices = new List<Vector2>();
            Neighbors = new HashSet<VoronoiCell>();
        }

        public void AddNeighbor(VoronoiCell neighbor)
        {
            if (neighbor != this)
            {
                Neighbors.Add(neighbor);
            }
        }

        public override string ToString()
        {
            return $"VoronoiCell: Site={Site}, Vertices={Vertices.Count}";
        }
    }
}