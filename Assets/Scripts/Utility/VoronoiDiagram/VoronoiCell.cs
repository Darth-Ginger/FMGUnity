
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
        [SerializeField] private List<string> _edges;
        [SerializeField] private List<string> _neighbors;

        public Vector2 Site => _site;
        public List<string> Vertices => _vertices;
        public List<string> Edges => _edges;
        public List<string> Neighbors => _neighbors;

        public VoronoiCell(Vector2 site)
        {
            _site = site;
            _vertices = new();
            _neighbors = new();
            _edges = new();
            Initialize("VoronoiCell", site);
        }

        public VoronoiCell(VoronoiPoint site) : this(site.Position) { }

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
            if (vertex != null && !_vertices.Contains(vertex.Name))
            {
                _vertices.Add(vertex.Name);
            }
        }
        public void AddVertex(VoronoiDiagram diagram, string pointId) => AddVertex(diagram.GetPoint(pointId));
        public void AddEdge(VoronoiEdge edge) => _edges.Add(edge.Name);
        public void AddEdge(VoronoiDiagram diagram, string edgeId) => AddEdge(diagram.GetEdge(edgeId));

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