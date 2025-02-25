
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{


    public class RotationSystemGUI : SystemGUI
    {

        public RotationSystem rotationSystem;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 200);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Rotation System GUI");
            GUILayout.Label($"Current Points: {this.rotationSystem.GetNrOfPoints()}", this.textStyle);

            if (this.rotationSystem.GetConvexHullSampler() != null)
            {
                var sampler = this.rotationSystem.GetConvexHullSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Convex Hull (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if (this.GUIButton("Add 10 Points"))
            {
                this.rotationSystem.AddPoints(10);
            }

            if (this.GUIButton("Add 100 points"))
            {
                this.rotationSystem.AddPoints(100);
            }

            if (this.GUIButton("Add 1000 points"))
            {
                this.rotationSystem.AddPoints(1000);
            }

            GUILayout.EndArea();
        }
    }
}