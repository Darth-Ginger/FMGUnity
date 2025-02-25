using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public class NativePolygon2DHandle
    {

        #region Public Variables

        public bool showLabel;
        public bool showCenterPositionHandles;
        public bool drawTriangleLines;
        public bool showConvexBorder;
        public bool pauseTriangulation;

        #endregion

        #region Private Variables

        private bool isControlPressed = false;
        private bool isShiftPressed = false;
        private bool isAltPressed = false;
        private bool inputEnabled = true;

        private CardinalPlane plane = CardinalPlane.XZ;

        private Color borderColor;
        private Color holeBorderColor;
        private Color areaColor;

        private float height = 0.0f;

        private UnityEngine.Object owner = null;

        private List<Vector3> handlePoints = new List<Vector3>();
        private List<Vector3> centerHandlePoints = new List<Vector3>();

        private NativePolygon2D polygon;

        private class TemporaryPolygon
        {
            public List<Vector2> points = new List<Vector2>();
            public List<int> separators = new List<int>();

            public List<Vector3> handlePoints = new List<Vector3>();
            public List<Vector3> centerHandlePoints = new List<Vector3>();

            public TemporaryPolygon(NativePolygon2D polygon, List<Vector3> handlePoints, List<Vector3> centerHandlePoints)
            {
                for(int i = 0; i < polygon.points.Length; i++)
                {
                    this.points.Add(polygon.points[i]);
                }

                for(int i = 0; i < polygon.separators.Length; i++)
                {
                    this.separators.Add(polygon.separators[i]);
                }

                for(int i = 0; i < handlePoints.Count; i++)
                {
                    this.handlePoints.Add(handlePoints[i]);
                }

                for(int i = 0; i < centerHandlePoints.Count; i++)
                {
                    this.centerHandlePoints.Add(centerHandlePoints[i]);
                }

            }
        }

        private Stack<TemporaryPolygon> history = new Stack<TemporaryPolygon>();

        private static float mouseOffset = 42.0f;

        #endregion

        public struct PolygonIndex
        {
            public int vertexIndex;
            public int subsurface;
        }

        public NativePolygon2D GetModifiedPolygon() => this.polygon;

        public NativePolygon2DHandle(UnityEngine.Object owner, NativePolygon2D polygon, Color borderColor, Color holeBorderColor, Color areaColor, CardinalPlane cardinalPlane = CardinalPlane.XZ, float height = 0.0f, bool showLabel = false)
        {
            this.owner = owner;
            this.polygon = polygon;
            this.borderColor = borderColor;
            this.holeBorderColor = holeBorderColor;
            this.areaColor = areaColor;
            this.showLabel = showLabel;
            this.plane = cardinalPlane;

            var points = polygon.points;
            var separators = polygon.separators;

            Vector3 avg = Vector3.zero;

            var axisIndices = cardinalPlane.GetAxisIndices();
            int normalIndex = cardinalPlane.GetNormalDirectionIndex();

            for (int i = 0; i < points.Length; i++)
            {
                var point = polygon.points.ElementAt(i);
                var pos = new Vector3();

                pos[axisIndices.x] = point.x;
                pos[axisIndices.y] = point.y;
                pos[normalIndex] = height;

                this.handlePoints.Add(pos);
                avg += pos;
            }
            this.centerHandlePoints.Add(avg / points.Length);
            for (int i = 0; i < separators.Length; i++)
            {
                this.centerHandlePoints.Add(Vector3.zero);
            }
            this.inputEnabled = true;
        }

        public void AddPoint(PolygonIndex index, Vector3 position)
        {

            if (index.vertexIndex >= 0)
            {
                var axisIndices = this.plane.GetAxisIndices();
                int normalIndex = this.plane.GetNormalDirectionIndex();

                var nextIdx = GetNextIndex(index);

                var newPoint = new Vector2(position[axisIndices.x], position[axisIndices.y]);
                var newHandlePoint = new Vector3();


                newHandlePoint[axisIndices.x] = position[axisIndices.x];
                newHandlePoint[axisIndices.y] = position[axisIndices.y];
                newHandlePoint[normalIndex] = this.height;

                this.StorePolygonForUndo();
                if (index.subsurface > 0)
                {
                    if (this.polygon.IsPointInside(newPoint))
                    {
                        this.polygon.InsertPoint(nextIdx.vertexIndex, newPoint);
                        this.handlePoints.Insert(nextIdx.vertexIndex, newHandlePoint);
                    }
                }
                else
                {
                    this.polygon.InsertPoint(nextIdx.vertexIndex, newPoint);
                    this.handlePoints.Insert(nextIdx.vertexIndex, newHandlePoint);
                }

                EditorUtility.SetDirty(this.owner);
            }
        }

        private void StorePolygonForUndo()
        {
            this.history.Push(new TemporaryPolygon(this.polygon, this.handlePoints, this.centerHandlePoints));
        }

        public void AddHole(Vector3 position)
        {
            var axisIndices = this.plane.GetAxisIndices();

            var pos = new Vector2(position[axisIndices.x], position[axisIndices.y]);

            if (this.polygon.IsPointInside(pos))
            {
                this.StorePolygonForUndo();
                var holePoints = new List<Vector2>();
                holePoints.Add(pos);

                this.polygon.AddHole(holePoints);

                int normalIndex = this.plane.GetNormalDirectionIndex();

                var worldPos = new Vector3();

                worldPos[axisIndices.x] = position.x;
                worldPos[axisIndices.y] = position.y;
                worldPos[normalIndex] = this.height;

                this.handlePoints.Add(worldPos);
                this.centerHandlePoints.Add(worldPos);

                EditorUtility.SetDirty(this.owner);
            }
        }

        public void RemoveHole(PolygonIndex index)
        {
            if (index.subsurface > 0)
            {
                this.StorePolygonForUndo();
                int subsurfaceStart = this.polygon.separators.ElementAt(index.subsurface - 1);
                int subsurfaceEnd = this.polygon.points.Length;
                if (index.subsurface < this.polygon.separators.Length)
                {
                    subsurfaceEnd = this.polygon.separators.ElementAt(index.subsurface);
                }
                int length = subsurfaceEnd - subsurfaceStart;

                this.handlePoints.RemoveRange(subsurfaceStart, length);
                this.centerHandlePoints.RemoveAt(index.subsurface);
                this.polygon.RemoveHole(index.subsurface);

                EditorUtility.SetDirty(this.owner);
            }
        }

        public void RemovePoint(PolygonIndex index)
        {
            if (index.vertexIndex >= 0)
            {
                this.StorePolygonForUndo();
                this.polygon.RemovePoint(index.vertexIndex, out int removedSubsurface);
                this.handlePoints.RemoveAt(index.vertexIndex);
                if (removedSubsurface > 0)
                {
                    this.centerHandlePoints.RemoveAt(removedSubsurface);
                }

                EditorUtility.SetDirty(this.owner);
            }
        }

        private PolygonIndex GetClosestIndex(Vector3 position)
        {
            var axisIndices = this.plane.GetAxisIndices();

            float2 pos = new float2(position[axisIndices.x], position[axisIndices.y]);

            NativePolygon2D.Distance(this.polygon, pos, out int idx, out int subsurface);

            return new PolygonIndex()
            {
                vertexIndex = idx,
                subsurface = subsurface,
            };
        }

        private Vector3 GetPoint(int idx)
        {
            var axisIndices = this.plane.GetAxisIndices();
            int normalIndex = this.plane.GetNormalDirectionIndex();

            var worldPos = new Vector3();

            float2 polyPoint = this.polygon.points.ElementAt(idx);

            worldPos[axisIndices.x] = polyPoint.x;
            worldPos[axisIndices.y] = polyPoint.y;
            worldPos[normalIndex] = this.height;

            return worldPos;
        }

        private PolygonIndex GetNextIndex(PolygonIndex index)
        {
            int subsurfaceIdx = index.subsurface;

            int subsurface = 0;
            if (subsurfaceIdx > 0 && subsurfaceIdx - 1 < this.polygon.separators.Length)
            {
                subsurface = this.polygon.separators.ElementAt(subsurfaceIdx - 1);
            }
            int nextSubsurface = this.polygon.points.Length;
            if (subsurfaceIdx < this.polygon.separators.Length)
            {
                nextSubsurface = this.polygon.separators.ElementAt(subsurfaceIdx);
            }
            int length = nextSubsurface - subsurface;

            return new PolygonIndex()
            {
                vertexIndex = subsurface + ((index.vertexIndex - subsurface + 1) % length),
                subsurface = subsurfaceIdx
            };

        }

        private void DrawInputLines(Event e)
        {
            if (this.isControlPressed || this.isShiftPressed)
            {

                var mousePos = e.mousePosition;
                var sceneCamera = SceneView.lastActiveSceneView.camera;
                if (sceneCamera != null)
                {

                    var cameraRay = sceneCamera.ScreenPointToRay(new Vector3(mousePos.x, Screen.height - mousePos.y - mouseOffset, 0.0f));

                    int normalAxis = this.plane.GetNormalDirectionIndex();
                    var axisIndices = this.plane.GetAxisIndices();

                    var pointInPlane = Vector3.zero;
                    pointInPlane[normalAxis] = this.height;

                    var planeDirection = Vector3.zero;
                    planeDirection[normalAxis] = 1.0f;

                    var plane = new Plane(planeDirection, pointInPlane);
                    if (plane.Raycast(cameraRay, out float dist))
                    {

                        var worldPos = cameraRay.origin + cameraRay.direction * dist;
                        var closestIndex = this.GetClosestIndex(worldPos);

                        if (closestIndex.vertexIndex >= 0)
                        {
                            if (this.isControlPressed)
                            {
                                var nextIndex = this.GetNextIndex(closestIndex);
                                var polyPos = GetPoint(closestIndex.vertexIndex);
                                var nextPolyPos = GetPoint(nextIndex.vertexIndex);

                                var oldColor = Handles.color;
                                Handles.color = Color.green;
                                Handles.DrawLine(polyPos, worldPos);
                                Handles.DrawLine(nextPolyPos, worldPos);

                                Handles.color = oldColor;

                                SceneView.RepaintAll();
                            }
                            else
                            {
                                var polyPos = GetPoint(closestIndex.vertexIndex);

                                var oldColor = Handles.color;
                                Handles.color = Color.red;
                                Handles.DrawLine(polyPos, worldPos);

                                Handles.color = oldColor;

                                SceneView.RepaintAll();
                            }
                        }
                        else
                        {
                            if (this.isControlPressed)
                            {
                                var oldColor = Handles.color;
                                Handles.color = Color.green * 0.3f;
                                Handles.DrawSolidDisc(worldPos, Vector3.up, 0.5f);

                                Handles.color = oldColor;

                                SceneView.RepaintAll();
                            }
                        }
                    }
                }
            }
        }

        private void Undo()
        {
            if(this.history.Count > 0)
            {
                var tmpPolygon = this.history.Pop();

                this.polygon.points.Clear();
                this.polygon.separators.Clear();
                this.handlePoints.Clear();
                this.centerHandlePoints.Clear();

                for(int i = 0; i < tmpPolygon.points.Count; i++)
                {
                    this.polygon.points.Add(tmpPolygon.points[i]);
                }

                for(int i = 0; i < tmpPolygon.separators.Count; i++)
                {
                    this.polygon.separators.Add(tmpPolygon.separators[i]);
                }

                for(int i = 0; i < tmpPolygon.handlePoints.Count; i++)
                {
                    this.handlePoints.Add(tmpPolygon.handlePoints[i]);
                }

                for(int i = 0; i < tmpPolygon.centerHandlePoints.Count; i++)
                {
                    this.centerHandlePoints.Add(tmpPolygon.centerHandlePoints[i]);
                }

            }
        }

        private void HandleInput()
        {
            Event e = Event.current;

            if (e.isKey)
            {
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        this.isControlPressed = true;
                    }
                    else if (e.keyCode == KeyCode.LeftShift)
                    {
                        this.isShiftPressed = true;
                    }
                    else if (e.keyCode == KeyCode.LeftAlt)
                    {
                        this.isAltPressed = true;
                    } else if(e.keyCode == KeyCode.W && this.isControlPressed)
                    {
                        this.Undo();
                    }
                }
                else if (e.type == EventType.KeyUp)
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        this.isControlPressed = false;

                    }
                    else if (e.keyCode == KeyCode.LeftShift)
                    {
                        this.isShiftPressed = false;
                    }
                    else if (e.keyCode == KeyCode.LeftAlt)
                    {
                        this.isAltPressed = false;
                    }
                }
            }

            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {

                var mousePos = e.mousePosition;
                var sceneCamera = SceneView.lastActiveSceneView.camera;
                if (sceneCamera != null)
                {
                    var cameraRay = sceneCamera.ScreenPointToRay(new Vector3(mousePos.x, Screen.height - mousePos.y - mouseOffset, 0.0f));

                    int normalAxis = this.plane.GetNormalDirectionIndex();
                    var axisIndices = this.plane.GetAxisIndices();

                    var pointInPlane = Vector3.zero;
                    pointInPlane[normalAxis] = this.height;

                    var planeDirection = Vector3.zero;
                    planeDirection[normalAxis] = 1.0f;


                    var plane = new Plane(planeDirection, pointInPlane);
                    if (plane.Raycast(cameraRay, out float dist))
                    {
                        var worldPos = cameraRay.origin + cameraRay.direction * dist;

                        var closestIndex = this.GetClosestIndex(worldPos);
                        if (this.polygon.points.Length >= 3)
                        {
                            if (closestIndex.vertexIndex >= 0)
                            {
                                if (this.isControlPressed)
                                {
                                    if (e.button == 0)
                                    {
                                        this.AddPoint(closestIndex, worldPos);
                                    }
                                    else if (e.button == 1)
                                    {
                                        this.AddHole(worldPos);
                                    }
                                }
                                else if (this.isShiftPressed)
                                {
                                    if (e.button == 0)
                                    {
                                        this.RemovePoint(closestIndex);
                                    }
                                    else if (e.button == 1)
                                    {
                                        this.RemoveHole(closestIndex);
                                    }

                                }
                            }
                        }
                        else if (e.button == 0)
                        {

                            if (this.isControlPressed)
                            {
                                StorePolygonForUndo();
                                var newPoint = new Vector2(worldPos[axisIndices.x], worldPos[axisIndices.y]);

                                var newHandlePoint = new Vector3();

                                newHandlePoint[axisIndices.x] = worldPos[axisIndices.x];
                                newHandlePoint[axisIndices.y] = worldPos[axisIndices.y];
                                newHandlePoint[normalAxis] = this.height;

                                var list = this.polygon.points;
                                list.Add(newPoint);
                                this.polygon.points = list;

                                this.handlePoints.Add(newHandlePoint);
                            }
                            else if (this.isShiftPressed)
                            {
                                if (closestIndex.vertexIndex >= 0)
                                {
                                    this.RemovePoint(closestIndex);
                                }
                            }
                        }
                    }
                }

            }

            this.DrawInputLines(e);
        }

        private void UpdateCenterHandlePositions()
        {


            float2 avg = float2.zero;
            float2 holeAvg = float2.zero;

            int subsurface = 0;
            int current = 0;
            int next = this.polygon.points.Length;
            if (this.polygon.separators.Length > 0)
            {
                next = this.polygon.separators.ElementAt(0);
            }

            for (int i = 0; i < this.polygon.points.Length; i++)
            {
                var point = this.polygon.points.ElementAt(i);

                avg += point;

                if (subsurface > 0)
                {
                    holeAvg += point;
                }

                if (i >= next - 1)
                {
                    holeAvg /= (float)(next - current);

                    this.centerHandlePoints[subsurface] = ToPlanePoint(holeAvg);
                    subsurface++;

                    current = next;
                    if (subsurface < this.polygon.separators.Length)
                    {
                        next = this.polygon.separators[subsurface];
                    }
                    else
                    {
                        next = this.polygon.points.Length;
                    }
                    holeAvg = float2.zero;
                }
            }

            avg /= (float)this.polygon.points.Length;

            this.centerHandlePoints[0] = ToPlanePoint(avg);
        }

        private Vector3 ToPlanePoint(Vector2 point)
        {
            int normalAxis = this.plane.GetNormalDirectionIndex();
            var axisIndices = this.plane.GetAxisIndices();

            Vector3 p = new Vector3();
            p[normalAxis] = this.height;
            p[axisIndices.x] = point.x;
            p[axisIndices.y] = point.y;
            return p;
        }

        private void HandleControls()
        {
            if (!this.isAltPressed)
            {
                int normalAxis = this.plane.GetNormalDirectionIndex();
                var axisIndices = this.plane.GetAxisIndices();

                for (int i = 0; i < this.handlePoints.Count; i++)
                {
                    EditorGUI.BeginChangeCheck();
                    this.handlePoints[i] = Handles.PositionHandle(this.handlePoints[i], Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        var handlePoint = new Vector3();
                        handlePoint[normalAxis] = this.height;
                        handlePoint[axisIndices.x] = this.handlePoints[i][axisIndices.x];
                        handlePoint[axisIndices.y] = this.handlePoints[i][axisIndices.y];

                        this.handlePoints[i] = handlePoint;

                        this.polygon.points.ElementAt(i) = new Vector2(this.handlePoints[i][axisIndices.x], this.handlePoints[i][axisIndices.y]);
                        EditorUtility.SetDirty(this.owner);
                    }
                }

                if (this.showCenterPositionHandles)
                {

                    this.UpdateCenterHandlePositions();
                    for (int i = 0; i < this.centerHandlePoints.Count; i++)
                    {
                        var oldPos = this.centerHandlePoints[i];
                        EditorGUI.BeginChangeCheck();
                        this.centerHandlePoints[i] = Handles.PositionHandle(this.centerHandlePoints[i], Quaternion.identity);
                        if (EditorGUI.EndChangeCheck())
                        {

                            var centerHandlePoint = new Vector3();
                            centerHandlePoint[normalAxis] = this.height;
                            centerHandlePoint[axisIndices.x] = this.centerHandlePoints[i][axisIndices.x];
                            centerHandlePoint[axisIndices.y] = this.centerHandlePoints[i][axisIndices.y];

                            var diff = centerHandlePoint - oldPos;

                            var diff2D = new Vector2(diff[axisIndices.x], diff[axisIndices.y]);

                            if (i == 0)
                            {
                                for (int j = 0; j < this.polygon.points.Length; j++)
                                {
                                    this.polygon.points.ElementAt(j) += (float2)diff2D;
                                    this.handlePoints[j] += diff;
                                }

                            }
                            else
                            {
                                int subsurface = i - 1;
                                int start = this.polygon.separators.ElementAt(subsurface);
                                int end = this.polygon.points.Length;
                                if (subsurface + 1 < this.polygon.separators.Length)
                                {
                                    end = this.polygon.separators[subsurface + 1];
                                }

                                for (int j = start; j < end; j++)
                                {
                                    this.polygon.points.ElementAt(j) += (float2)diff2D;
                                    this.handlePoints[j] += diff;
                                }
                            }
                            this.centerHandlePoints[i] = centerHandlePoint;
                            EditorUtility.SetDirty(this.owner);
                        }
                    }
                }
            }
        }

        private void DrawConvexBorder()
        {
            List<float2> points = new List<float2>();
            for (int i = 0; i < this.polygon.points.Length; i++)
            {
                points.Add(this.polygon.points.ElementAt(i));
            }
            var convexPolygon = HullAlgorithms.CreateConvexHull(Allocator.TempJob, points, true);

            var oldColor = Handles.color;
            Handles.color = this.borderColor;

            for (int i = 0; i < convexPolygon.points.Length; i++)
            {
                int nextIdx = (i + 1) % convexPolygon.points.Length;

                var point = convexPolygon.points.ElementAt(i);
                var nextPoint = convexPolygon.points.ElementAt(nextIdx);

                
                var point3D = this.ToPlanePoint(point);
                var nextPoint3D = this.ToPlanePoint(nextPoint);

                Handles.DrawLine(point3D, nextPoint3D);
            }

            Handles.color = oldColor;

            convexPolygon.Dispose();
        }

        private void DrawPolygon2D()
        {
            var oldHandleColor = Handles.color;


            int subsurfaceIdx = 0;
            int subsurface = 0;
            int nextSubsurface = this.polygon.points.Length;
            if (subsurfaceIdx < this.polygon.separators.Length)
            {
                nextSubsurface = this.polygon.separators.ElementAt(subsurfaceIdx);
            }
            int length = nextSubsurface - subsurface;

            Handles.color = this.borderColor;
            for (int i = 0; i < this.handlePoints.Count; i++)
            {

                var pointA = this.handlePoints[i];
                var pointB = this.handlePoints[subsurface + ((i - subsurface + 1) % length)];

                Handles.DrawLine(pointA, pointB, 2.0f);

                if (i >= nextSubsurface - 1)
                {
                    Handles.color = this.holeBorderColor;

                    subsurfaceIdx++;
                    if (subsurfaceIdx < this.polygon.separators.Length)
                    {
                        subsurface = nextSubsurface;
                        nextSubsurface = this.polygon.separators.ElementAt(subsurfaceIdx);
                    }
                    else
                    {
                        subsurface = nextSubsurface;
                        nextSubsurface = this.polygon.points.Length;
                    }
                    length = nextSubsurface - subsurface;
                }


            }

            if (!this.pauseTriangulation)
            {
                var monotonePolyPointMapping = new NativeList<int>(Allocator.TempJob);
                var monotonePolySeparators = new NativeList<int>(Allocator.TempJob);
                var triangulation = new NativeList<int>(Allocator.TempJob);

                var yMonotoneJob = new Polygon2DTriangulationJobs.MonotoneDecompositionJob()
                {
                    epsilon = 10e-5f,
                    polyPoints = polygon.points,
                    polySeparators = polygon.separators,
                    monotonePolyPointMapping = monotonePolyPointMapping,
                    monotonePolySeparators = monotonePolySeparators,
                };
                yMonotoneJob.Schedule().Complete();

                int startIdx = 0;
                for (int i = 0; i < monotonePolySeparators.Length + 1; i++)
                {
                    int endIdx;
                    if (i < monotonePolySeparators.Length)
                    {
                        endIdx = monotonePolySeparators.ElementAt(i);
                    }
                    else
                    {
                        endIdx = monotonePolyPointMapping.Length;
                    }

                    for (int j = startIdx; j < endIdx; j++)
                    {
                        int current = monotonePolyPointMapping[j];
                        int next = monotonePolyPointMapping[startIdx + (j - startIdx + 1) % (endIdx - startIdx)];

                        var startPoint = this.ToPlanePoint(this.polygon.points[current]);
                        var endPoint = this.ToPlanePoint(this.polygon.points[next]);

                        Handles.color = Color.black;
                        Handles.DrawLine(startPoint, endPoint);
                    }

                    startIdx = endIdx;
                }

                var triangulationJob = new Polygon2DTriangulationJobs.YMonotoneTriangulationJob()
                {
                    triangles = triangulation,
                    monotonePolyPointMapping = monotonePolyPointMapping,
                    monotonePolySeparators = monotonePolySeparators,
                    polyPoints = this.polygon.points,
                    clockwiseWinding = true
                };
                triangulationJob.Schedule().Complete();

                for (int i = 0; i < triangulation.Length / 3; i++)
                {
                    int a = triangulation[i * 3];
                    int b = triangulation[i * 3 + 1];
                    int c = triangulation[i * 3 + 2];

                    Vector2 pointA = this.polygon.points.ElementAt(a);
                    Vector2 pointB = this.polygon.points.ElementAt(b);
                    Vector2 pointC = this.polygon.points.ElementAt(c);

                    var posA = this.ToPlanePoint(pointA);
                    var posB = this.ToPlanePoint(pointB);
                    var posC = this.ToPlanePoint(pointC);

                    Handles.color = this.areaColor;
                    Handles.DrawAAConvexPolygon(posA, posB, posC);

                    if (this.drawTriangleLines)
                    {
                        Handles.color = this.borderColor;
                        Handles.DrawAAPolyLine(posA, posB, posC, posA);
                    }
                }

                monotonePolyPointMapping.Dispose();
                monotonePolySeparators.Dispose();
                triangulation.Dispose();

                /*
                var simplePolygon = NativePolygon2D.MakeSimple(Allocator.TempJob, this.polygon);
                var triangulation = Polygon2DTriangulation.EarClippingTriangulate(simplePolygon);

                for(int i = 0; i < triangulation.Count / 3; i++)
                {
                    int a = triangulation[i * 3];
                    int b = triangulation[i * 3 + 1];
                    int c = triangulation[i * 3 + 2];

                    Vector2 pointA = simplePolygon.points.ElementAt(a);
                    Vector2 pointB = simplePolygon.points.ElementAt(b);
                    Vector2 pointC = simplePolygon.points.ElementAt(c);

                    var posA = new Vector3(pointA.x, this.height, pointA.y);
                    var posB = new Vector3(pointB.x, this.height, pointB.y);
                    var posC = new Vector3(pointC.x, this.height, pointC.y);

                    Handles.color = this.areaColor;
                    Handles.DrawAAConvexPolygon(posA, posB, posC);

                    if(this.drawTriangleLines)
                    {
                        Handles.color = this.borderColor;
                        Handles.DrawAAPolyLine(posA, posB, posC, posA);
                    }
                }

                simplePolygon.Dispose();
                */
            }
            Handles.color = oldHandleColor;
        }

        private void DrawLabel()
        {
            var oldHandleColor = Handles.color;

            Handles.color = Color.white;

            int subsurfaceIdx = 0;
            int subsurface = 0;
            int nextSubsurface = this.polygon.points.Length;

            Vector3 normalDir = new Vector3();
            int normalDirIndex = this.plane.GetNormalDirectionIndex();
            normalDir[normalDirIndex] = 1.0f;

            if (this.polygon.separators.Length > 0)
            {
                nextSubsurface = this.polygon.separators.ElementAt(0);
            }
            for (int i = 0; i < this.handlePoints.Count; i++)
            {
                if (subsurfaceIdx == 0)
                {
                    Handles.Label(this.handlePoints[i] + normalDir * 0.1f, $"Point {i}");
                }
                else
                {
                    Handles.Label(this.handlePoints[i] + normalDir * 0.1f, $"Hole {subsurfaceIdx} : {i - subsurface}");
                }

                if (i >= nextSubsurface - 1)
                {
                    subsurfaceIdx++;
                    subsurface = nextSubsurface;
                    if (subsurfaceIdx < this.polygon.separators.Length)
                    {
                        nextSubsurface = this.polygon.separators.ElementAt(subsurfaceIdx);
                    }
                    else
                    {
                        nextSubsurface = this.polygon.points.Length;
                    }
                }
            }

            if (this.showCenterPositionHandles)
            {
                for (int i = 0; i < this.centerHandlePoints.Count; i++)
                {
                    if (i == 0)
                    {
                        Handles.Label(this.centerHandlePoints[i] + normalDir * 0.1f, $"Center");
                    }
                    else
                    {
                        Handles.Label(this.centerHandlePoints[i] + normalDir * 0.1f, $"Hole Center {i}");
                    }
                }
            }

            Handles.color = oldHandleColor;
        }


        public void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUIUtility.DrawHorizontalLine(1.0f);
            EditorGUILayout.LabelField("Polygon Handle");

            EditorGUILayout.HelpBox("Left Mouse: Vertex Operation" +
                "\nRight Mouse: Hole Operation" +
                "\nCtrl: Add" +
                "\nShift: Remove" +
                "\nCtrl + W: Undo", MessageType.Info);

            this.showLabel = EditorGUILayout.Toggle("Show Labels", this.showLabel);
            this.showCenterPositionHandles = EditorGUILayout.Toggle("Show Center Handles", this.showCenterPositionHandles);
            this.drawTriangleLines = EditorGUILayout.Toggle("Draw Triangle Lines", this.drawTriangleLines);
            this.showConvexBorder = EditorGUILayout.Toggle("Show Convex Border", this.showConvexBorder);
            this.pauseTriangulation = EditorGUILayout.Toggle("Pause Triangulation", this.pauseTriangulation);
            EditorGUIUtility.DrawHorizontalLine(1.0f);

            if(EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            this.inputEnabled = enabled;
        }

        public void OnSceneGUI()
        {
            
            HandleUtility.AddDefaultControl(UnityEngine.GUIUtility.GetControlID(FocusType.Passive));

            if (this.inputEnabled)
            {
                this.HandleInput();
            }
            this.HandleControls();

            this.DrawPolygon2D();
            if (this.showConvexBorder)
            {
                this.DrawConvexBorder();
            }

            if (this.showLabel)
            {
                this.DrawLabel();
            }


        }


    }
}