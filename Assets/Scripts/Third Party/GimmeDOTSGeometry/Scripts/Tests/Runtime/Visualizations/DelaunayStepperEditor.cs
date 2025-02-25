using UnityEditor;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    [CustomEditor(typeof(DelaunayStepper))]
    public class DelaunayStepperEditor : Editor
    {
        private DelaunayStepper stepper;

        private void OnEnable()
        {
            this.stepper = this.target as DelaunayStepper;
        }

        public override void OnInspectorGUI()
        {
            if(GUILayout.Button("Step"))
            {
                this.stepper.Step();
            }

            if(GUILayout.Button("Finish")) {
                this.stepper.finished = true;
                this.stepper = null;
            };
        }
    }
}
