
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class PointSamplingSystemGUI : SystemGUI
    {
        public PointSamplingSystem pointSystem;

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

            if(this.GUIButton("Clear Points"))
            {
                this.pointSystem.ClearPoints();
            }

            string buttonText = "Distribution: Even";
            if (this.pointSystem.SampleMethod() == Polygon2DSampleMethod.DISTANCE_FIELD)
            {
                buttonText = "Distribution: Distance Field";
            }

            if (this.GUIButton(buttonText))
            {
                if (this.pointSystem.SampleMethod() == Polygon2DSampleMethod.EVENLY)
                {
                    this.pointSystem.ChangeSampleMethod(Polygon2DSampleMethod.DISTANCE_FIELD);
                } else
                {
                    this.pointSystem.ChangeSampleMethod(Polygon2DSampleMethod.EVENLY);
                }
            }

            GUILayout.EndArea();
        }
    }
}