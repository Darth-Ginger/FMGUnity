using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class DBSCAN2DSystemGUI : SystemGUI
    {
        public DBSCAN2DSystem system;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 400);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("DBSCAN 2D GUI");
            GUILayout.Label($"Current Points: {this.system.GetNrOfPoints()}");

            if(this.system.GetDBSCANSampler() != null)
            {
                var sampler = this.system.GetDBSCANSampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"DBSCAN (ms) {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            GUILayout.Label("Epsilon:");
            this.system.Epsilon = GUILayout.HorizontalSlider(this.system.Epsilon, 0.01f, 1.0f);

            GUILayout.Label("Min. Points:");
            this.system.minClusterPoints = (int)GUILayout.HorizontalSlider(this.system.minClusterPoints, 1, 30);
            
            if(this.GUIButton("Negate Gradients"))
            {
                this.system.NegateGradients();
            }

            if(this.GUIButton("Add 10 Points"))
            {
                this.system.AddPoints(10);
            }

            if(this.GUIButton("Add 100 Points"))
            {
                this.system.AddPoints(100);
            }

            if(this.GUIButton("Add 1000 Points"))
            {
                this.system.AddPoints(1000);
            }

            GUILayout.EndArea();
        }

    }
}
