using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    //Doubly-connected edge list (DCEL)
    //Loosely based on the book from Mark de Berg et al: "Computational Geometry"
    public struct DCEL : IDisposable
    {

        public NativeList<float2> vertices;
        public NativeList<HalfEdge> edges;
        public NativeList<FaceRecord> faces;

        public NativeList<int> incidentEdges;

        public DCEL(Allocator allocator)
        {
            this.vertices = new NativeList<float2>(allocator);
            this.edges = new NativeList<HalfEdge>(allocator);
            this.faces = new NativeList<FaceRecord>(allocator);
            this.incidentEdges = new NativeList<int>(allocator);
        }

        public DCEL(Allocator allocator, int vertexCapacity, int edgeCapacity, int facesCapacity)
        {
            this.vertices = new NativeList<float2>(vertexCapacity, allocator);
            this.edges = new NativeList<HalfEdge>(edgeCapacity, allocator);
            this.faces = new NativeList<FaceRecord>(facesCapacity, allocator);
            this.incidentEdges = new NativeList<int>(vertexCapacity, allocator);
        }

        public void Dispose()
        {
            this.vertices.DisposeIfCreated();
            this.edges.DisposeIfCreated();
            this.faces.DisposeIfCreated();
            this.incidentEdges.DisposeIfCreated();
        }


        public JobHandle Dispose(JobHandle dependsOn = default)
        {
            var dependencyA = this.vertices.Dispose(dependsOn);
            var dependencyB = this.edges.Dispose(dependencyA);
            var dependencyC = this.faces.Dispose(dependencyB);
            return this.incidentEdges.Dispose(dependencyC);
        }

        /// <summary>
        /// Merges two DCELs together - does not check if the edge list remains valid!
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        public unsafe static void Merge(DCEL a, DCEL b, ref DCEL mergedDCEL)
        {
            int vertexCountA = a.vertices.Length;
            int vertexCountB = b.vertices.Length;
            int edgeCountA = a.edges.Length;
            int edgeCountB = b.edges.Length;
            int faceCountA = a.faces.Length;
            int faceCountB = b.faces.Length;

            int mergedVertexCount = vertexCountA + vertexCountB;
            int mergedEdgeCount = edgeCountA + edgeCountB;
            int mergedFaceCount = faceCountA + faceCountB;

            mergedDCEL.vertices.Length = mergedVertexCount;
            mergedDCEL.edges.Length = mergedEdgeCount;
            mergedDCEL.faces.Length = mergedFaceCount;
            mergedDCEL.incidentEdges.Length = mergedVertexCount;

            UnsafeUtility.MemCpy(mergedDCEL.vertices.GetUnsafePtr(), a.vertices.GetUnsafePtr(), sizeof(float2) * vertexCountA);
            UnsafeUtility.MemCpy(mergedDCEL.edges.GetUnsafePtr(), a.edges.GetUnsafePtr(), sizeof(HalfEdge) * edgeCountA);
            UnsafeUtility.MemCpy(mergedDCEL.faces.GetUnsafePtr(), a.faces.GetUnsafePtr(), sizeof(FaceRecord) * faceCountA);
            UnsafeUtility.MemCpy(mergedDCEL.incidentEdges.GetUnsafePtr(), a.incidentEdges.GetUnsafePtr(), sizeof(int) * vertexCountA);

            for (int i = 0; i < vertexCountB; i++)
            {
                float2 vertexB = b.vertices[i];
                int incidentEdge = b.incidentEdges[i];
                mergedDCEL.vertices[i + vertexCountA] = vertexB;
                mergedDCEL.incidentEdges[i + vertexCountA] = incidentEdge + edgeCountA;
            }

            for (int i = 0; i < edgeCountB; i++)
            {
                var hEdge = b.edges[i];

                if (hEdge.fwd >= 0) hEdge.fwd += edgeCountA;
                if (hEdge.back >= 0) hEdge.back += edgeCountA;
                if (hEdge.twin >= 0) hEdge.twin += edgeCountA;

                hEdge.vertexFwd += vertexCountA;
                hEdge.vertexBack += vertexCountA;
                hEdge.face += faceCountA;

                mergedDCEL.edges[i + edgeCountA] = hEdge;
            }

            for (int i = 0; i < faceCountB; i++)
            {
                var face = b.faces[i];
                face.outerComponent += edgeCountA;
                for (int j = 0; j < face.innerComponents.Length; j++)
                {
                    int innerComponent = face.innerComponents[j];
                    innerComponent += edgeCountA;
                    face.innerComponents[j] = innerComponent;
                }
                mergedDCEL.faces[i + faceCountA] = face;
            }

        }


        public unsafe static DCEL Merge(DCEL a, DCEL b, Allocator allocator)
        {
            DCEL mergedDCEL = new DCEL(allocator);
            Merge(a, b, ref mergedDCEL);
            return mergedDCEL;
        }
    }
}
