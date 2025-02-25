using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class MeshUtil
    {

        /// <summary>
        /// Creates a mesh from a given polygon (2D) and a triangulation (calculated by some algorithm)
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="triangulation"></param>
        /// <returns></returns>
        public static Mesh CreatePolygonMesh(NativePolygon2D polygon, List<int> triangulation, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var points = polygon.points;

            var polyMesh = new Mesh();

            var axisIndices = cardinalPlane.GetAxisIndices();

            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < points.Length; i++)
            {
                var point = points.ElementAt(i);
                float3 position = float3.zero;
                position[axisIndices.x] = point.x;
                position[axisIndices.y] = point.y;
                vertices.Add(position);
            }

            polyMesh.SetVertices(vertices);
            polyMesh.SetTriangles(triangulation, 0);

            polyMesh.RecalculateBounds();
            polyMesh.RecalculateNormals();

            return polyMesh;
        }

        /// <summary>
        /// Creates a 3D prism form a base polygon and an offset
        /// </summary>
        /// <param name="basePolygon">The base polygon</param>
        /// <param name="triangulation"></param>
        /// <param name="offset">The offset where the other polygon is places</param>
        /// <param name="smoothMantle">If the mantle between the polygons should have smooth or non-smooth edges</param>
        /// <param name="cardinalPlane"></param>
        /// <returns></returns>
        public static Mesh CreatePrism(NativePolygon2D basePolygon, NativeArray<int> triangulation, Vector3 offset, 
            bool smoothMantle = false, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var mesh = new Mesh();
            var baseA = CreatePolygonMesh(basePolygon, triangulation, cardinalPlane);
            var baseB = Mesh.Instantiate(baseA);

            var bTriangles = baseB.triangles;
            var aVertices = baseA.vertices;
            for(int i = 0; i < bTriangles.Length; i += 3)
            {
                AlgoUtil.Swap(ref bTriangles[i], ref bTriangles[i + 2]);
            }
            for(int i = 0; i < aVertices.Length; i++)
            {
                aVertices[i] += offset;
            }

            baseB.SetTriangles(bTriangles, 0);
            baseA.SetVertices(aVertices);

            var mantleMesh = new Mesh();
            int polygonLength = baseA.vertices.Length;

            if (smoothMantle)
            {
                var mantleVertices = new Vector3[basePolygon.points.Length * 2];
                var mantleTriangles = new int[polygonLength * 2 * 3];

                for(int i = 0; i < polygonLength; i++)
                {
                    mantleVertices[i] = baseA.vertices[i];
                }
                for(int i = polygonLength; i < polygonLength * 2; i++)
                {
                    mantleVertices[i] = baseB.vertices[i - polygonLength];
                }

                for(int i = 0; i < polygonLength; i++)
                {
                    mantleTriangles[i * 6 + 0] = i;
                    mantleTriangles[i * 6 + 1] = i + polygonLength;
                    mantleTriangles[i * 6 + 2] = (i + 1) % polygonLength;

                    mantleTriangles[i * 6 + 3] = ((i + 1) % polygonLength) + polygonLength;
                    mantleTriangles[i * 6 + 4] = (i + 1) % polygonLength;
                    mantleTriangles[i * 6 + 5] = i + polygonLength;
                }

                mantleMesh.SetVertices(mantleVertices);
                mantleMesh.SetTriangles(mantleTriangles, 0);

            } else
            {
                var mantleVertices = new Vector3[basePolygon.points.Length * 4];
                var mantleTriangles = new int[polygonLength * 2 * 3];

                for (int i = 0; i < polygonLength; i++)
                {
                    mantleVertices[i * 2 + 0] = baseA.vertices[i];
                    mantleVertices[i * 2 + 1] = baseA.vertices[i];
                }
                for (int i = polygonLength; i < polygonLength * 2; i++)
                {
                    mantleVertices[i * 2 + 0] = baseB.vertices[i - polygonLength];
                    mantleVertices[i * 2 + 1] = baseB.vertices[i - polygonLength];
                }

                for (int i = 0; i < polygonLength; i++)
                {
                    mantleTriangles[i * 6 + 0] = (i * 2 + 3) % (polygonLength * 2);
                    mantleTriangles[i * 6 + 1] = i * 2 + polygonLength * 2;
                    mantleTriangles[i * 6 + 2] = i * 2;

                    mantleTriangles[i * 6 + 3] = i * 2 + polygonLength * 2;
                    mantleTriangles[i * 6 + 4] = (i * 2 + 3) % (polygonLength * 2);
                    mantleTriangles[i * 6 + 5] = ((i * 2 + 3) % (polygonLength * 2)) + polygonLength * 2;
                }

                mantleMesh.SetVertices(mantleVertices);
                mantleMesh.SetTriangles(mantleTriangles, 0);
            }


            var combineInstances = new CombineInstance[3];

            var baseAInstance = new CombineInstance();
            baseAInstance.mesh = baseA;

            var baseBInstance = new CombineInstance();
            baseBInstance.mesh = baseB;
            
            var mantleInstance = new CombineInstance();
            mantleInstance.mesh = mantleMesh;


            combineInstances[0] = baseAInstance;
            combineInstances[1] = baseBInstance;
            combineInstances[2] = mantleInstance;

            mesh.CombineMeshes(combineInstances, true, false, false);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;

        }

        /// <summary>
        /// Creates a mesh from a given polygon (2D) and a triangulation (calculated by some algorithm)
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="triangulation"></param>
        /// <returns></returns>
        public static Mesh CreatePolygonMesh(NativePolygon2D polygon, NativeArray<int> triangulation, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {

            return CreatePolygonMesh(polygon, triangulation.ToList(), cardinalPlane);
        }

        //Works for many polygons but not all - for a better version I will need a straight skeleton algorithm first
        public static Mesh CreatePolygonOutline(NativePolygon2D polygon, float thickness, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var mesh = new Mesh();

            var points = polygon.points;
            var vertices = new Vector3[polygon.points.Length * 2];
            var triangles = new int[vertices.Length * 2 * 3];

            var separators = polygon.separators;
            int subsurface = 0;
            int prevSubsurfaceIdx = 0;
            int nextSubsurfaceIdx = points.Length;
            if(separators.Length > 0)
            {
                nextSubsurfaceIdx = separators.ElementAt(subsurface);
            }


            for (int i = 0; i < points.Length; i++)
            {
                int idx = i - prevSubsurfaceIdx;
                int length = nextSubsurfaceIdx - prevSubsurfaceIdx;

                int prevI = i - 1;
                int nextI = i + 1;
                if ((idx + 1) % length != (idx + 1))
                {
                    nextI = prevSubsurfaceIdx;
                }
                if((idx - 1) < prevSubsurfaceIdx)
                {
                    prevI = nextSubsurfaceIdx - 1;
                }

                var a = points.ElementAt(prevSubsurfaceIdx + idx);
                var b = points.ElementAt(prevSubsurfaceIdx + ((idx + 1) % length));
                var c = points.ElementAt(prevI);

                var dirFwd = b - a;
                var dirBack = c - a;
                var bisectorDir = math.normalize((math.normalize(dirFwd) + math.normalize(dirBack)));
                var innerPoint = a + bisectorDir * thickness;
                if(!polygon.IsPointInside(innerPoint))
                {
                    innerPoint = a - bisectorDir * thickness;
                }

                vertices[i * 2 + 0] = a.AsFloat3(cardinalPlane);
                vertices[i * 2 + 1] = innerPoint.AsFloat3(cardinalPlane);

                triangles[i * 6 + 0] = i * 2 + 0;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = nextI * 2 + 1;

                triangles[i * 6 + 3] = nextI * 2 + 1;
                triangles[i * 6 + 4] = nextI * 2 + 0;
                triangles[i * 6 + 5] = i * 2 + 0;

                if (i >= nextSubsurfaceIdx - 1)
                {
                    prevSubsurfaceIdx = nextSubsurfaceIdx;
                    subsurface++;
                    if (separators.Length > subsurface)
                    {
                        nextSubsurfaceIdx = separators.ElementAt(subsurface);
                    }
                    else
                    {
                        nextSubsurfaceIdx = points.Length;
                    }
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }


        /// <summary>
        /// Creates a line mesh (tube) defined by a 3D segment
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="thickness"></param>
        /// <param name="circlePoints">Number of vertices around the tube</param>
        /// <returns></returns>
        public static Mesh CreateLine(LineSegment3D segment, float thickness, int circlePoints = 8)
        {
            var dir = segment.b - segment.a;

            var mesh = new Mesh();

            var vertices = new Vector3[2 * circlePoints];
            var triangles = new int[6 * circlePoints];

            float angle = 0.0f;
            float angleIncrease = Constants.TAU / (float)circlePoints;

            for (int i = 0; i < circlePoints; i++)
            {
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                vertices[i * 2] = segment.a + Quaternion.LookRotation(dir) * new Vector3(cos, sin, 0.0f) * thickness;
                vertices[i * 2 + 1] = segment.b + Quaternion.LookRotation(dir) * new Vector3(cos, sin, 0.0f) * thickness;

                angle += angleIncrease;
            }

            for (int i = 0; i < circlePoints; i++)
            {
                int iPlusOne = (i + 1) % circlePoints;

                triangles[i * 6 + 0] = iPlusOne * 2;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = i * 2;
                triangles[i * 6 + 3] = iPlusOne * 2 + 1;
                triangles[i * 6 + 4] = i * 2 + 1;
                triangles[i * 6 + 5] = iPlusOne * 2;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateArrow3D(LineSegment3D segment, float thickness, float arrowHeadLength, float arrowHeadWidth, int circlePoints = 8)
        {
            var dir = segment.b - segment.a;

            float length = dir.magnitude;

            if (length <= arrowHeadLength)
            {
                return CreateLine(segment, thickness, circlePoints);
            } else
            {
                var mesh = new Mesh();

                var vertices = new Vector3[6 * circlePoints + 1];
                var triangles = new int[15 * circlePoints + (circlePoints - 2) * 3];

                float angle = 0.0f;
                float angleIncrease = Constants.TAU / (float)circlePoints;
                    
                var lookRotation = Quaternion.LookRotation(dir);

                for (int i = 0; i < circlePoints; i++)
                {
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);

                    vertices[i * 6] = segment.a + lookRotation * new Vector3(cos, sin, 0.0f) * thickness;
                    vertices[i * 6 + 1] = vertices[i * 6];
                    vertices[i * 6 + 2] = segment.b - dir * arrowHeadLength + lookRotation * new Vector3(cos, sin, 0.0f) * thickness;
                    vertices[i * 6 + 3] = vertices[i * 6 + 2];
                    vertices[i * 6 + 4] = segment.b - dir * arrowHeadLength + lookRotation * new Vector3(cos, sin, 0.0f) * (thickness + arrowHeadWidth);
                    vertices[i * 6 + 5] = vertices[i * 6 + 4];

                    angle += angleIncrease;
                }
                vertices[vertices.Length - 1] = segment.b;

                for (int i = 0; i < circlePoints; i++)
                {
                    int iPlusOne = (i + 1) % circlePoints;

                    triangles[i * 15 + 0] = iPlusOne * 6 + 1;
                    triangles[i * 15 + 1] = i * 6 + 2;
                    triangles[i * 15 + 2] = i * 6 + 1;
                    triangles[i * 15 + 3] = iPlusOne * 6 + 2;
                    triangles[i * 15 + 4] = i * 6 + 2;
                    triangles[i * 15 + 5] = iPlusOne * 6 + 1;

                    triangles[i * 15 + 6] = i * 6 + 4;
                    triangles[i * 15 + 7] = i * 6 + 3;
                    triangles[i * 15 + 8] = iPlusOne * 6 + 3;
                    triangles[i * 15 + 9] = iPlusOne * 6 + 3;
                    triangles[i * 15 + 10] = iPlusOne * 6 + 4;
                    triangles[i * 15 + 11] = i * 6 + 4;

                    triangles[i * 15 + 12] = i * 6 + 5;
                    triangles[i * 15 + 13] = iPlusOne * 6 + 5;
                    triangles[i * 15 + 14] = vertices.Length - 1;
                }

                int startTriangleIdx = circlePoints * 15;
                for (int i = 0; i < circlePoints - 2; i++)
                {
                    int next = i + 1;
                    int nextNext = i + 2;
                    nextNext = nextNext % circlePoints;

                    triangles[startTriangleIdx + i * 3 + 0] = nextNext * 6;
                    triangles[startTriangleIdx + i * 3 + 1] = next * 6;
                    triangles[startTriangleIdx + i * 3 + 2] = 0;

                }

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);


                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                return mesh;
            }
        }

        /// <summary>
        /// Creates a line mesh (rectangle) defined by a 2D segment
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
        public static Mesh CreateLine(LineSegment2D segment, float thickness, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var mesh = new Mesh();
            var indices = cardinalPlane.GetAxisIndices();

            var dir = segment.b - segment.a;
            var perpendicular = Vector3.zero;
            perpendicular[indices.x] = dir.y;
            perpendicular[indices.y] = -dir.x;
            perpendicular = math.normalize(perpendicular);

            var segA = segment.a.AsVector3(cardinalPlane);
            var segB = segment.b.AsVector3(cardinalPlane);

            var vertices = new Vector3[4];
            vertices[0] = segA + perpendicular * thickness * 0.5f;
            vertices[1] = segB + perpendicular * thickness * 0.5f;
            vertices[2] = segB - perpendicular * thickness * 0.5f;
            vertices[3] = segA - perpendicular * thickness * 0.5f;

            var triangles = new int[6] { 2, 1, 0, 3, 2, 0 };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateLineStrip(float2[] points, float thickness, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var mesh = new Mesh();
            var indices = cardinalPlane.GetAxisIndices();

            var vertices = new Vector3[points.Length * 2];
            var triangles = new int[(points.Length - 1) * 6];

            for (int i = 0; i < points.Length - 1; i++)
            {
                float2 current = points[i];
                float2 next = points[i + 1];
                var dir = next - current;
                var perpendicular = Vector3.zero;
                perpendicular[indices.x] = dir.y;
                perpendicular[indices.y] = -dir.x;
                perpendicular = math.normalize(perpendicular);

                var segA = ((Vector2)current).AsVector3(cardinalPlane);

                vertices[i * 2 + 0] = segA + perpendicular * thickness * 0.5f;
                vertices[i * 2 + 1] = segA - perpendicular * thickness * 0.5f;

                if (i == points.Length - 2)
                {
                    var segB = ((Vector2)next).AsVector3(cardinalPlane);

                    vertices[i * 2 + 2] = segB + perpendicular * thickness * 0.5f;
                    vertices[i * 2 + 3] = segB - perpendicular * thickness * 0.5f;
                }

                triangles[i * 6 + 0] = i * 2 + 0;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = i * 2 + 3;
                triangles[i * 6 + 3] = i * 2 + 3;
                triangles[i * 6 + 4] = i * 2 + 2;
                triangles[i * 6 + 5] = i * 2 + 0;

            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateArrow2D(LineSegment2D segment, float thickness, float arrowHeadLength, float arrowHeadWidth, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var dir = segment.b - segment.a;

            float length = dir.magnitude;

            if (length <= arrowHeadLength)
            {
                return CreateLine(segment, thickness, cardinalPlane);
            }
            else
            {
                var mesh = new Mesh();
                var indices = cardinalPlane.GetAxisIndices();

                var vertices = new Vector3[7];

                var dir3D = dir.AsVector3(cardinalPlane).normalized;
                var perpendicular = Vector3.zero;
                perpendicular[indices.x] = dir.y;
                perpendicular[indices.y] = -dir.x;
                perpendicular = math.normalize(perpendicular);

                var segA = segment.a.AsVector3(cardinalPlane);
                var segB = segment.b.AsVector3(cardinalPlane);

                vertices[0] = segA + perpendicular * thickness * 0.5f;
                vertices[1] = segB + perpendicular * thickness * 0.5f - arrowHeadLength * dir3D;
                vertices[2] = segB - perpendicular * thickness * 0.5f - arrowHeadLength * dir3D;
                vertices[3] = segA - perpendicular * thickness * 0.5f;

                vertices[4] = vertices[1] + perpendicular * arrowHeadWidth * 0.5f;
                vertices[5] = vertices[2] - perpendicular * arrowHeadWidth * 0.5f;
                vertices[6] = segA + length * dir3D;

                var triangles = new int[15]
                {
                    2, 1, 0,
                    3, 2, 0,
                    4, 1, 6,
                    2, 5, 6,
                    2, 6, 1
                };

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);

                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                return mesh;
            }
        }




        /// <summary>
        /// Creates a box mesh (cuboid)
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="smooth">If true, the corner vertices are shared</param>
        /// <returns></returns>
        public static Mesh CreateBox(Bounds bounds, bool smooth = false)
        {
            var mesh = new Mesh();

            var min = bounds.min;
            var max = bounds.max;

            Vector3[] vertices;
            int[] triangles;
            if (smooth)
            {
                vertices = new Vector3[8];
            }
            else
            {
                vertices = new Vector3[24];
            }

            vertices[0] = min;
            vertices[1] = new Vector3(min.x, min.y, max.z);
            vertices[2] = new Vector3(max.x, min.y, max.z);
            vertices[3] = new Vector3(max.x, min.y, min.z);

            vertices[4] = new Vector3(min.x, max.y, min.z);
            vertices[5] = new Vector3(min.x, max.y, max.z);
            vertices[6] = max;
            vertices[7] = new Vector3(max.x, max.y, min.z);


            if (smooth)
            {
                triangles = new int[36]
                {
                    2, 1, 0,    0, 3, 2,
                    4, 5, 6,    6, 7, 4,
                    1, 5, 0,    5, 4, 0,
                    7, 6, 3,    6, 2, 3,
                    7, 3, 0,    4, 7, 0,
                    5, 1, 2,    6, 5, 2,
                };

            } else {

                for (int i = 0; i < 8; i++)
                {
                    vertices[8 + i] = vertices[i];
                    vertices[16 + i] = vertices[i];
                }

                triangles = new int[36]
                {
                    2, 1, 0,    0, 3, 2,
                    4, 5, 6,    6, 7, 4,
                    9, 13, 8,    13, 12, 8,
                    15, 14, 11,    14, 10, 11,
                    23, 19, 16,    20, 23, 16,
                    21, 17, 18,    22, 21, 18,
                };
            }
        

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a cube / box given with inverted faces
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Mesh CreateInvertedBox(Bounds bounds)
        {
            var mesh = new Mesh();

            var min = bounds.min;
            var max = bounds.max;

            var vertices = new Vector3[8];

            vertices[0] = min;
            vertices[1] = new Vector3(min.x, min.y, max.z);
            vertices[2] = new Vector3(max.x, min.y, max.z);
            vertices[3] = new Vector3(max.x, min.y, min.z);

            vertices[4] = new Vector3(min.x, max.y, min.z);
            vertices[5] = new Vector3(min.x, max.y, max.z);
            vertices[6] = max;
            vertices[7] = new Vector3(max.x, max.y, min.z);

            var triangles = new int[36]
            {
                0, 1, 2,    2, 3, 0,
                6, 5, 4,    4, 7, 6,
                0, 5, 1,    0, 4, 5,
                3, 6, 7,    3, 2, 6,
                0, 3, 7,    0, 7, 4,
                2, 1, 5,    2, 5, 6,

            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }



        /// <summary>
        /// Creates an outline of a box. Each side of the box will have a hollow rectangle as geometry
        /// (same as CreateRectangleOutline, but for each side of the box)
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
        public static Mesh CreateBoxOutline(Bounds bounds, float thickness)
        {
            var mesh = new Mesh();

            var size = bounds.size;

            var frontAndBack = CreateRectangleOutline(new Rect(new Vector2(-size.x * 0.5f, -size.y * 0.5f), new Vector2(size.x, size.y)), thickness, CardinalPlane.XY);
            var leftAndRight = CreateRectangleOutline(new Rect(new Vector2(-size.z * 0.5f, -size.y * 0.5f), new Vector2(size.z, size.y)), thickness, CardinalPlane.ZY);
            var topAndBottom = CreateRectangleOutline(new Rect(new Vector2(-size.x * 0.5f, -size.z * 0.5f), new Vector2(size.x, size.z)), thickness, CardinalPlane.XZ);

            var combineInstances = new CombineInstance[6];

            var frontBackInstance = new CombineInstance();
            frontBackInstance.mesh = frontAndBack;

            var leftRightInstance = new CombineInstance();
            leftRightInstance.mesh = leftAndRight;

            var topBottomInstance = new CombineInstance();
            topBottomInstance.mesh = topAndBottom;

            frontBackInstance.transform = Matrix4x4.TRS(new Vector3(0.0f, 0.0f, -size.z * 0.5f), Quaternion.identity, Vector3.one);
            combineInstances[0] = frontBackInstance;
            frontBackInstance.transform = Matrix4x4.TRS(new Vector3(0.0f, 0.0f, size.z * 0.5f), Quaternion.identity, -Vector3.one);
            combineInstances[1] = frontBackInstance;

            leftRightInstance.transform = Matrix4x4.TRS(new Vector3(-size.x * 0.5f, 0.0f, 0.0f), Quaternion.identity, Vector3.one);
            combineInstances[2] = leftRightInstance;
            leftRightInstance.transform = Matrix4x4.TRS(new Vector3(size.x * 0.5f, 0.0f, 0.0f), Quaternion.identity, -Vector3.one);
            combineInstances[3] = leftRightInstance;

            topBottomInstance.transform = Matrix4x4.TRS(new Vector3(0.0f, size.y * 0.5f, 0.0f), Quaternion.identity, Vector3.one);
            combineInstances[4] = topBottomInstance;
            topBottomInstance.transform = Matrix4x4.TRS(new Vector3(0.0f, -size.y * 0.5f, 0.0f), Quaternion.identity, -Vector3.one);
            combineInstances[5] = topBottomInstance;

            mesh.CombineMeshes(combineInstances, true, true, false);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a hollow rectangle mesh (A rectangle with a rectangle hole inside)
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="thickness">The thickness of the border</param>
        /// <returns></returns>
        public static Mesh CreateRectangleOutline(Rect rect, float thickness, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var mesh = new Mesh();

            var indices = cardinalPlane.GetAxisIndices();

            var min = rect.min;
            var max = rect.max;

            var vertices = new Vector3[8];

            var bottomLeft = Vector3.zero;
            var topLeft = Vector3.zero;
            var topRight = Vector3.zero;
            var bottomRight = Vector3.zero;

            bottomLeft[indices.x] = min.x;
            bottomLeft[indices.y] = min.y;

            topLeft[indices.x] = min.x;
            topLeft[indices.y] = max.y;

            topRight[indices.x] = max.x;
            topRight[indices.y] = max.y;

            bottomRight[indices.x] = max.x;
            bottomRight[indices.y] = min.y;

            vertices[0] = bottomLeft;
            vertices[1] = topLeft;
            vertices[2] = topRight;
            vertices[3] = bottomRight;

            bottomLeft[indices.x] += thickness;
            bottomLeft[indices.y] += thickness;

            topLeft[indices.x] += thickness;
            topLeft[indices.y] -= thickness;

            topRight[indices.x] -= thickness;
            topRight[indices.y] -= thickness;

            bottomRight[indices.x] -= thickness;
            bottomRight[indices.y] += thickness;

            vertices[4] = bottomLeft;
            vertices[5] = topLeft;
            vertices[6] = topRight;
            vertices[7] = bottomRight;

            var triangles = new int[24]
            {
                0, 1, 4,
                1, 5, 4,
                1, 2, 6,
                1, 6, 5,
                2, 7, 6,
                2, 3, 7,
                3, 4, 7,
                3, 0, 4
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a centered plane with a number of cells (subdivided plane)
        /// </summary>
        /// <param name="size">The size of the plane</param>
        /// <param name="cellsX">Number of cells in the "X-Direction" (first direction in the cardinal plane)</param>
        /// <param name="cellsY">Number of cells in the "Y-Direction" (second direction in the cardinal plane)</param>
        /// <param name="cardinalPlane"></param>
        /// <returns></returns>
        public static Mesh CreatePlane(Vector2 size, int cellsX, int cellsY, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            if(cellsX < 1 ||cellsY < 1)
            {
                throw new ArgumentException("[Gimme DOTS Geometry]: Number of cells should be greater than 0");
            }

            var mesh = new Mesh();
            var indices = cardinalPlane.GetAxisIndices();

            var vertices = new Vector3[(cellsX + 1) * (cellsY + 1)];
            var triangles = new int[cellsX * cellsY * 2 * 3];

            float xOffset = size.x * 0.5f;
            float yOffset = size.y * 0.5f;

            float cellSizeX = size.x / (float)cellsX;
            float cellSizeY = size.y / (float)cellsY;

            for(int x = 0; x <= cellsX; x++)
            {
                for(int y = 0; y <= cellsY; y++)
                {
                    int idx = y * (cellsX + 1) + x;

                    float posX = x * cellSizeX - xOffset;
                    float posY = y * cellSizeY - yOffset;

                    vertices[idx][indices.x] = posX;
                    vertices[idx][indices.y] = posY;
                }
            }

            for(int x = 0; x < cellsX; x++)
            {
                for(int y = 0; y < cellsY; y++)
                {
                    int idx = y * cellsX + x;

                    int vertexIdx = y * (cellsX + 1) + x;
                    int xPlusOne = y * (cellsX + 1) + (x + 1);
                    int yPlusOne = (y + 1) * (cellsX + 1) + x;
                    int xyPlusOne = (y + 1) * (cellsX + 1) + (x + 1);

                    triangles[idx * 6 + 0] = yPlusOne;
                    triangles[idx * 6 + 1] = xPlusOne;
                    triangles[idx * 6 + 2] = vertexIdx;

                    triangles[idx * 6 + 3] = xyPlusOne;
                    triangles[idx * 6 + 4] = xPlusOne;
                    triangles[idx * 6 + 5] = yPlusOne;
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a 3D Grid with a given number of cells in each direction of space
        /// </summary>
        /// <param name="cellSize"></param>
        /// <param name="cells"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
        public static Mesh CreateGrid3D(Vector3 cellSize, int3 cells, float thickness)
        {
            if(math.any(cells < 1))
            {
                throw new ArgumentException("[Gimme DOTS Geometry]: Number of grid cells should be greater than 0");
            }

            var mesh = new Mesh();

            int totalCells = cells.x * cells.y * cells.z;

            var boxOutlineMesh = CreateBoxOutline(new Bounds(Vector3.zero, cellSize), thickness);

            var combineInstanceArr = new CombineInstance[totalCells];

            float halfCellsX = (cells.x / 2.0f);
            float halfCellsY = (cells.y / 2.0f);
            float halfCellsZ = (cells.z / 2.0f);

            Vector3 currentPosition = Vector3.zero;
            currentPosition.z -= halfCellsZ * cellSize.z - cellSize.z * 0.5f;

            for (int z = 0; z < cells.z; z++)
            {
                currentPosition.y = -halfCellsY * cellSize.y + cellSize.y * 0.5f;
                for (int y = 0; y < cells.y; y++)
                {
                    currentPosition.x = -halfCellsX * cellSize.x + cellSize.x * 0.5f;
                    for (int x = 0; x < cells.x; x++)
                    {
                        int idx = z * cells.x * cells.y + y * cells.x + x;

                        var instance = new CombineInstance();
                        instance.mesh = boxOutlineMesh;
                        instance.transform = Matrix4x4.TRS(currentPosition, Quaternion.identity, Vector3.one * 0.995f);

                        combineInstanceArr[idx] = instance;

                        currentPosition.x += cellSize.x;
                    }

                    currentPosition.y += cellSize.y;
                }
                currentPosition.z += cellSize.z;
            }

            mesh.CombineMeshes(combineInstanceArr, true, true, false);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a 2D Grid with a given number of cells in each direction of a plane
        /// </summary>
        /// <param name="cellSize">The size of each cell</param>
        /// <param name="cellsX">The number of cells in the "X-Direction" (first direction in the cardinal plane)</param>
        /// <param name="cellsY">The number of cells in the "Y-Direction" (second direction in the cardinal plane)</param>
        /// <param name="thickness">Thickness of the border of each cell</param>
        /// <param name="cardinalPlane"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Mesh CreateGrid2D(Vector2 cellSize, int cellsX, int cellsY, float thickness, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            if(cellsX < 1 || cellsY < 1)
            {
                throw new ArgumentException("[Gimme DOTS Geometry]: Number of grid cells should be greater than 0");
            }

            var mesh = new Mesh();
            var indices = cardinalPlane.GetAxisIndices();

            int totalCells = cellsX * cellsY;

            var rectOutlineMesh = CreateRectangleOutline(new Rect(Vector2.zero, cellSize), thickness, cardinalPlane);

            var combineInstanceArr = new CombineInstance[totalCells];


            float halfCellsX = (cellsX / 2.0f);
            float halfCellsY = (cellsY / 2.0f);

            Vector3 currentPosition = Vector3.zero;
            currentPosition[indices.y] -= halfCellsY * cellSize.y;

            for (int y = 0; y < cellsY; y++)
            {
                currentPosition[indices.x] = -halfCellsX * cellSize.x;
                for(int x = 0; x < cellsX; x++)
                {
                    int idx = y * cellsX + x;

                    var instance = new CombineInstance();
                    instance.mesh = rectOutlineMesh;
                    instance.transform = Matrix4x4.TRS(currentPosition, Quaternion.identity, Vector3.one);

                    combineInstanceArr[idx] = instance;

                    currentPosition[indices.x] += cellSize.x;
                }

                currentPosition[indices.y] += cellSize.y;
            }

            mesh.CombineMeshes(combineInstanceArr, true, true, false);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a 2D triangle mesh. Clockwise is front
        /// </summary>
        /// <param name="triangle"></param>
        /// <returns></returns>
        public static Mesh CreateTriangle(NativeTriangle2D triangle, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[3];
            var triangles = new int[3] { 0, 1, 2 };

            var indices = cardinalPlane.GetAxisIndices();

            vertices[0][indices.x] = triangle.a.x;
            vertices[0][indices.y] = triangle.a.y;

            vertices[1][indices.x] = triangle.b.x;
            vertices[1][indices.y] = triangle.b.y;

            vertices[2][indices.x] = triangle.c.x;
            vertices[2][indices.y] = triangle.c.y;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a 3D triangle mesh. Clockwise is front
        /// </summary>
        /// <param name="triangle"></param>
        /// <returns></returns>
        public static Mesh CreateTriangle(NativeTriangle3D triangle)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[3];
            var triangles = new int[3] { 0, 1, 2 };

            vertices[0] = triangle.a;
            vertices[1] = triangle.b;
            vertices[2] = triangle.c;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a 3D triangle outline. Clockwise is front
        /// </summary>
        /// <param name="triangle"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
        public static Mesh CreateTriangleOutline(NativeTriangle3D triangle, float thickness)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[6];

            vertices[0] = triangle.a;
            vertices[1] = triangle.b;
            vertices[2] = triangle.c;

            var dirA = triangle.b - triangle.a;
            var dirB = triangle.c - triangle.a;
            var dirC = triangle.c - triangle.b;

            var bisectorA = math.normalize(math.normalize(dirA) + math.normalize(dirB));
            var bisectorB = math.normalize(math.normalize(-dirA) + math.normalize(dirC));
            var bisectorC = math.normalize(math.normalize(-dirB) + math.normalize(-dirC));

            vertices[3] = triangle.a + bisectorA * thickness;
            vertices[4] = triangle.b + bisectorB * thickness;
            vertices[5] = triangle.c + bisectorC * thickness;

            var triangles = new int[18]
            {
                1, 3, 0,    4, 3, 1,
                5, 4, 1,    2, 5, 1,
                3, 5, 2,    2, 0, 3,
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }


        /// <summary>
        /// Creates a rectangle mesh given by a rect in the given cardinal plane
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static Mesh CreateRectangle(Rect rect, CardinalPlane cardinalPlane = CardinalPlane.XZ,
            Vector2[] uvs = null, int uvChannel = 0)
        {
            var mesh = new Mesh();

            var min = rect.min;
            var max = rect.max;

            var vertices = new Vector3[4];
            var triangles = new int[6];

            float3 bottomLeft = float3.zero;    
            float3 topLeft = float3.zero;
            float3 topRight = float3.zero;
            float3 bottomRight = float3.zero;

            var axisIndices = cardinalPlane.GetAxisIndices();

            bottomLeft[axisIndices.x] = min.x;
            bottomLeft[axisIndices.y] = min.y;

            topLeft[axisIndices.x] = min.x;
            topLeft[axisIndices.y] = max.y;

            topRight[axisIndices.x] = max.x;
            topRight[axisIndices.y] = max.y;

            bottomRight[axisIndices.x] = max.x;
            bottomRight[axisIndices.y] = min.y;

            vertices[0] = bottomLeft;
            vertices[1] = topLeft;
            vertices[2] = topRight;
            vertices[3] = bottomRight;

            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;

            triangles[3] = 2;
            triangles[4] = 3;
            triangles[5] = 0;



            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs != null)
            {
                mesh.SetUVs(uvChannel, uvs);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();



            return mesh;
        }

        /// <summary>
        /// Creates a circle mesh with a radius in the given cardinal plane
        /// </summary>
        /// <param name="circlePoints"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static Mesh CreateCircle(float radius, int circlePoints = 32, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            if(circlePoints < 3)
            {
                Debug.LogError("Unable to create a circle with fewer than three points!");
            }

            var axisIndices = cardinalPlane.GetAxisIndices();

            var mesh = new Mesh();

            var vertices = new Vector3[circlePoints];
            var triangles = new int[(circlePoints - 2) * 3];

            float angle = 0.0f;
            float angleIncrease = Constants.TAU / (float)circlePoints;

            for(int i = 0; i < circlePoints; i++)
            {
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                vertices[i][axisIndices.x] = cos * radius;
                vertices[i][axisIndices.y] = sin * radius;

                //Fan Triangulation
                if(i >= 2)
                {
                    triangles[(i - 2) * 3] = i;
                    triangles[(i - 2) * 3 + 1] = i - 1;
                    triangles[(i - 2) * 3 + 2] = 0;
                }

                angle += angleIncrease;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Creates a ring mesh with a radius and thickness in the given cardinal plane
        /// </summary>
        /// <param name="circlePoints"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static Mesh CreateRing(float radius, float thickness, int circlePoints = 32, CardinalPlane cardinalPlane = CardinalPlane.XZ)
        {
            if (circlePoints < 3)
            {
                Debug.LogError("Unable to create a ring with fewer than three points!");
            }

            var axisIndices = cardinalPlane.GetAxisIndices();

            var mesh = new Mesh();

            var vertices = new Vector3[circlePoints * 2];
            var triangles = new int[circlePoints * 2 * 3];

            float angle = 0.0f;
            float angleIncrease = Constants.TAU / (float)circlePoints;

            for (int i = 0; i < circlePoints; i++)
            {
                int innerPointIdx = i;
                int outerPointIdx = i + circlePoints;

                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                Vector3 outerPos = Vector3.zero;
                Vector3 innerPos = Vector3.zero;

                outerPos[axisIndices.x] = cos * radius;
                outerPos[axisIndices.y] = sin * radius;

                innerPos[axisIndices.x] = cos * (radius - thickness);
                innerPos[axisIndices.y] = sin * (radius - thickness);

                vertices[innerPointIdx] = innerPos;
                vertices[outerPointIdx] = outerPos;

                angle += angleIncrease;

                int triangleStart = innerPointIdx * 6;

                int outerNext = outerPointIdx + 1;
                if (outerNext - circlePoints >= circlePoints) outerNext = circlePoints + (outerNext % circlePoints);

                triangles[triangleStart] = outerNext;
                triangles[triangleStart + 1] = outerPointIdx;
                triangles[triangleStart + 2] = innerPointIdx;
                triangles[triangleStart + 3] = innerPointIdx;
                triangles[triangleStart + 4] = (innerPointIdx + 1) % circlePoints;
                triangles[triangleStart + 5] = outerNext;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateTetrahedron(Tetrahedron tetrahedron)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[12];

            vertices[0] = tetrahedron.a;
            vertices[1] = tetrahedron.b;
            vertices[2] = tetrahedron.c;

            vertices[3] = tetrahedron.a;
            vertices[4] = tetrahedron.b;
            vertices[5] = tetrahedron.d;

            vertices[6] = tetrahedron.a;
            vertices[7] = tetrahedron.c;
            vertices[8] = tetrahedron.d;

            vertices[9] = tetrahedron.b;
            vertices[10] = tetrahedron.c;
            vertices[11] = tetrahedron.d;

            //We have to order the vertices first, so the faces point outwards
            var triangles = new int[12];

            //Centroid of Tetrahedron is always inside the Tetrahedron (you learn something new every day)
            var center = tetrahedron.GetCenter();

            var dirBA = tetrahedron.b - tetrahedron.a;
            var dirCA = tetrahedron.c - tetrahedron.a;
            var dirDA = tetrahedron.d - tetrahedron.a;
            var dirDB = tetrahedron.d - tetrahedron.b;

            var perpBC = Vector3.Cross(dirBA, dirCA);
            var perpBD = Vector3.Cross(dirBA, dirDA);
            var perpCD = Vector3.Cross(dirCA, dirDA);
            var perpAD = Vector3.Cross(-dirBA, dirDB);

            var dotBC = Vector3.Dot(perpBC, center - tetrahedron.a);
            var dotBD = Vector3.Dot(perpBD, center - tetrahedron.a);
            var dotCD = Vector3.Dot(perpCD, center - tetrahedron.a);
            var dotAD = Vector3.Dot(perpAD, center - tetrahedron.b);

            triangles[0] = dotBC >= 0 ? 2 : 0;
            triangles[1] = 1;
            triangles[2] = dotBC >= 0 ? 0 : 2;

            triangles[3] = dotBD >= 0 ? 5 : 3;
            triangles[4] = 4;
            triangles[5] = dotBD >= 0 ? 3 : 5;

            triangles[6] = dotCD >= 0 ? 8 : 6;
            triangles[7] = 7;
            triangles[8] = dotCD >= 0 ? 6 : 8;

            triangles[9] = dotAD >= 0 ? 9 : 11;
            triangles[10] = 10;
            triangles[11] = dotAD >= 0 ? 11 : 9;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateTetrahedronOutline(Tetrahedron tetrahedron, float thickness)
        {
            var mesh = new Mesh();

            var center = tetrahedron.GetCenter();

            var dirBA = tetrahedron.b - tetrahedron.a;
            var dirCA = tetrahedron.c - tetrahedron.a;
            var dirDA = tetrahedron.d - tetrahedron.a;
            var dirDB = tetrahedron.d - tetrahedron.b;

            var perpBC = Vector3.Cross(dirBA, dirCA);
            var perpBD = Vector3.Cross(dirBA, dirDA);
            var perpCD = Vector3.Cross(dirCA, dirDA);
            var perpAD = Vector3.Cross(-dirBA, dirDB);

            var dotBC = Vector3.Dot(perpBC, center - tetrahedron.a);
            var dotBD = Vector3.Dot(perpBD, center - tetrahedron.a);
            var dotCD = Vector3.Dot(perpCD, center - tetrahedron.a);
            var dotAD = Vector3.Dot(perpAD, center - tetrahedron.b);

            CombineInstance[] triangleMeshes = new CombineInstance[4];

            var triangleA = new NativeTriangle3D(dotBC >= 0 ? tetrahedron.c : tetrahedron.a, tetrahedron.b, dotBC >= 0 ? tetrahedron.a : tetrahedron.c);
            var triangleB = new NativeTriangle3D(dotBD >= 0 ? tetrahedron.d : tetrahedron.a, tetrahedron.b, dotBD >= 0 ? tetrahedron.a : tetrahedron.d);
            var triangleC = new NativeTriangle3D(dotCD >= 0 ? tetrahedron.d : tetrahedron.a, tetrahedron.c, dotCD >= 0 ? tetrahedron.a : tetrahedron.d);
            var triangleD = new NativeTriangle3D(dotAD >= 0 ? tetrahedron.b : tetrahedron.d, tetrahedron.c, dotAD >= 0 ? tetrahedron.d : tetrahedron.b);

            triangleMeshes[0].mesh = CreateTriangleOutline(triangleA, thickness);
            triangleMeshes[1].mesh = CreateTriangleOutline(triangleB, thickness);
            triangleMeshes[2].mesh = CreateTriangleOutline(triangleC, thickness);
            triangleMeshes[3].mesh = CreateTriangleOutline(triangleD, thickness);

            mesh.CombineMeshes(triangleMeshes, true, false, false);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateUVCapsule(float radius, float height, int uPoints = 24, int vPointsPerSide = 12)
        {
            Mesh mesh = new Mesh();

            int vertexCount = uPoints * (vPointsPerSide - 1) * 2 + 2;

            var vertices = new Vector3[vertexCount];
            List<int> triangles = new List<int>();

            float anglePerIndex = Constants.TAU / (float)uPoints;
            float heightPerCircle = (2.0f * radius) / (float)(vPointsPerSide * 2 - 2);

            float halfHeight = height * 0.5f;
            float currentHeight = -radius + heightPerCircle;

            var bottom = new Vector3(0.0f, -radius - halfHeight, 0.0f);
            var top = new Vector3(0.0f, radius + halfHeight, 0.0f);

            vertices[vertexCount - 2] = bottom;
            vertices[vertexCount - 1] = top;

            float offset = -halfHeight;

            for (int v = 1; v < vPointsPerSide; v++)
            {
                float currentAngle = 0.0f;
                for (int u = 0; u < uPoints; u++)
                {
                    int idx = (v - 1) * uPoints + u;

                    float flatPosX = Mathf.Sin(currentAngle) * radius;
                    float flatPosZ = Mathf.Cos(currentAngle) * radius;

                    float shrinkFactor = Mathf.Cos(Mathf.Asin(currentHeight / radius));
                    flatPosX *= shrinkFactor;
                    flatPosZ *= shrinkFactor;

                    vertices[idx] = new Vector3(flatPosX, currentHeight + offset, flatPosZ);

                    currentAngle += anglePerIndex;
                }
                currentHeight += heightPerCircle;
            }

            currentHeight -= heightPerCircle;
            offset = halfHeight;

            for(int v = vPointsPerSide; v < vPointsPerSide * 2 - 1; v++)
            {
                float currentAngle = 0.0f;
                for(int u = 0; u < uPoints; u++)
                {
                    int idx = (v - 1) * uPoints + u;

                    float flatPosX = Mathf.Sin(currentAngle) * radius;
                    float flatPosZ = Mathf.Cos(currentAngle) * radius;

                    float shrinkFactor = Mathf.Cos(Mathf.Asin(currentHeight / radius));
                    flatPosX *= shrinkFactor;
                    flatPosZ *= shrinkFactor;

                    vertices[idx] = new Vector3(flatPosX, currentHeight + offset, flatPosZ);

                    currentAngle += anglePerIndex;
                }
                currentHeight += heightPerCircle;
            }

            for (int v = 1; v < vPointsPerSide * 2 - 2; v++)
            {
                for (int u = 0; u < uPoints; u++)
                {
                    int idx = (v - 1) * uPoints + u;
                    int nextU = (v - 1) * uPoints + ((u + 1) % uPoints);
                    int nextV = v * uPoints + u;
                    int nextUV = v * uPoints + ((u + 1) % uPoints);

                    triangles.Add(nextU);
                    triangles.Add(nextV);
                    triangles.Add(idx);

                    triangles.Add(nextU);
                    triangles.Add(nextUV);
                    triangles.Add(nextV);
                }
            }

            for (int u = 0; u < uPoints; u++)
            {
                int bottomIdx = u;
                int topIdx = (vPointsPerSide * 2 - 3) * uPoints + u;

                int bottomNextIdx = (u + 1) % uPoints;
                int topNextIdx = (vPointsPerSide * 2 - 3) * uPoints + ((u + 1) % uPoints);

                triangles.Add(vertexCount - 2);
                triangles.Add(bottomNextIdx);
                triangles.Add(bottomIdx);

                triangles.Add(topIdx);
                triangles.Add(topNextIdx);
                triangles.Add(vertexCount - 1);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateUVSphere(float radius, int uPoints = 24, int vPoints = 24)
        {
            Mesh mesh = new Mesh();

            int vertexCount = uPoints * (vPoints - 2) + 2;

            var vertices = new Vector3[vertexCount];
            List<int> triangles = new List<int>();

            float anglePerIndex = Constants.TAU / (float)uPoints;
            float heightPerCircle = (2.0f * radius) / (float)(vPoints - 2);

            float currentHeight = -radius + heightPerCircle;

            var bottom = new Vector3(0.0f, -radius, 0.0f);
            var top = new Vector3(0.0f, radius, 0.0f);

            vertices[vertexCount - 2] = bottom;
            vertices[vertexCount - 1] = top;

            for(int v = 1; v < vPoints - 1; v++)
            {
                float currentAngle = 0.0f;
                for(int u = 0; u < uPoints; u++)
                {
                    int idx = (v - 1) * uPoints + u;

                    float flatPosX = Mathf.Sin(currentAngle) * radius;
                    float flatPosZ = Mathf.Cos(currentAngle) * radius;

                    float shrinkFactor = Mathf.Cos(Mathf.Asin(currentHeight / radius));
                    flatPosX *= shrinkFactor;
                    flatPosZ *= shrinkFactor;

                    vertices[idx] = new Vector3(flatPosX, currentHeight, flatPosZ);

                    currentAngle += anglePerIndex;
                }
                currentHeight += heightPerCircle;
            }

            for(int v = 1; v < vPoints - 2; v++)
            {
                for(int u = 0; u < uPoints; u++)
                {
                    int idx = (v - 1) * uPoints + u;
                    int nextU = (v - 1) * uPoints + ((u + 1) % uPoints);
                    int nextV = v * uPoints + u;
                    int nextUV = v * uPoints + ((u + 1) % uPoints);


                    triangles.Add(nextU);
                    triangles.Add(nextV);
                    triangles.Add(idx);


                    triangles.Add(nextU);
                    triangles.Add(nextUV);
                    triangles.Add(nextV);
                }
            }

            for(int u = 0; u < uPoints; u++)
            {
                int bottomIdx = u;
                int topIdx = (vPoints - 3) * uPoints + u;

                int bottomNextIdx = (u + 1) % uPoints;
                int topNextIdx = (vPoints - 3) * uPoints + ((u + 1) % uPoints);

                triangles.Add(vertexCount - 2);
                triangles.Add(bottomNextIdx);
                triangles.Add(bottomIdx);

                triangles.Add(topIdx);
                triangles.Add(topNextIdx);
                triangles.Add(vertexCount - 1);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();


            return mesh;
        }

        public static Mesh CreateTorus(Torus torus, int minorCirclePoints = 24, int majorCirclePoints = 24)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[minorCirclePoints * majorCirclePoints];
            var triangles = new int[minorCirclePoints * majorCirclePoints * 2 * 3];

            float tau = 2 * Mathf.PI;

            float minorAngle = 0.0f;
            float minorAngleIncrease = tau / (float)minorCirclePoints;

            float majorAngle = 0.0f;
            float majorAngleIncrease = tau / (float)majorCirclePoints;

            for(int i = 0; i < majorCirclePoints; i++) {

                int iPlusOne = (i + 1) % majorCirclePoints;
                minorAngle = 0.0f;
                for(int j = 0; j < minorCirclePoints; j++)
                {
                    int idx = i * minorCirclePoints + j;
                    int jPlusOne = ((j + 1) % minorCirclePoints);
                    
                    Vector3 innerCirclePos = new Vector3(Mathf.Cos(minorAngle), Mathf.Sin(minorAngle), 0.0f) * torus.minorRadius;
                    Vector3 pos = Quaternion.AngleAxis(majorAngle * Mathf.Rad2Deg, Vector3.up) * (innerCirclePos + Vector3.right * torus.majorRadius);

                    vertices[idx] = pos;


                    triangles[idx * 6 + 0] = iPlusOne * minorCirclePoints + j;
                    triangles[idx * 6 + 1] = i * minorCirclePoints + jPlusOne;
                    triangles[idx * 6 + 2] = idx;
                    triangles[idx * 6 + 3] = iPlusOne * minorCirclePoints + jPlusOne;
                    triangles[idx * 6 + 4] = i * minorCirclePoints + jPlusOne;
                    triangles[idx * 6 + 5] = iPlusOne * minorCirclePoints + j;

                    minorAngle += minorAngleIncrease;
                }

                majorAngle += majorAngleIncrease;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateCone(Cone cone, int circlePoints = 32)
        {
            if(circlePoints < 3)
            {
                Debug.LogError("Unable to create a cone with fewer than three points at its base!");
            }

            if (circlePoints < 3)
            {
                Debug.LogError("Unable to create a cylinder with fewer than three points at its base!");
            }

            var mesh = new Mesh();

            var vertices = new Vector3[circlePoints * 2 + 1];
            var triangles = new int[(circlePoints - 2) * 3 + circlePoints * 3];

            float angle = 0.0f;
            float angleIncrease = Constants.TAU / (float)circlePoints;

            int startIdx = 0;
            int triangleIdx = 0;
            int lastIdx = vertices.Length - 1;
            vertices[lastIdx] = new Vector3(0.0f, cone.height, 0.0f);
            for (int i = 0; i < circlePoints; i++)
            {
                int idxA = i;
                int idxB = i + circlePoints;

                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                var circlePos = new Vector3(cos * cone.radius, 0.0f, sin * cone.radius);

                vertices[idxA] = circlePos;
                vertices[idxB] = circlePos;

                angle += angleIncrease;

                if (i < circlePoints - 2)
                {
                    triangles[triangleIdx + 0] = startIdx;
                    triangles[triangleIdx + 1] = (idxA + 1);
                    triangles[triangleIdx + 2] = (idxA + 2);

                    triangleIdx += 3;
                }

                int bNext = (idxB + 1 - circlePoints) >= circlePoints ? idxB + 1 - circlePoints : idxB + 1;

                triangles[triangleIdx + 0] = lastIdx;
                triangles[triangleIdx + 1] = bNext;
                triangles[triangleIdx + 2] = idxB;

                triangleIdx += 3;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

        public static Mesh CreateCylinder(Cylinder cylinder, int circlePoints = 32)
        {

            if (circlePoints < 3)
            {
                Debug.LogError("Unable to create a cylinder with fewer than three points at its base!");
            }

            var mesh = new Mesh();

            var vertices = new Vector3[circlePoints * 2 * 2];
            var triangles = new int[circlePoints * 3 * 2 + (circlePoints - 1) * 3 * 2];

            float angle = 0.0f;
            float angleIncrease = Constants.TAU / (float)circlePoints;


            for (int i = 0; i < circlePoints; i++)
            {
                int lowerPointIdx = i;
                int upperPointIdx = i + circlePoints;

                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                var circlePos = new Vector3(cos * cylinder.radius, 0.0f, sin * cylinder.radius);

                Vector3 lowerPos = circlePos - 0.5f * cylinder.height * Vector3.up;
                Vector3 upperPos = circlePos + 0.5f * cylinder.height * Vector3.up;

                vertices[lowerPointIdx] = lowerPos;
                vertices[upperPointIdx] = upperPos;
                vertices[lowerPointIdx + circlePoints * 2] = lowerPos;
                vertices[upperPointIdx + circlePoints * 2] = upperPos;

                angle += angleIncrease;

                int triangleStart = lowerPointIdx * 6;

                int upperNext = upperPointIdx + 1;
                if (upperNext - circlePoints >= circlePoints) upperNext = circlePoints + (upperNext % circlePoints);

                triangles[triangleStart] = lowerPointIdx;
                triangles[triangleStart + 1] = upperPointIdx;
                triangles[triangleStart + 2] = upperNext;
                triangles[triangleStart + 3] = upperNext;
                triangles[triangleStart + 4] = (lowerPointIdx + 1) % circlePoints;
                triangles[triangleStart + 5] = lowerPointIdx;
            }

            int triangleOffset = circlePoints * 2 * 3;
            for(int i = 0; i < circlePoints - 2; i++)
            {
                int lowerPointIdx = i + circlePoints * 2;
                int upperPointIdx = i + circlePoints * 3;

                int upperNext = upperPointIdx + 1;
                if (upperNext - 3 * circlePoints >= circlePoints) upperNext = 3 * circlePoints + (upperNext % circlePoints);

                int upperNextNext = upperPointIdx + 2;
                if (upperNextNext - 3 * circlePoints >= circlePoints) upperNextNext = 3 * circlePoints + (upperNextNext % circlePoints);

                int lowerNext = lowerPointIdx + 1;
                if (lowerNext - 2 * circlePoints >= circlePoints) lowerNext = 2 * circlePoints + (lowerNext % circlePoints);

                int lowerNextNext = lowerPointIdx + 2;
                if (lowerNextNext - 2 * circlePoints >= lowerNextNext) lowerNextNext = 2 * circlePoints + (lowerNextNext % circlePoints);

                triangles[triangleOffset + i * 6 + 0] = circlePoints * 2;
                triangles[triangleOffset + i * 6 + 1] = lowerNext;
                triangles[triangleOffset + i * 6 + 2] = lowerNextNext;

                triangles[triangleOffset + i * 6 + 3] = upperNextNext;
                triangles[triangleOffset + i * 6 + 4] = upperNext;
                triangles[triangleOffset + i * 6 + 5] = circlePoints * 3;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }

    }
}
