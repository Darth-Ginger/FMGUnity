using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Utility class for creating meshes from internal geometric data structures 
    /// </summary>
    public static class GeometricStructureVisualizationUtility 
    {

        public static Mesh CreateDCELVisualization(DCEL dcel, float arrowThickness, float arrowHeadWidth, float arrowHeadLength, float edgeOffset, CardinalPlane plane = CardinalPlane.XZ)
        {
            Mesh mesh = new Mesh();

            int halfEdgeCount = dcel.edges.Length;

            CombineInstance[] combineInstances = new CombineInstance[halfEdgeCount];

            for(int i = 0; i < halfEdgeCount; i++)
            {
                var hEdge = dcel.edges[i];
                var vertexA = dcel.vertices[hEdge.vertexBack];
                var vertexB = dcel.vertices[hEdge.vertexFwd];

                var dir = vertexB - vertexA;
                dir = math.normalize(dir);
                var perp = dir.Perpendicular();
                vertexA += perp * edgeOffset * 0.5f;
                vertexB += perp * edgeOffset * 0.5f;
                vertexA += dir * edgeOffset * 0.5f;
                vertexB -= dir * edgeOffset * 0.5f;

                var ls2D = new LineSegment2D(vertexA, vertexB);

                var arrowMesh = MeshUtil.CreateArrow2D(ls2D, arrowThickness, arrowHeadLength, arrowHeadWidth, plane);
                combineInstances[i] = new CombineInstance()
                {
                    mesh = arrowMesh,
                    transform = Matrix4x4.identity,
                    subMeshIndex = 0,
                };
            }

            mesh.CombineMeshes(combineInstances, true, false, false);

            return mesh;
        }

    }
}
