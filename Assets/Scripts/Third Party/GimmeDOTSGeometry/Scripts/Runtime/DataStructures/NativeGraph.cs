using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    public struct GraphVertex
    {
        public int dataPtr;

        public UnsafeList<int> vertexPointers;
    }

    public struct NativeGraph : IDisposable
    {
        public Allocator allocator;

        public NativeList<GraphVertex> vertices;

        public NativeGraph(Allocator allocator)
        {
            this.vertices = new NativeList<GraphVertex>(allocator);
            this.allocator = allocator;
        }

        public void AddVertex(int dataPtr)
        {
            this.vertices.Add(new GraphVertex()
            {
                dataPtr = dataPtr,
                vertexPointers = new UnsafeList<int>(1, this.allocator),
            });
        }

        public unsafe void AddEdge(int vertexIdx, int connectedVertexIdx)
        {
            //Copying lists is expensive -> pointers are better
            var vertex = this.vertices[vertexIdx];
            var list = &vertex.vertexPointers;
            list->Add(connectedVertexIdx);
            this.vertices[vertexIdx] = vertex;
        }

        public void Clear()
        {
            for (int i = 0; i < this.vertices.Length; i++)
            {
                if (this.vertices[i].vertexPointers.IsCreated)
                {
                    var vertex = this.vertices[i];
                    var list = vertex.vertexPointers;
                    list.m_length = 0;
                    vertex.vertexPointers = list;
                    this.vertices[i] = vertex;
                }
            }
            this.vertices.Clear();
        }

        public void Dispose()
        {
            for (int i = 0; i < this.vertices.Length; i++)
            {
                if (this.vertices[i].vertexPointers.IsCreated)
                {
                    this.vertices[i].vertexPointers.Dispose();
                }
            }
            this.vertices.DisposeIfCreated();
        }


    }
}
