
using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiCell: IIdentifiable
    {
        public Guid Id { get; set; }
        [SerializeField] public Vector2 Site; // Voronoi site
        [SerializeField] public List<Vector2> Vertices; // Cell vertices
        [SerializeField] public HashSet<VoronoiCell> Neighbors { get; } // Adjacent cells

        public VoronoiCell(Vector2 site)
        {
            Id = Guid.NewGuid();
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
            string neighbors = "";
            foreach (var neighbor in Neighbors)
            {
                neighbors += neighbor.Site + ", ";
            }
            return $"VoronoiCell: Site= {Site}, Vertices= {Vertices.Count}, Neighbors({Neighbors.Count})= {neighbors}";
        }

        public string ToJson()
        {
            return $"{{\"site\": {Site}, \"vertices\": {Vertices}, \"neighbors\": {Neighbors}}}";
        }
    }
}