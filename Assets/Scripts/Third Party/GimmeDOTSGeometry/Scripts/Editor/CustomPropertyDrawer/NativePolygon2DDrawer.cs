using UnityEditor;
using UnityEngine;


namespace GimmeDOTSGeometry
{
    
    [CustomPropertyDrawer(typeof(NativePolygon2D))]
    public class NativePolygon2DDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = UnityEditor.EditorGUIUtility.singleLineHeight * 4;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var labelRect = position;
            labelRect.height = UnityEditor.EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(labelRect, label);

            EditorGUI.indentLevel++;

            var helpBoxRect = position;
            helpBoxRect.yMin = position.yMin + UnityEditor.EditorGUIUtility.singleLineHeight;
            helpBoxRect.yMax = helpBoxRect.yMin + UnityEditor.EditorGUIUtility.singleLineHeight * 3;

            EditorGUI.HelpBox(helpBoxRect, "Strg + Left Mouse -> Add Vertex\nShift + Left Mouse -> Remove Vertex\nStrg + Right Mouse -> Add Hole\nShift + Right Mouse -> Remove Hole", MessageType.None);

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }
    }
}
