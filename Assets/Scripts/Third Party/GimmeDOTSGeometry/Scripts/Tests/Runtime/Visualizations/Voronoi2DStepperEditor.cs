using UnityEditor;

namespace GimmeDOTSGeometry
{
    [CustomEditor(typeof(Voronoi2DStepper))]
    public class Voronoi2DStepperEditor : Editor
    {
        //Scrapped - see Voronoi2DJobs.cs
        /*
        private Voronoi2DStepper stepper;

        private void OnEnable()
        {
            this.stepper = this.target as Voronoi2DStepper;
        }

        public override void OnInspectorGUI()
        {
            if (this.stepper != null)
            {
                EditorGUILayout.LabelField("Status:");
                if (this.stepper.status.IsCreated)
                {
                    var controlRect = EditorGUILayout.GetControlRect(false, 400.0f);
                    GUIUtility.DrawSkipList(controlRect, this.stepper.status, 12, true);

                }

                EditorGUILayout.LabelField("Queue:");
                if (this.stepper.eventQueue.IsCreated)
                {
                    var controlRect = EditorGUILayout.GetControlRect(false, 400.0f);
                    GUIUtility.DrawSkipList(controlRect, this.stepper.eventQueue, 12, true);
                }
            }

            if (GUILayout.Button("Step"))
            {
                this.stepper.Step();
            }

            if(GUILayout.Button("Finish"))
            {
                this.stepper.finished = true;
                this.stepper = null;
            }
        }*/
    }
}
