
using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiCell: IIdentifiable
    {
        public Guid Id { get; private set; }
        [SerializeField] public Vector2 Site; // Voronoi site
        [SerializeField] public List<Vector2> Vertices; // Cell vertices
        [SerializeField] public HashSet<Guid> Neighbors { get; } // Adjacent cells

        public VoronoiCell(Vector2 site)
        {
            Id = Guid.NewGuid();
            Site = site;
            Vertices = new List<Vector2>();
            Neighbors = new();
        }

        public void AddNeighbor(VoronoiCell neighbor)
        {
            if (neighbor != this)
            {
                Neighbors.Add(neighbor.Id);
            }
        }

        public void AddNeighbor(VoronoiDiagram diagram, Guid cellId) => AddNeighbor(diagram.GetCell(cellId));
        
        public bool HasNeighbor(Guid cellId) => Neighbors.Contains(cellId);
        public bool HasNeighbor(VoronoiCell cell) => Neighbors.Contains(cell.Id);

        public override string ToString()
        {
            string neighbors = "";
            foreach (var neighbor in Neighbors)
            {
                neighbors += neighbor + ", ";
            }
            return $"VoronoiCell: Site= {Site}, Vertices= {Vertices.Count}, Neighbors({Neighbors.Count})= {neighbors}";
        }

    }
}