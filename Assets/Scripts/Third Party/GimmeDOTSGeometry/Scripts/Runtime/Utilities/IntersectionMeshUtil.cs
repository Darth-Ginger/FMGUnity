using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// A utility for creating meshes from intersections between shapes
    /// </summary>
    public static class IntersectionMeshUtil 
    {

        /// <summary>
        /// Creates a mesh that is the intersection between a plane and a box / cuboid. This mesh takes on the form
        /// of a convex polygon in 3D Space. If the plane does not intersect the box, null is returned
        /// </summary>
        /// <param name="plane"></param>
        /// <param name="bounds"></param>
        public static Mesh PlaneCuboidIntersectionMesh(Plane plane, Bounds bounds)
        {
            var intersections = ShapeIntersection.PlaneCuboidIntersections(plane, bounds);

            if(intersections.Length > 2)
            {
                var mesh = new Mesh();
                var vertices = new Vector3[intersections.Length];
                var triangles = new int[(intersections.Length - 2) * 3];

                if (intersections.Length == 3)
                {
                    vertices[0] = intersections[0];
                    vertices[1] = intersections[1];
                    vertices[2] = intersections[2];

                    triangles[0] = 0;
                    triangles[1] = 1;
                    triangles[2] = 2;

                    float3 normal = math.cross(vertices[1] - vertices[0], vertices[2] - vertices[0]);
                    if(math.dot(normal, plane.normal) < 0)
                    {
                        triangles[0] = 2;
                        triangles[2] = 0;
                    }
                }
                else if (intersections.Length > 3)
                {
                    //This shows the power of this package:
                    //  - A plane can intersect a cube by up to six points
                    //  - First we transform the coordinates to 2D
                    //  - The points are unordered -> We build the convex hull to get a correct ordering
                    //  - Then we build a triangulation
                    //  - We use that triangulation to build a mesh
                    NativeArray<float2> planePoints = new NativeArray<float2>(intersections.Length, Allocator.TempJob);
                    var intersectionPolygon = new NativePolygon2D(Allocator.TempJob, intersections.Length);

                    var i0 = intersections[0];

                    float3 xAxis = math.normalize((i0 - (float3)(plane.normal * plane.distance)));
                    float3 yAxis = math.normalize(math.cross(xAxis, (float3)plane.normal));

                    Dictionary<float2, int> planePointToIndex = new Dictionary<float2, int>();

                    for (int i = 0; i < intersections.Length; i++)
                    {
                        vertices[i] = intersections[i];

                        float x = VectorUtil.ScalarProjection(intersections[i], xAxis);
                        float y = VectorUtil.ScalarProjection(intersections[i], yAxis);

                        planePoints[i] = new float2(x, y);
                        planePointToIndex.Add(planePoints[i], i);
                    }

                    var convexHullJob = HullAlgorithms.CreateConvexHull(planePoints, ref intersectionPolygon);
                    convexHullJob.Complete();

                    var triangulation = Polygon2DTriangulation.FanTriangulation(intersectionPolygon);

                    for (int i = 0; i < triangulation.Count; i++)
                    {
                        int idx = triangulation[i];
                        float2 point = intersectionPolygon.points[idx];

                        int originalIdx = planePointToIndex[point];

                        triangles[i] = originalIdx;
                    }

                    planePoints.Dispose();
                    intersectionPolygon.Dispose();
                }

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();

                return mesh;
            }
            return null;
        }

    }
}
