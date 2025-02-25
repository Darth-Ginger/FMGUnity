
using UnityEditor;
using UnityEngine;



namespace GimmeDOTSGeometry
{
    public static class EditorGUIUtility
    {

        public static void DrawHorizontalLine(float height)
        {
            var rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1.0f));
        }

        public static void DrawHorizontalLine(float height, float spaceAbove, float spaceBelow)
        {
            EditorGUILayout.Space(spaceAbove);
            DrawHorizontalLine(height);
            EditorGUILayout.Space(spaceBelow);
        }

    }
}