using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    [CustomEditor(typeof(SkipListDrawer))]
    public class SkipListDrawerEditor : Editor
    {

        private SkipListDrawer drawer;

        private void OnEnable()
        {
            this.drawer = this.target as SkipListDrawer;
        }

        public override void OnInspectorGUI()
        {

            EditorGUILayout.LabelField("Skip List");

            if (this.drawer != null && !this.drawer.finished)
            {
                var controlRect = EditorGUILayout.GetControlRect(false, this.drawer.ySize);
                GUIUtility.DrawSkipList(controlRect, this.drawer.list);
            }

            if(GUILayout.Button("Finish"))
            {
                this.drawer.finished = true;
                this.drawer = null;
            }
        }
    }
}
