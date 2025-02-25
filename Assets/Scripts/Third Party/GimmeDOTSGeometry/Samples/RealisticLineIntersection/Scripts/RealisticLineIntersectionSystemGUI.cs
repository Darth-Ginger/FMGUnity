
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public class RealisticLineIntersectionSystemGUI : SystemGUI
    {
        public RealisticLineIntersectionSystem lineSystem;


        protected override void OnGUI()
        {
            base.OnGUI();

            var areaRect = new Rect(0, 0, 350, 180);
            GUI.Box(areaRect, "");
            GUILayout.BeginArea(areaRect);
            GUI.DrawTexture(areaRect, this.background, ScaleMode.StretchToFill);
            GUILayout.Box("Realistic Line System GUI");

            GUILayout.Label($"Current Lines: {this.lineSystem.NrOfSegments}", this.textStyle);
            GUILayout.Label($"Detected Intersections: {this.lineSystem.NrOfIntersections}", this.textStyle);
            GUILayout.Label($"Percentage of max: {this.lineSystem.NrOfIntersections / (float)(this.lineSystem.NrOfSegments * this.lineSystem.NrOfSegments)}", this.textStyle);

            if (this.lineSystem.GetLineIntersectionSampler() != null)
            {
                var sampler = this.lineSystem.GetLineIntersectionSampler();
                var recorder = sampler.GetRecorder();
                if (recorder != null)
                {
                    GUILayout.Label($"Line Intersection (ms): {recorder.elapsedNanoseconds / 10e5f}", this.textStyle);
                }
            }

            if (this.lineSystem.intersectionMethod == RealisticLineIntersectionSystem.IntersectionMethod.COMBINATORICAL)
            {
                if (this.GUIButton("Use Sweepline Detection"))
                {
                    this.lineSystem.intersectionMethod = RealisticLineIntersectionSystem.IntersectionMethod.SWEEP;
                }
            }
            else
            {
                if (this.GUIButton("Use Combinatorical Detection"))
                {
                    this.lineSystem.intersectionMethod = RealisticLineIntersectionSystem.IntersectionMethod.COMBINATORICAL;
                }
            }

            GUILayout.EndArea();
        }
    }
}