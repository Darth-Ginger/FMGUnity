
using System;
using System.Collections.Generic;
using FMGUnity.Utility.Interfaces;
using FMGUnity.Utility.Serials;
using UnityEngine;

namespace FMGUnity.Utility
{
    [System.Serializable]
    public class VoronoiCell: Identifiable
    {

        [SerializeField] public Vector2 Site; // Voronoi site
        [SerializeField] public List<Vector2> Vertices; // Cell vertices
        [SerializeField] public List<string> Neighbors { get; } // Adjacent cells

        public VoronoiCell(Vector2 site)
        {
            Site = site;
            Vertices = new List<Vector2>();
            Neighbors = new();
            Initialize("VoronoiCell", site);
        }

        public void AddNeighbor(VoronoiCell neighbor)
        {
            if (neighbor != this && !Neighbors.Contains(neighbor.Name))
            {
                Neighbors.Add(neighbor.Name);
            }
        }

        public void AddNeighbor(VoronoiDiagram diagram, string cellId) => AddNeighbor(diagram.GetCell(cellId));
        
        public bool HasNeighbor(string cellId) => Neighbors.Contains(cellId);
        public bool HasNeighbor(VoronoiCell cell) => Neighbors.Contains(cell.Name);

        public override string ToString()
        {
            string neighbors = "";
            foreach (var neighbor in Neighbors)
            {
                neighbors += neighbor + ", ";
            }
            return $"VoronoiCell: Name= {Name}, Site= {Site}, Vertices= {Vertices.Count}, Neighbors({Neighbors.Count})= {neighbors}";
        }

    }
}