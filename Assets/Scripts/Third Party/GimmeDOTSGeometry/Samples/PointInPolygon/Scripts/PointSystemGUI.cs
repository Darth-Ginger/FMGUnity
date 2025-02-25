
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class PointSystemGUI : SystemGUI
    {
        public PointSystem pointSystem;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 200);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Point System GUI");
            GUILayout.Label($"Current Points: {this.pointSystem.GetNrOfPoints()}", this.textStyle);

            if (this.pointSystem.GetPointLocationSampler() != null)
            {
                var sampler = this.pointSystem.GetPointLocationSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Point in Polygon (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if (this.GUIButton("Add 10 Points"))
            {
                this.pointSystem.AddPoints(10);
            }

            if (this.GUIButton("Add 100 points"))
            {
                this.pointSystem.AddPoints(100);
            }

            if (this.GUIButton("Add 1000 points"))
            {
                this.pointSystem.AddPoints(1000);
            }

            string buttonText = "Change Color: ON";
            if (!this.pointSystem.IsChangingColor())
            {
                buttonText = "Change Color: OFF";
            }

            if (this.GUIButton(buttonText))
            {
                this.pointSystem.ToggleChangeColor();
            }

            GUILayout.EndArea();
        }
    }
}