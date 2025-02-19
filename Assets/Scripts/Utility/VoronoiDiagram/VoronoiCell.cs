
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
        [SerializeField] private Vector2 _site;
        [SerializeField] private List<string> _vertices;
        [SerializeField] private List<string> _neighbors;

        public Vector2 Site => _site;
        public List<string> Vertices => _vertices;
        public List<string> Neighbors => _neighbors;

        public VoronoiCell(Vector2 site)
        {
            _site = site;
            _vertices = new();
            _neighbors = new();
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

        public void AddVertex(VoronoiPoint vertex) 
        {
            if (!_neighbors.Contains(vertex.Name))
            {
                _neighbors.Add(vertex.Name);
            }
        }
        public void AddVertex(VoronoiDiagram diagram, string pointId) => AddVertex(diagram.GetPoint(pointId));

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