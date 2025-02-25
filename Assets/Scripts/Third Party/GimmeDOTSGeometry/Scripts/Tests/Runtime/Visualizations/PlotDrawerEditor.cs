using GimmeDOTSGeometry.Tools.DotsPlotter;
using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    [CustomEditor(typeof(PlotDrawer))]
    public class PlotDrawerEditor : Editor
    {
        private PlotDrawer drawer;

        private void OnEnable()
        {
            this.drawer = this.target as PlotDrawer;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Plot");

            if(this.drawer != null && !this.drawer.finished)
            {
                var controlRect = EditorGUILayout.GetControlRect(false, this.drawer.ySize);
                PlotterGUI.DrawPlotter(controlRect, this.drawer.plotter);

                var wnd = this.drawer.plotter.GetWindow();

                EditorGUILayout.BeginHorizontal();
                wnd.xMin = EditorGUILayout.FloatField("Min X", wnd.xMin);
                wnd.xMax = EditorGUILayout.FloatField("Max X", wnd.xMax);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                wnd.yMin = EditorGUILayout.FloatField("Min Y", wnd.yMin);
                wnd.yMax = EditorGUILayout.FloatField("Max Y", wnd.yMax);
                EditorGUILayout.EndHorizontal();

                this.drawer.plotter.SetWindow(wnd);

            }


            if (GUILayout.Button("Finish"))
            {
                this.drawer.finished = true;
                this.drawer = null;
            }
        }
    }
}
