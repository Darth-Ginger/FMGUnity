using FMGUnity.Utility.Interfaces;
using UnityEditor;
using UnityEngine;

namespace FMGUnity.Utility
{
    [CustomEditor(typeof(IIdentifiable))]
    public class IIdentifiableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            IIdentifiable identifiable = (IIdentifiable)target;
            EditorGUILayout.LabelField("Id", identifiable.Id.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            DrawDefaultInspector();
        }
    }
}

