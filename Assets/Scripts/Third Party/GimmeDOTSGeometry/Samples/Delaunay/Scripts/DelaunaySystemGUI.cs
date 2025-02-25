using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class DelaunaySystemGUI : SystemGUI
    {
        public DelaunaySystem delaunaySystem;

        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 550);
            GUI.Box(areaRect, string.Empty);
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Delaunay GUI");
            GUILayout.Label($"Current Points: {this.delaunaySystem.GetNrOfPoints()}", this.textStyle);

            if(this.delaunaySystem.GetDelaunaySampler() != null)
            {
                var sampler = this.delaunaySystem.GetDelaunaySampler();
                var recorder = sampler.GetRecorder();
                if(recorder != null)
                {
                    GUILayout.Label($"Delaunay Triangulation (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            int rings = this.delaunaySystem.rings;
            this.delaunaySystem.rings = (int)GUILayout.HorizontalSlider(this.delaunaySystem.rings, 2, 40);
            if(this.delaunaySystem.rings != rings)
            {
                this.delaunaySystem.CreateRingPoints();
            }
            
            GUILayout.EndArea();
        }
    }
}
