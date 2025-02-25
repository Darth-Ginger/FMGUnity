using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    [CustomEditor(typeof(LineSegmentIntersectionStepper))]
    public class LineSegmentIntersectionStepperEditor : Editor
    {

        private LineSegmentIntersectionStepper stepper;

        private void OnEnable()
        {
            this.stepper = this.target as LineSegmentIntersectionStepper;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if(GUILayout.Button("Step"))
            {
                this.stepper.Step();
            }

            EditorGUILayout.LabelField("Event Queue");
            var controlRect = EditorGUILayout.GetControlRect(false, 500.0f);
            GUIUtility.DrawTree(controlRect, this.stepper.EventQueue, (node) =>
            {
                if (node is NativeRBTree<LineIntersectionJobs.EventPoint, LineIntersectionJobs.EventPointComparer>.TreeNode rbTreeNode)
                {
                    if (rbTreeNode.Black == 0)
                    {
                        GL.Color(Color.red);
                    }
                    else
                    {
                        GL.Color(Color.black);
                    }
                }

            });

            EditorGUILayout.LabelField("Status");
            controlRect = EditorGUILayout.GetControlRect(false, 500.0f);
            GUIUtility.DrawTree(controlRect, this.stepper.Status, (node) =>
            {
                if (node is NativeRBTree<LineSegment2D, LineIntersectionJobs.SweepLineComparer>.TreeNode rbTreeNode)
                {
                    if (rbTreeNode.Black == 0)
                    {
                        GL.Color(Color.red);
                    }
                    else
                    {
                        GL.Color(Color.black);
                    }
                }

            });
        }
    }
}
