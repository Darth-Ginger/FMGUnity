using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
#if COLLECTIONS_2_OR_NEWER
using Unity.Collections.NotBurstCompatible;
#endif

namespace GimmeDOTSGeometry
{
    public static class MeshSlicing
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh">Mesh to slice - note that only the part that is cut is not allowed to be disconnected or self-intersecting</param>
        /// <param name="plane">Determines the cut - if the plane is not intersecting the mesh, null is returned</param>
        /// <param name="epsilon"></param>
        /// <param name="dependsOn"></param>
        /// <returns>Two meshes, one for each side of the cutting plane. Each submesh, except the first, corresponds to one created surface polygon during slicing</returns>
        public static Mesh[] Slice(Mesh mesh, Plane plane, float epsilon = 10e-7f, JobHandle dependsOn = default)
        {
            //1.) Create Edge Neighbor Map
            //2.) Find Loops
            //3.) Create Polygons by projection
            //4.) Add Holes to outer polygons
            //5.) Create Shells
            //6.) Triangulate
            //7.) Create Meshes from Triangulation
            //8.) (Create UVS)

            Mesh[] meshes = null;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var uv = mesh.uv;

            NativeArray<Vector3> verticesArray = new NativeArray<Vector3>(vertices, Allocator.TempJob);
            NativeArray<int> trianglesArray = new NativeArray<int>(triangles, Allocator.TempJob);
            NativeArray<Vector2> uvsArray = new NativeArray<Vector2>(uv, Allocator.TempJob);

            NativeParallelHashMap<int, MeshSlicingJobs.EdgeNeighborsEntry> edgeNeighborMap 
                = new NativeParallelHashMap<int, MeshSlicingJobs.EdgeNeighborsEntry>(triangles.Length, Allocator.TempJob);
            NativeList<UnsafeList<MeshSlicingJobs.LoopEntry>> loops = new NativeList<UnsafeList<MeshSlicingJobs.LoopEntry>>(1, Allocator.TempJob);
            NativeList<NativePolygon2D> polygons = new NativeList<NativePolygon2D>(1, Allocator.TempJob);

            NativeReference<float3> xAxisRef = new NativeReference<float3>(Allocator.TempJob);
            NativeReference<float3> yAxisRef = new NativeReference<float3>(Allocator.TempJob);
            NativeReference<Rect> uvBounds = new NativeReference<Rect>(Allocator.TempJob);

            NativeList<Vector3> shellAVertices = new NativeList<Vector3>(1, Allocator.TempJob);
            NativeList<Vector3> shellBVertices = new NativeList<Vector3>(1, Allocator.TempJob);

            NativeList<int> shellATriangles = new NativeList<int>(1, Allocator.TempJob);
            NativeList<int> shellBTriangles = new NativeList<int>(1, Allocator.TempJob);

            NativeList<Vector2> shellAUVs = new NativeList<Vector2>(1, Allocator.TempJob);
            NativeList<Vector2> shellBUVs = new NativeList<Vector2>(1, Allocator.TempJob);

            //1.)
            var createEdgeNeighborJob = new MeshSlicingJobs.CreateEdgeNeighborMapJob()
            {
                neighborMap = edgeNeighborMap,
                triangles = trianglesArray,
                vertices = verticesArray
            };
            var createEdgeNeighborHandle = createEdgeNeighborJob.Schedule(dependsOn);

            //2.)
            var findLoopsJob = new MeshSlicingJobs.FindLoopsJob()
            {
                triangles = trianglesArray,
                vertices = verticesArray,
                plane = plane,
                epsilon = epsilon,
                neighborMap = edgeNeighborMap,
                loops = loops,
                allocator = Allocator.TempJob,
               
            };
            var loopHandle = findLoopsJob.Schedule(createEdgeNeighborHandle);
            
            //3.)
            var createPolygonsJob = new MeshSlicingJobs.CreatePolygonsFromLoopsJob()
            {
                plane = plane,
                loops = loops,
                allocator = Allocator.TempJob,
                polygons = polygons,
                xAxisRef = xAxisRef,
                yAxisRef = yAxisRef,
                uvBounds = uvBounds
            };
            var polygonHandle = createPolygonsJob.Schedule(loopHandle);

            
            //4.)
            var combinePolygonsJob = new MeshSlicingJobs.CombinePolygonsJob()
            {
                plane = plane,
                polygons = polygons,
            };
            combinePolygonsJob.Schedule(polygonHandle).Complete();

            
            if (polygons.Length > 0) {

                meshes = new Mesh[2];

                //5.)
                var shellJob = new MeshSlicingJobs.CreateShellsJob()
                {
                    shellATriangles = shellATriangles,
                    shellAVertices = shellAVertices,
                    plane = plane,
                    shellBTriangles = shellBTriangles,
                    shellBVertices = shellBVertices,
                    triangles = trianglesArray,
                    vertices = verticesArray,

                    shellAUVs = shellAUVs,
                    epsilon = epsilon,
                    shellBUVs = shellBUVs,
                    uvs = uvsArray,
                };
                shellJob.Schedule().Complete();

                CombineInstance[] shellAInstances = new CombineInstance[polygons.Length + 1];
                CombineInstance[] shellBInstances = new CombineInstance[polygons.Length + 1];

                //6.)

                var uvBoundsRect = uvBounds.Value;
                for (int i = 0; i < polygons.Length; i++)
                {
                    var poly = polygons[i];

                    var triangulation = new NativeList<int>(poly.points.Length, Allocator.TempJob);
                    var triangulationJob = Polygon2DTriangulation.YMonotoneTriangulationJob(poly, ref triangulation, epsilon: epsilon);
                    triangulationJob.Complete();

                    var polyMeshA = new Mesh();
                    var polyMeshB = new Mesh();

                    Vector3[] polyVertices = new Vector3[poly.points.Length];
                    Vector2[] polyUVs = new Vector2[poly.points.Length];
                    for(int j = 0; j < polyVertices.Length; j++)
                    {
                        var polyVert = poly.points[j];
                        polyVertices[j] = xAxisRef.Value * polyVert.x + yAxisRef.Value * polyVert.y + (float3)plane.normal * plane.distance;

                        float uvX = Mathf.InverseLerp(uvBoundsRect.xMin, uvBoundsRect.xMax, polyVert.x);
                        float uvY = Mathf.InverseLerp(uvBoundsRect.yMin, uvBoundsRect.yMax, polyVert.y);

                        polyUVs[j] = new Vector2(uvX, uvY);
                    }

#if COLLECTIONS_2_OR_NEWER
                    var tris = triangulation.ToArrayNBC();
#else
                    var tris = triangulation.ToArray();
#endif

                    polyMeshA.SetVertices(polyVertices);
                    polyMeshA.SetTriangles(tris, 0);
                    polyMeshA.SetUVs(0, polyUVs);

                    polyMeshB.SetVertices(polyVertices);
                    polyMeshB.SetTriangles(tris.Reverse().ToArray(), 0);
                    polyMeshB.SetUVs(0, polyUVs);

                    shellAInstances[i + 1] = new CombineInstance()
                    {
                        mesh = polyMeshB,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    };

                    shellBInstances[i + 1] = new CombineInstance()
                    {
                        mesh = polyMeshA,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    };

                    triangulation.Dispose();
                }

                Mesh shellA = new Mesh();
                Mesh shellB = new Mesh();



                shellA.SetVertices(shellAVertices.AsArray());
#if COLLECTIONS_2_OR_NEWER
                shellA.SetTriangles(shellATriangles.ToArrayNBC(), 0);
                shellA.SetUVs(0, shellAUVs.ToArrayNBC());
#else
                shellA.SetTriangles(shellATriangles.ToArray(), 0);
                shellA.SetUVs(0, shellAUVs.ToArray());
#endif

                shellB.SetVertices(shellBVertices.AsArray());
#if COLLECTIONS_2_OR_NEWER
                shellB.SetTriangles(shellBTriangles.ToArrayNBC(), 0);
                shellB.SetUVs(0, shellBUVs.ToArrayNBC());
#else
                shellB.SetTriangles(shellBTriangles.ToArray(), 0);
                shellB.SetUVs(0, shellBUVs.ToArray());
#endif

                shellAInstances[0] = new CombineInstance()
                {
                    mesh = shellA,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity,
                };

                shellBInstances[0] = new CombineInstance()
                {
                    mesh = shellB,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity,
                };

                meshes[0] = new Mesh();
                meshes[1] = new Mesh();

                meshes[0].CombineMeshes(shellAInstances, false, false);
                meshes[1].CombineMeshes(shellBInstances, false, false);

                meshes[0].RecalculateBounds();
                meshes[0].RecalculateNormals();

                meshes[1].RecalculateBounds();
                meshes[1].RecalculateNormals();

                for(int i = 0; i < shellAInstances.Length; i++)
                {
                    GameObject.Destroy(shellAInstances[i].mesh);
                }
                for(int i = 0; i < shellBInstances.Length; i++)
                {
                    GameObject.Destroy(shellBInstances[i].mesh);
                }
            }

            verticesArray.Dispose();
            trianglesArray.Dispose();
            uvsArray.Dispose();

            edgeNeighborMap.Dispose();
            var polyDisposeJob = new MeshSlicingJobs.DisposePolygonsJob()
            {
                polygons = polygons,
            };
            polyDisposeJob.Schedule().Complete();
            polygons.Dispose();

            xAxisRef.Dispose();
            yAxisRef.Dispose();

            shellAVertices.Dispose();
            shellBVertices.Dispose();

            shellATriangles.Dispose();
            shellBTriangles.Dispose();

            shellAUVs.Dispose();
            shellBUVs.Dispose();
            uvBounds.Dispose();
            loops.Dispose();

            return meshes;
        }


    }
}
