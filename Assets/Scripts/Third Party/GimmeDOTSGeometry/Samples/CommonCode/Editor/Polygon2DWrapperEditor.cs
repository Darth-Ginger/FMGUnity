

using UnityEditor;
using UnityEngine;


namespace GimmeDOTSGeometry.Samples
{

    [CustomEditor(typeof(Polygon2DWrapper))]
    public class Polygon2DWrapperEditor : Editor
    {
        private bool pauseTriangulation = false;
        private bool isControlPressed = false;

        private Polygon2DWrapper polyClass;
        private NativePolygon2D polygon;
        private NativePolygon2DHandle polygonHandle;


        private void OnEnable()
        {
            this.polyClass = this.target as Polygon2DWrapper;
            this.polyClass.Init();
            this.polygon = this.polyClass.polygon;
            this.polygonHandle = new NativePolygon2DHandle(this, this.polygon, Color.black, Color.red, Color.cyan * 0.5f, 0.0f);
        }

        private void OnDisable()
        {
            this.polyClass.Dispose();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            this.polygonHandle.OnInspectorGUI();

            if (this.polyClass.filePath != null && this.polyClass.filePath != string.Empty && GUILayout.Button("Save"))
            {
                var path = Application.dataPath + this.polyClass.filePath;
                this.polygon.SaveAsBinary(path);
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
                    else if (e.keyCode == KeyCode.S && this.isControlPressed)
                    {
                        var path = Application.dataPath + this.polyClass.filePath;
                        this.polygon.SaveAsBinary(path);
                        Debug.Log($"Saved polygont to {path}");
                    }
                }

                else if (e.type == EventType.KeyUp)
                {
                    if (e.keyCode == KeyCode.LeftControl)
                    {
                        this.isControlPressed = false;

                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            if (!this.pauseTriangulation)
            {
                this.polygonHandle.OnSceneGUI();
            }

            this.HandleInput();
        }
    }
}