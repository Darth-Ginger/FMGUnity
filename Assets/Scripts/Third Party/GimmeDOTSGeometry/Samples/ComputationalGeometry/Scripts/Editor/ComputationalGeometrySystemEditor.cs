
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{

    [CustomEditor(typeof(ComputationalGeometrySystem))]
    public class ComputationalGeometrySystemEditor : Editor
    {
        private bool leftClicked = false;
        private bool shift = false;

        private Camera mainCamera;
        private ComputationalGeometrySystem system;

        private Vector2 segmentStart;



        private static float mouseOffset = 42.0f;


        private void OnEnable()
        {
            this.system = this.target as ComputationalGeometrySystem;
            this.mainCamera = Object.FindAnyObjectByType<Camera>();
        }


        private void DrawSegments()
        {

        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }

        private bool GetMousePlanePosition(out Vector3 worldPos)
        {
            Event e = Event.current;
            var mousePos = e.mousePosition;
            var sceneCamera = SceneView.lastActiveSceneView.camera;
            if (sceneCamera != null)
            {

                var cameraRay = sceneCamera.ScreenPointToRay(new Vector3(mousePos.x, Screen.height - mousePos.y - mouseOffset, 0.0f));
                var plane = new Plane(Vector3.up, Vector3.zero);
                if (plane.Raycast(cameraRay, out float dist))
                {
                    worldPos = sceneCamera.transform.position + cameraRay.direction * dist;
                    return true;
                }
            }
            worldPos = Vector3.zero;
            return false;
        }

        private void HandleInput()
        {
            Event e = Event.current;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.LeftShift)
            {
                this.shift = true;
            }
            else if (this.shift && e.type == EventType.KeyUp && e.keyCode == KeyCode.LeftShift)
            {
                this.shift = false;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (this.GetMousePlanePosition(out var worldPos))
                {
                    this.segmentStart = new Vector2(worldPos.x, worldPos.z);
                    this.leftClicked = true;
                }
            }
            else if (this.leftClicked && e.type == EventType.MouseUp && e.button == 0)
            {
                if (this.GetMousePlanePosition(out var worldPos))
                {
                    var segmentEnd = new Vector2(worldPos.x, worldPos.z);

                    var segment = new LineSegment2D(this.segmentStart, segmentEnd);
                    if (this.shift)
                    {
                        this.system.secondaryLineSegments.Add(segment);
                    }
                    else
                    {
                        this.system.lineSegments.Add(segment);
                    }
                }
                this.leftClicked = false;
            }
        }

        private void DrawCameraBounds()
        {
            if (this.mainCamera != null)
            {
                Vector3[] frustumCorners = new Vector3[4];
                this.mainCamera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), this.mainCamera.transform.position.y, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

                Handles.color = Color.blue;
                for (int i = 0; i < frustumCorners.Length; i++)
                {
                    var worldFrustumCorner = this.mainCamera.transform.TransformPoint(frustumCorners[i]);
                    var nextWorldFrustumCorner = this.mainCamera.transform.TransformPoint(frustumCorners[(i + 1) % frustumCorners.Length]);
                    Handles.DrawSolidDisc(worldFrustumCorner, Vector3.up, 0.2f);
                    Handles.DrawLine(worldFrustumCorner, nextWorldFrustumCorner, 0.1f);
                }
            }
        }

        private void OnSceneGUI()
        {
            HandleUtility.AddDefaultControl(1);

            Handles.color = Color.cyan;
            for (int i = 0; i < this.system.lineSegments.Count; i++)
            {
                var segment = this.system.lineSegments[i];

                Handles.DrawSolidDisc(new Vector3(segment.a.x, 0.0f, segment.a.y), Vector3.up, 0.3f);
                Handles.DrawSolidDisc(new Vector3(segment.b.x, 0.0f, segment.b.y), Vector3.up, 0.3f);
                Handles.DrawLine(new Vector3(segment.a.x, 0.0f, segment.a.y), new Vector3(segment.b.x, 0.0f, segment.b.y));
            }

            Handles.color = Color.gray;
            for (int i = 0; i < this.system.secondaryLineSegments.Count; i++)
            {
                var segment = this.system.secondaryLineSegments[i];

                Handles.DrawSolidDisc(new Vector3(segment.a.x, 0.0f, segment.a.y), Vector3.up, 0.3f);
                Handles.DrawSolidDisc(new Vector3(segment.b.x, 0.0f, segment.b.y), Vector3.up, 0.3f);
                Handles.DrawLine(new Vector3(segment.a.x, 0.0f, segment.a.y), new Vector3(segment.b.x, 0.0f, segment.b.y));
            }

            this.HandleInput();

            if (this.leftClicked)
            {
                if (this.GetMousePlanePosition(out var worldPos))
                {
                    Handles.color = Color.red;
                    Handles.DrawLine(new Vector3(this.segmentStart.x, 0.0f, this.segmentStart.y), worldPos);
                    SceneView.RepaintAll();
                }
            }

            this.DrawCameraBounds();
        }

    }
}